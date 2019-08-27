// 
// Program.cs
// 
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using McMaster.Extensions.CommandLineUtils;
using McMaster.NETCore.Plugins;

using Newtonsoft.Json;

using Serilog;
using Serilog.Events;

using TroublemakerInterfaces;

using TroublemakerProxy.BLIP;
using TroublemakerProxy.Interop;

namespace TroublemakerProxy
{
    [HelpOption]
    class Program
    {
        #region Variables

        [NotNull] private readonly unsafe BLIPConnectionContainer _connectionFromClient =
            new BLIPConnectionContainer(Native.blip_connection_new());
        [NotNull] private readonly unsafe BLIPConnectionContainer _connectionFromServer =
            new BLIPConnectionContainer(Native.blip_connection_new());
        [NotNull] private readonly HttpListener _listener = new HttpListener();
        [NotNull] private readonly List<ITroublemakerPlugin> _plugins = new List<ITroublemakerPlugin>();
        [NotNull] private readonly byte[] _readWriteBuffer = new byte[32 * 1024];
        [NotNull] private readonly ClientWebSocket _toRemote = new ClientWebSocket();
        [NotNull] private MemoryStream _currentClientMessage = new MemoryStream();
        [NotNull] private MemoryStream _currentServerMessage = new MemoryStream();

        private WebSocket _fromClient;
        private ILogger _logger;
        private Configuration _parsedConfig;

        #endregion

        #region Properties

        [FileExists]
        [Option(CommandOptionType.SingleValue, Description =
            "The path to the configuration file for the proxy.  The schema file can be found in the repo.")]
        public string Config { get; set; }

        #endregion

        #region Public Methods

        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        #endregion

        #region Private Methods

        private async Task<byte[]> ApplyMessagePlugins(BLIPConnectionContainer connection, MemoryStream currentMessage,
            bool fromClient)
        {
            if (!_plugins.Any(x => x.Style.HasFlag(TamperStyle.Message))) {
                return currentMessage.ToArray();
            }

            using (var messageContainer = connection.ReadMessage(currentMessage)) {
                var message = messageContainer.CreateMessage();
                foreach (var plugin in _plugins.Where(x => x.Style.HasFlag(TamperStyle.Message))) {
                    await plugin.HandleMessageStage(ref message, fromClient);
                }

                return connection.SerializeMessage(messageContainer, message);
            }
        }

        private async Task CloseSocket(WebSocket socket, WebSocketCloseStatus status, string description)
        {
            if (socket.State == WebSocketState.Open) {
                await socket.CloseAsync(status, description, CancellationToken.None);
            } else if (socket.State == WebSocketState.CloseReceived) {
                await socket.CloseOutputAsync(status, description, CancellationToken.None);
            }
        }

        private async Task ListenForConnection()
        {
            var nextContext = await _listener.GetContextAsync();
            if (!nextContext.Request.IsWebSocketRequest) {
                _logger.Warning("Ignoring non-websocket request to {0}", nextContext.Request.Url);
                nextContext.Response.Close();
                ListenForConnection();
                return;
            }

            var webSocket = await nextContext.AcceptWebSocketAsync("BLIP_3+CBMobile_2");
            _logger.Information("Established websocket connection to client...");
            _fromClient = webSocket.WebSocket;
            var builder = new UriBuilder(nextContext.Request.Url);
            builder.Port = _parsedConfig.ToPort;
            builder.Scheme = "ws";
            _toRemote.Options.AddSubProtocol(_fromClient.SubProtocol);
            await _toRemote.ConnectAsync(builder.Uri, CancellationToken.None);
            _logger.Information("Established websocket connection to server...");
            ReadFromClient();
            ReadFromServer();
        }

        private void LoadPlugins()
        {
            if (_parsedConfig.Plugins == null) {
                return;
            }

            foreach (var plugin in _parsedConfig.Plugins) {
                var loader = PluginLoader.CreateFromAssemblyFile(plugin.Path,
                    new[] {typeof(ITroublemakerPlugin), typeof(ILogger)});
                foreach (var pluginType in loader
                    .LoadDefaultAssembly()
                    .GetTypes()
                    .Where(x => typeof(ITroublemakerPlugin).IsAssignableFrom(x) && !x.IsAbstract)) {
                    var pluginObj = (ITroublemakerPlugin) Activator.CreateInstance(pluginType);
                    if (pluginObj is TroublemakerPluginBase libraryClass) {
                        libraryClass.Log = _logger.ForContext("SourceContext", pluginObj.GetType().Name);
                        libraryClass.Log.Information("Initializing...");
                    }

                    if (plugin.ConfigPath != null) {
                        var configFilePath = plugin.ConfigPath;
                        if (!File.Exists(configFilePath)) {
                            _logger.Warning(
                                "Plugin at {0} needs config file {1} but that file could not be found, skipping...",
                                plugin.Path, configFilePath);
                            continue;
                        }

                        using (var stream = File.OpenRead(configFilePath)) {
                            var result = false;
                            try {
                                result = pluginObj.Configure(stream);
                                if (!result) {
                                    _logger.Warning("Unsuccessful configure for {0}", plugin.Path);
                                    continue;
                                }
                            } catch (Exception e) {
                                _logger.Warning("Exception caught while configuring plugin at {0}:{1}{2}",
                                    plugin.Path, Environment.NewLine, e);
                                continue;
                            }
                        }
                    }

                    _plugins.Add(pluginObj);
                }
            }
        }

        private async Task<int> OnExecute()
        {
            Console.WriteLine("Press Ctrl+C to exit...");
            var configToUse = Config ?? Prompt.GetString("Enter the path to the configuration file: ");

            using (var streamReader = new StreamReader(File.OpenRead(configToUse)))
            using (var jsonReader = new JsonTextReader(streamReader)) {
                _parsedConfig = JsonSerializer.CreateDefault().Deserialize<Configuration>(jsonReader);
            }

            SetupLogger();
            LoadPlugins();

            _listener.Prefixes.Add($"http://*:{_parsedConfig.FromPort}/");
            _listener.Start();
            ListenForConnection();
            _logger.Information($"Listening on port {_parsedConfig.FromPort}...");


            var waitHandle = new SemaphoreSlim(0, 1);
            Console.CancelKeyPress += (sender, args) => waitHandle.Release();
            await waitHandle.WaitAsync().ConfigureAwait(false);

            return 0;
        }

        private async Task Read(bool client)
        {
            ArraySegment<byte> buffer;
            WebSocket socketFrom, socketTo;
            MemoryStream currentMessage;
            BLIPConnectionContainer connection;
            if (client) {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 0, 16 * 1024);
                socketFrom = _fromClient;
                socketTo = _toRemote;
                currentMessage = _currentClientMessage;
                connection = _connectionFromClient;
            } else {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 16 * 1024, 16 * 1024);
                socketFrom = _toRemote;
                socketTo = _fromClient;
                currentMessage = _currentServerMessage;
                connection = _connectionFromServer;
            }

            if (socketFrom.State == WebSocketState.CloseReceived) {
                await CloseSocket(socketFrom, WebSocketCloseStatus.NormalClosure, "");
                return;
            }

            WebSocketReceiveResult received = null;
            try {
                received = await socketFrom.ReceiveAsync(buffer, CancellationToken.None);
            } catch (OperationCanceledException) {
                return;
            }

            if (received.MessageType == WebSocketMessageType.Close) {
                await CloseSocket(socketTo, received.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                    received.CloseStatusDescription);
                return;
            }

            foreach (var plugin in _plugins) {
                if (plugin.Style.HasFlag(TamperStyle.Network)) {
                    await plugin.HandleNetworkStage(NetworkStage.Initial, -1);
                    await plugin.HandleNetworkStage(NetworkStage.Write, received.Count);
                }

                if (plugin.Style.HasFlag(TamperStyle.Bytes)) {
                    await plugin.HandleBytesStage(buffer, received.Count, true);
                }
            }

            currentMessage.Write(_readWriteBuffer, client ? 0 : 16 * 1024, received.Count);
            if (received.EndOfMessage) {
                byte[] serialized = await ApplyMessagePlugins(connection, currentMessage, client);
                var writeBuffer = new ArraySegment<byte>(serialized);
                await socketTo.SendAsync(writeBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
                currentMessage.Dispose();
                if (client) {
                    _currentClientMessage = new MemoryStream();
                } else {
                    _currentServerMessage = new MemoryStream();
                }
            }
        }

        private async Task ReadFromClient()
        {
            await Read(true);
            if (_fromClient.State == WebSocketState.Open || _fromClient.State == WebSocketState.CloseReceived) {
                ReadFromClient();
            } else {
                _logger.Information("Client disconnected...");
                ListenForConnection();
            }
        }

        private async Task ReadFromServer()
        {
            await Read(false);
            if (_toRemote.State == WebSocketState.Open || _toRemote.State == WebSocketState.CloseReceived) {
                ReadFromServer();
            } else {
                _logger.Information("Server disconnected...");
            }
        }

        private void SetupLogger()
        {
            _logger = new LoggerConfiguration()
                .WriteTo.Console(LogEventLevel.Information,
                    "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(Path.GetTempPath(), "Logs", "troublemaker-log.txt"),
                    rollOnFileSizeLimit: true, fileSizeLimitBytes: 1024 * 1024, retainedFileCountLimit: 5,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger().ForContext("SourceContext", "TroublemakerProxy");
            _logger.Information("Logging started ({0})!", Path.Combine(Path.GetTempPath(), "Logs", "troublemaker-log.txt"));
        }

        #endregion
    }
}

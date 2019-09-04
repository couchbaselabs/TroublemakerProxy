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

namespace TroublemakerProxy
{
    public static class Misc
    {
        #region Public Methods

        public static void SafeSwap<T>(ref T old, T @new) where T : class, IDisposable
        {
            if (Object.ReferenceEquals(old, @new)) {
                return;
            }

            var oldRef = Interlocked.Exchange(ref old, @new);
            oldRef?.Dispose();
        }

        #endregion
    }

    [HelpOption]
    class Program
    {
        #region Variables

        [NotNull] private readonly HttpListener _listener = new HttpListener();
        [NotNull] private readonly List<ITroublemakerPlugin> _plugins = new List<ITroublemakerPlugin>();
        [NotNull] private readonly byte[] _readWriteBuffer = new byte[32 * 1024];

        private BLIPConnectionContainer _connectionFromClient;
        private BLIPConnectionContainer _connectionFromServer;
        [NotNull] private MemoryStream _currentClientMessage = new MemoryStream();
        [NotNull] private MemoryStream _currentServerMessage = new MemoryStream();
        private WebSocket _fromClient;
        private ILogger _logger;
        private Configuration _parsedConfig;
        private ClientWebSocket _toRemote;

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

        private async Task<(byte[] serialized, bool intercepted)> ApplyTransformPlugins(MemoryStream currentMessage,
            bool fromClient)
        {
            if (!_plugins.Any(x => x.Style.HasFlag(TamperStyle.Message) || x.Style.HasFlag(TamperStyle.Response))) {
                return (currentMessage.ToArray(), false);
            }

            var connection = fromClient ? _connectionFromClient : _connectionFromServer;
            var otherConnection = fromClient ? _connectionFromServer : _connectionFromClient;
            var intercept = false;
            using (var messageContainer = connection.ReadMessage(currentMessage)) {
                var message = messageContainer.CreateMessage();
                foreach (var plugin in _plugins.Where(x => x.Style.HasFlag(TamperStyle.Message))) {
                    try {
                        await plugin.HandleMessageStage(ref message, fromClient);
                    } catch (Exception e) {
                        _logger.Error(e, "Plugin exception during message stage ({0})", plugin.GetType().Name);
                        throw;
                    }
                }

                if (message.Type == MessageType.Request) {
                    var responsePlugin = _plugins.FirstOrDefault(x => x.Style.HasFlag(TamperStyle.Response));
                    if (responsePlugin != null) {
                        try {
                            var response = await responsePlugin.HandleResponseStage(message, fromClient);
                            if (response != null) {
                                message = response;
                                intercept = true;
                            }
                        } catch (Exception e) {
                            _logger.Error(e, "Plugin exception during response stage ({0})",
                                responsePlugin.GetType().Name);
                            throw;
                        }
                    }
                }

                var connectionToSerialize = intercept ? otherConnection : connection;
                return (connectionToSerialize.SerializeMessage(messageContainer, message), intercept);
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
                var ignore = ListenForConnection().ContinueWith(
                    t => _logger.Error(t.Exception.InnerException, "Exception in ListenForConnection"),
                    TaskContinuationOptions.OnlyOnFaulted);
                return;
            }

            var webSocket = await nextContext.AcceptWebSocketAsync("BLIP_3+CBMobile_2");
            _logger.Information("Established websocket connection to client...");
            Misc.SafeSwap(ref _fromClient, webSocket.WebSocket);
            var builder = new UriBuilder(nextContext.Request.Url)
            {
                Port = _parsedConfig.ToPort, 
                Scheme = "ws"
            };

            Misc.SafeSwap(ref _toRemote, new ClientWebSocket());
            _toRemote.Options.AddSubProtocol(_fromClient.SubProtocol);
            await _toRemote.ConnectAsync(builder.Uri, CancellationToken.None);
            _logger.Information("Established websocket connection to server...");
            Misc.SafeSwap(ref _connectionFromClient, new BLIPConnectionContainer("From Client"));
            Misc.SafeSwap(ref _connectionFromServer, new BLIPConnectionContainer("From Server"));

            var ignore2 = ReadFromClient().ContinueWith(
                t => _logger.Error(t.Exception.InnerException, "Exception in ReadFromClient"),
                TaskContinuationOptions.OnlyOnFaulted);
            ignore2 = ReadFromServer().ContinueWith(
                t => _logger.Error(t.Exception.InnerException, "Exception in ReadFromServer"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private void LoadPlugins()
        {
            if (_parsedConfig.Plugins == null) {
                return;
            }

            foreach (var plugin in _parsedConfig.Plugins) {
                var loader = PluginLoader.CreateFromAssemblyFile(plugin.Path,
                    new[] {typeof(ITroublemakerPlugin), typeof(ILogger), typeof(JsonSerializer)});
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
                            try {
                                var result = pluginObj.Configure(stream);
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
            var ignore = ListenForConnection().ContinueWith(
                t => _logger.Error(t.Exception.InnerException, "Exception in ListenForConnection"),
                TaskContinuationOptions.OnlyOnFaulted);
            ;
            _logger.Information("Listening on port {0}...", _parsedConfig.FromPort);


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
            if (client) {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 0, 16 * 1024);
                socketFrom = _fromClient;
                socketTo = _toRemote;
                currentMessage = _currentClientMessage;
            } else {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 16 * 1024, 16 * 1024);
                socketFrom = _toRemote;
                socketTo = _fromClient;
                currentMessage = _currentServerMessage;
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
                    try {
                        await plugin.HandleNetworkStage(NetworkStage.Initial, -1);
                        await plugin.HandleNetworkStage(NetworkStage.Write, received.Count);
                    } catch (Exception e) {
                        _logger.Error(e, "Plugin exception during network stage ({0})", plugin.GetType().Name);
                        throw;
                    }
                }

                if (plugin.Style.HasFlag(TamperStyle.Bytes)) {
                    try {
                        await plugin.HandleBytesStage(buffer, received.Count, true);
                    } catch (Exception e) {
                        _logger.Error(e, "Plugin exception during bytes stage ({0})", plugin.GetType().Name);
                        throw;
                    }
                }
            }

            currentMessage.Write(_readWriteBuffer, client ? 0 : 16 * 1024, received.Count);
            if (received.EndOfMessage) {
                var transformResult = await ApplyTransformPlugins(currentMessage, client);
                var writeBuffer = new ArraySegment<byte>(transformResult.serialized);
                var socketToSend = transformResult.intercepted ? socketFrom : socketTo;
                await SendNoop(transformResult, client ? _connectionFromClient : _connectionFromServer, socketTo);
                await socketToSend.SendAsync(writeBuffer, WebSocketMessageType.Binary, true, CancellationToken.None);
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
            while (_fromClient.State == WebSocketState.Open || _fromClient.State == WebSocketState.CloseReceived) {
                try {
                    await Read(true);
                } catch (Exception) {
                    _logger.Warning("Exception during read from local, resetting...");
                }
            }

            _logger.Information("Client disconnected...");
            var ignore = ListenForConnection().ContinueWith(
                t => _logger.Error(t.Exception.InnerException, "Exception in ListenForConnection"),
                TaskContinuationOptions.OnlyOnFaulted);
            ;
        }

        private async Task ReadFromServer()
        {
            while (_toRemote.State == WebSocketState.Open || _toRemote.State == WebSocketState.CloseReceived) {
                try {
                    await Read(false);
                } catch (Exception) {
                    _logger.Warning("Exception during read from remote, resetting...");
                }
            }

            _logger.Information("Server disconnected...");
        }

        private async Task SendNoop((byte[] serialized, bool intercepted) transformResult,
            BLIPConnectionContainer container, WebSocket connection)
        {
            if (!transformResult.intercepted) {
                return;
            }

            var msgNo = VarintBitConverter.ToUInt64(transformResult.serialized);
            var msg = new BLIPMessage
            {
                MessageNumber = msgNo,
                Flags = FrameFlags.NoReply,
                Properties = "no-op:true",
                Type = MessageType.Request
            };

            using (var msgContainer = new BLIPMessageContainer()) {
                var msgBytes = container.SerializeMessage(msgContainer, msg);
                await connection.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Binary, true,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private void SetupLogger()
        {
            var logsDir = Path.Combine(Path.GetTempPath(), "Logs");
            Directory.CreateDirectory(logsDir);

            _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(LogEventLevel.Information,
                    "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(logsDir, "troublemaker-log.txt"),
                    rollOnFileSizeLimit: true, fileSizeLimitBytes: 1024 * 1024, retainedFileCountLimit: 5,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .CreateLogger().ForContext("SourceContext", "TroublemakerProxy");
            _logger.Information("Logging started ({0})!",
                Path.Combine(Path.GetTempPath(), "Logs", "troublemaker-log.txt"));
        }

        #endregion
    }
}
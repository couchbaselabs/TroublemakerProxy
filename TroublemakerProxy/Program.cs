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

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
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

        public static void SafeSwap<T>(ref T? old, T? @new) where T : class, IDisposable
        {
            if (Object.ReferenceEquals(old, @new)) {
                return;
            }

            var oldRef = Interlocked.Exchange(ref old, @new);
            oldRef?.Dispose();
        }

        public static void SafeSwapNonNull<T>(ref T old, T @new) where T : class, IDisposable
        {
            if (Object.ReferenceEquals(old, @new)) {
                return;
            }

            var oldRef = Interlocked.Exchange(ref old, @new);
            oldRef.Dispose();
        }

        #endregion
    }

    [HelpOption]
    [UsedImplicitly]
    internal class Program
    {
        #region Constants

        // Trial and error for this list since there is no API to get what headers are set
        // automatically on ClientWebSocket
        private static readonly HashSet<string> ExcludedHeaders = new()
        {
            "Sec-WebSocket-Version", "Sec-WebSocket-Key", "Connection", "Upgrade"
        };

        #endregion

        #region Variables

        private readonly HttpListener _listener = new();
        private readonly ILogger _logger = CreateLogger();
        private readonly List<ITroublemakerPlugin> _plugins = new();
        private readonly byte[] _readWriteBuffer = new byte[32 * 1024];

        private BLIPConnectionContainer _connectionFromClient = new("From Client");
        private BLIPConnectionContainer _connectionFromServer = new("From Server");
        private MemoryStream _currentClientMessage = new();
        private MemoryStream _currentServerMessage = new();
        private WebSocket? _fromClient;
        private Configuration? _parsedConfig;
        private ClientWebSocket? _toRemote;

        #endregion

        #region Properties

        [FileExists]
        [Option(CommandOptionType.SingleValue, Description =
            "The path to the configuration file for the proxy.  The schema file can be found in the repo.")]
        [UsedImplicitly] 
        public string? Config { get; [UsedImplicitly] set; }

        #endregion

        #region Public Methods

        public static int Main(string[] args)
            => CommandLineApplication.Execute<Program>(args);

        #endregion

        #region Private Methods

        private static async Task CloseSocket(WebSocket socket, WebSocketCloseStatus status, string description)
        {
            switch (socket.State) {
                case WebSocketState.Open:
                    await socket.CloseAsync(status, description, CancellationToken.None);
                    break;
                case WebSocketState.CloseReceived:
                    await socket.CloseOutputAsync(status, description, CancellationToken.None);
                    break;
                case WebSocketState.None:
                case WebSocketState.Connecting:
                case WebSocketState.CloseSent:
                case WebSocketState.Closed:
                case WebSocketState.Aborted:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static ILogger CreateLogger()
        {
            var logsDir = Path.Combine(Path.GetTempPath(), "Logs");
            Directory.CreateDirectory(logsDir);

            var logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(LogEventLevel.Information,
                    "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(logsDir, "troublemaker-log.txt"),
                    rollOnFileSizeLimit: true, fileSizeLimitBytes: 1024 * 1024, retainedFileCountLimit: 5,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}")
                .CreateLogger().ForContext("SourceContext", "TroublemakerProxy");
            logger.Information("Logging started ({0})!",
                Path.Combine(Path.GetTempPath(), "Logs", "troublemaker-log.txt"));
            return logger;
        }

        private async Task<(byte[] serialized, bool intercepted)> ApplyTransformPlugins(MemoryStream currentMessage,
            bool fromClient)
        {
            if (!_plugins.Any(x => x.Style.HasFlag(TamperStyle.Message) || x.Style.HasFlag(TamperStyle.Response))) {
                return (currentMessage.ToArray(), false);
            }

            var connection = fromClient ? _connectionFromClient : _connectionFromServer;
            var otherConnection = fromClient ? _connectionFromServer : _connectionFromClient;
            var intercept = false;

            using var messageContainer = connection.ReadMessage(currentMessage);
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

        private void CloseAndRetry(HttpListenerContext context)
        {
            context.Response.Close();
#pragma warning disable CS4014
            ListenForConnection().ContinueWith(
                t => _logger.Error(t.Exception?.InnerException, "Exception in ListenForConnection"),
                TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
        }

        private bool HandleNetworkAction(NetworkAction action)
        {
            switch (action) {
                case NetworkAction.Continue:
                    return false;
                case NetworkAction.BreakPipe:
                    _fromClient?.Abort();
                    _toRemote?.Abort();
                    break;
                case NetworkAction.CloseWebSocket:

                    if (_fromClient == null) {
                        _logger.Error("Unexpected _fromClient null in HandleNetworkAction");
                    } else {
#pragma warning disable CS4014
                        CloseSocket(_fromClient, (WebSocketCloseStatus)_parsedConfig!.WebSocketDisconnectCode, _parsedConfig.WebSocketDisconnectMessage ?? "The server is on fire!");
#pragma warning restore CS4014
                    }
                    break;
            }

            return true;
        }

        private async Task ListenForConnection()
        {
            var nextContext = await _listener.GetContextAsync();
            _logger.Verbose($"Got connection from {nextContext.Request.RemoteEndPoint.Address}");
            if (!nextContext.Request.IsWebSocketRequest) {
                _logger.Warning("Ignoring non-websocket request to {0}", nextContext.Request.Url);
                CloseAndRetry(nextContext);
                return;
            }

            foreach (var plugin in _plugins.Where(x => x.Style.HasFlag(TamperStyle.Network))) {
                var nextAction = await plugin.HandleNetworkStage(NetworkStage.Connect, 0).ConfigureAwait(false);
                if (nextAction != NetworkAction.CloseHTTP) {
                    continue;
                }

                nextContext.Response.StatusCode = _parsedConfig!.HttpDisconnectCode;
                if (_parsedConfig.HttpDisconnectMessage != null) {
                    nextContext.Response.StatusDescription = _parsedConfig.HttpDisconnectMessage;
                }

                CloseAndRetry(nextContext);
                return;
            }

            var builder = new UriBuilder(nextContext.Request.Url ??
                                         throw new ArgumentException("Invalid context received (no url)"))
            {
                Host = _parsedConfig!.ToHost,
                Port = _parsedConfig.ToPort,
                Scheme = "ws"
            };

            Misc.SafeSwap(ref _toRemote, new ClientWebSocket());
            foreach (string header in nextContext.Request.Headers) {
                if (ExcludedHeaders.Contains(header)) {
                    continue;
                }

                switch (header) {
                    case "Sec-WebSocket-Protocol":
                    {
                        foreach (var protocol in nextContext.Request.Headers[header]!.Split(',')) {
                            _logger.Verbose($"Adding websocket subprotocol {protocol}");
                            _toRemote!.Options.AddSubProtocol(protocol);
                        }

                        break;
                    }
                    case "Host":
                    {
                        _logger.Verbose("Adding altered header 'Host' from origin...");
                        _toRemote!.Options.SetRequestHeader(header, nextContext.Request.Headers[header]!
                            .Replace(_parsedConfig.FromPort.ToString(), _parsedConfig.ToPort.ToString()));
                        break;
                    }
                    default:
                    {
                        _logger.Verbose($"Adding header '{header}' from origin...");
                        _toRemote!.Options.SetRequestHeader(header, nextContext.Request.Headers[header]);
                        break;
                    }
                }
            }

            try {
                await _toRemote!.ConnectAsync(builder.Uri, CancellationToken.None);
            } catch (WebSocketException e) {
                if (e.WebSocketErrorCode != WebSocketError.NotAWebSocket) {
                    throw;
                }

                // This is a horrid dance, WebSocketException doesn't give any information
                // about the underlying error code.
                var regex = new Regex("The server returned status code '(\\d+)' when status code '101' was expected.");
                var match = regex.Match(e.Message);
                if (!match.Success) {
                    throw;
                }

                var returnCode = Int32.Parse(match.Groups[1].Value);
                _logger.Verbose($"Got error code '{returnCode}' from destination, returning to origin...");
                nextContext.Response.StatusCode = returnCode;

                if (returnCode == 401) {
                    // Hope this doesn't change because there is no bleeding way to access it, even via reflection
                    // with ClientWebSocket
                    nextContext.Response.AddHeader("Www-Authenticate", "Basic realm=\"Couchbase Sync Gateway\"");
                }

                CloseAndRetry(nextContext);
                return;
            }

            _logger.Information("Established websocket connection to server...");
            var webSocket = await nextContext.AcceptWebSocketAsync(_toRemote.SubProtocol);

            _logger.Information("Established websocket connection to client...");
            Misc.SafeSwap(ref _fromClient, webSocket.WebSocket);

            Misc.SafeSwapNonNull(ref _connectionFromClient, new BLIPConnectionContainer("From Client"));
            Misc.SafeSwapNonNull(ref _connectionFromServer, new BLIPConnectionContainer("From Server"));

#pragma warning disable CS4014
            ReadFromClient().ContinueWith(
                t => _logger.Error(t.Exception?.InnerException, "Exception in ReadFromClient"),
                TaskContinuationOptions.OnlyOnFaulted);
            ReadFromServer().ContinueWith(
                t => _logger.Error(t.Exception?.InnerException, "Exception in ReadFromServer"),
                TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
        }

        private void LoadPlugins()
        {
            if (_parsedConfig!.Plugins == null) {
                return;
            }

            foreach (var plugin in _parsedConfig.Plugins) {
                var loader = PluginLoader.CreateFromAssemblyFile(plugin.Path,
                    new[] { typeof(ITroublemakerPlugin), typeof(ILogger), typeof(JsonSerializer) });
                foreach (var pluginType in loader
                             .LoadDefaultAssembly()
                             .GetTypes()
                             .Where(x => typeof(ITroublemakerPlugin).IsAssignableFrom(x) && !x.IsAbstract)) {
                    ITroublemakerPlugin pluginObj;
                    if (typeof(TroublemakerPluginBase).IsAssignableFrom(pluginType)) {
                        pluginObj = (ITroublemakerPlugin)Activator.CreateInstance(pluginType, _logger.ForContext("SourceContext", pluginType.Name))!;
                        ((TroublemakerPluginBase)pluginObj).Log.Information("Initializing...");
                    } else {
                        pluginObj = (ITroublemakerPlugin)Activator.CreateInstance(pluginType)!;
                    }

                    if (plugin.ConfigPath != null) {
                        var configFilePath = plugin.ConfigPath;
                        if (!File.Exists(configFilePath)) {
                            _logger.Warning(
                                "Plugin at {0} needs config file {1} but that file could not be found, skipping...",
                                plugin.Path, configFilePath);
                            continue;
                        }

                        using var stream = File.OpenRead(configFilePath);
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

                    _plugins.Add(pluginObj);
                }
            }
        }

        // ReSharper disable once UnusedMember.Local
        private async Task<int> OnExecute()
        {
            Console.WriteLine("Press Ctrl+C to exit...");
            var configToUse = Config;
            while (configToUse == null) {
                configToUse = Prompt.GetString("Enter the path to the configuration file: ");
            }

            using (var streamReader = new StreamReader(File.OpenRead(configToUse)))
            using (var jsonReader = new JsonTextReader(streamReader)) {
                _parsedConfig = JsonSerializer.CreateDefault().Deserialize<Configuration>(jsonReader);
            }

#pragma warning disable CS0162 // Unreachable code detected
            _logger.Information($"Starting TroublemakerProxy {ThisAssembly.Git.Tag}");
            if (ThisAssembly.Git.IsDirty) {
                _logger.Warning("Repository has been modified locally!");
            }
#pragma warning restore CS0162 // Unreachable code detected

            LoadPlugins();

            _listener.Prefixes.Add($"http://*:{_parsedConfig!.FromPort}/");
            _listener.Start();

#pragma warning disable CS4014
            ListenForConnection().ContinueWith(
                t => _logger.Error(t.Exception?.InnerException, "Exception in ListenForConnection"),
                TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
            _logger.Information("Listening on port {0}...", _parsedConfig.FromPort);


            var waitHandle = new SemaphoreSlim(0, 1);
            Console.CancelKeyPress += (_, _) => waitHandle.Release();
            await waitHandle.WaitAsync().ConfigureAwait(false);

            return 0;
        }

        private async Task Read(bool client)
        {
            ArraySegment<byte> buffer;
            WebSocket socketFrom, socketTo;
            MemoryStream currentMessage;
            string fromName, toName;
            if (client) {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 0, 16 * 1024);
                socketFrom = _fromClient!;
                socketTo = _toRemote!;
                fromName = "client";
                toName = "remote";
                currentMessage = _currentClientMessage;
            } else {
                buffer = new ArraySegment<byte>(_readWriteBuffer, 16 * 1024, 16 * 1024);
                socketFrom = _toRemote!;
                socketTo = _fromClient!;
                fromName = "remote";
                toName = "client";
                currentMessage = _currentServerMessage;
            }

            if (socketFrom.State == WebSocketState.CloseReceived) {
                _logger.Verbose($"Closing {fromName} socket in response to close message...");
                await CloseSocket(socketFrom, WebSocketCloseStatus.NormalClosure, "");
                return;
            }

            WebSocketReceiveResult received;
            try {
                _logger.Verbose($"Sleeping until next input from {fromName}...");
                received = await socketFrom.ReceiveAsync(buffer, CancellationToken.None);
                _logger.Verbose($"Received {received.Count} bytes from {fromName}...");
            } catch (OperationCanceledException) {
                return;
            }

            if (received.MessageType == WebSocketMessageType.Close) {
                _logger.Verbose($"Closing {toName} socket in response to close message...");
                await CloseSocket(socketTo, received.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                    received.CloseStatusDescription ?? String.Empty);
                return;
            }

            foreach (var plugin in _plugins) {
                if (plugin.Style.HasFlag(TamperStyle.Network)) {
                    try {
                        _logger.Verbose("Initial network stage...");
                        var action = await plugin.HandleNetworkStage(NetworkStage.Initial, -1);
                        if (HandleNetworkAction(action)) {
                            return;
                        }

                        _logger.Verbose("Write network stage...");
                        action = await plugin.HandleNetworkStage(NetworkStage.Write, received.Count);
                        if (HandleNetworkAction(action)) {
                            return;
                        }
                    } catch (Exception e) {
                        _logger.Error(e, "Plugin exception during network stage ({0})", plugin.GetType().Name);
                        throw;
                    }
                }

                if (plugin.Style.HasFlag(TamperStyle.Bytes)) {
                    try {
                        _logger.Verbose("Byte tampering stage...");
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
                await currentMessage.DisposeAsync();
                if (client) {
                    _currentClientMessage = new MemoryStream();
                } else {
                    _currentServerMessage = new MemoryStream();
                }
            }
        }

        private async Task ReadFromClient()
        {
            while (_fromClient!.State == WebSocketState.Open || _fromClient.State == WebSocketState.CloseReceived) {
                try {
                    await Read(true);
                } catch (Exception) {
                    _logger.Warning("Exception during read from local, resetting...");
                }
            }

            _logger.Information("Client disconnected...");
#pragma warning disable CS4014
            ListenForConnection().ContinueWith(
                t => _logger.Error(t.Exception?.InnerException, "Exception in ListenForConnection"),
                TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014
        }

        private async Task ReadFromServer()
        {
            while (_toRemote!.State == WebSocketState.Open || _toRemote.State == WebSocketState.CloseReceived) {
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

            using var msgContainer = new BLIPMessageContainer();
            var msgBytes = container.SerializeMessage(msgContainer, msg);
            await connection.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Binary, true,
                CancellationToken.None).ConfigureAwait(false);
        }

        #endregion
    }
}
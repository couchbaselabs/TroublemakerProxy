﻿// 
// DisconnectionPlugin.cs
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

#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Serilog;
using sly.parser.generator;

using TroublemakerInterfaces;

namespace DisconnectionPlugin
{
    [UsedImplicitly]
    public sealed class DisconnectionPlugin : TroublemakerPluginBase<Configuration>
    {
        #region Variables

        private NetworkAction _nextAction;
        private Pattern? _pattern;
        private DateTime? _started;

        #endregion

        #region Properties

        public override TamperStyle Style => ParsedConfig?.DisconnectType == DisconnectType.BLIPErrorMessage
            ? TamperStyle.Response
            : TamperStyle.Response | TamperStyle.Network;

        #endregion

        #region Constructors

        public DisconnectionPlugin(ILogger log) : base(log)
        {
        }

        #endregion

        #region Private Methods

        private async Task<BLIPMessage?> SetupDisconnect(ulong number)
        {
            switch (ParsedConfig?.DisconnectType) {
                case DisconnectType.WebsocketClose:
                    _nextAction = NetworkAction.CloseWebSocket;
                    break;
                case DisconnectType.HTTPClose:
                    _nextAction = NetworkAction.CloseHTTP;
                    break;
                case DisconnectType.PipeBreak:
                    _nextAction = NetworkAction.BreakPipe;
                    break;
                case DisconnectType.Timeout:
                    await Task.Delay(TimeSpan.FromMinutes(2));
                    break;
                case DisconnectType.BLIPErrorMessage:
                    return new BLIPMessage
                    {
                        Type = MessageType.Error,
                        MessageNumber = number,
                        Properties = "Error-Domain:HTTP:Error-Code:500",
                        Body = Encoding.ASCII.GetBytes("The server is on fire!")
                    };
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return null;
        }

        #endregion

        #region Overrides

        public override Task<NetworkAction> HandleNetworkStage(NetworkStage stage, int size) =>
            _pattern!.Evaluate(new BLIPMessage(), default(TimeSpan))
                ? Task.FromResult(_nextAction)
                : Task.FromResult(NetworkAction.Continue);

        public override Task<BLIPMessage?> HandleResponseStage(BLIPMessage message, bool fromClient)
        {
            _started ??= DateTime.Now;

            return _pattern?.Evaluate(message, DateTime.Now - _started.Value) == true
                ? SetupDisconnect(message.MessageNumber) 
                : Task.FromResult(default(BLIPMessage));
        }

        protected override bool Init()
        {
            var builder = new ParserBuilder<PatternToken, Pattern>();
            var parser = builder.BuildParser(new PatternParser(),
                ParserType.LL_RECURSIVE_DESCENT, "blip_comparison");
            if (parser.IsError) {
                Log.Error("Unable to create parser ({0})",
                    JsonConvert.SerializeObject(parser.Errors.Select(x => x.ToString())));
                return false;
            }

            if (ParsedConfig == null) {
                Log.Error("Null config!");
                return false;
            }

            if (ParsedConfig.DisconnectType == DisconnectType.HTTPClose) {
                _nextAction = NetworkAction.CloseHTTP;
            }

            foreach (var clause in ParsedConfig.PatternClauses) {
                var parseResult = parser.Result.Parse(clause.ToLowerInvariant());
                if (parseResult.IsError) {
                    Log.Error("Unable to parse text ({0})",
                        JsonConvert.SerializeObject(parseResult.Errors.Select(x => x.ToString())));
                    return false;
                }

                _pattern = parseResult.Result;
            }

            if (_pattern != null || _nextAction == NetworkAction.CloseHTTP) {
                return true;
            }

            Log.Error("No pattern clauses provided!");
            return false;

        }

        #endregion
    }
}
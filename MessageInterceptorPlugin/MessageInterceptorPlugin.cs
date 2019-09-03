// 
// MessageInterceptorPlugin.cs
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

using System.Collections.Generic;
using System.Threading.Tasks;

using TroublemakerInterfaces;

namespace MessageInterceptorPlugin
{
    public sealed class MessageInterceptorPlugin : TroublemakerPluginBase<Configuration>
    {
        private Dictionary<Awaiting, IReadOnlyList<IOutputTransform>> _awaitingReplies =
            new Dictionary<Awaiting, IReadOnlyList<IOutputTransform>>();

        private struct Awaiting
        {
            public ulong Number;
            public bool FromClient;
        }

        #region Properties

        public override TamperStyle Style => TamperStyle.Message;

        #endregion

        #region Private Methods

        private bool IsValidDirection(bool fromClient, Rule.Direction direction)
        {
            if (fromClient) {
                return direction.HasFlag(Rule.Direction.ToServer);
            }

            return direction.HasFlag(Rule.Direction.ToClient);
        }

        private bool HandleAwaitingOut(ref BLIPMessage message, bool fromClient)
        {
            if (message.Type != MessageType.Response && message.Type != MessageType.Error) {
                return false;
            }

            var awaiting = new Awaiting
            {
                FromClient = fromClient,
                Number = message.MessageNumber
            };

            if (_awaitingReplies.ContainsKey(awaiting)) {
                var transforms = _awaitingReplies[awaiting];
                foreach (var transform in transforms) {
                    Log.Verbose("Applying rule for message {0} ({1})", 
                        fromClient ? "from client" : "from server",
                        transform);
                    transform.Transform(ref message);
                }

                _awaitingReplies.Remove(awaiting);
                return true;
            }

            return false;
        }

        private void HandleAwaitingIn(BLIPMessage message, bool fromClient, Rule rule)
        {
            var awaiting = new Awaiting
            {
                FromClient = !fromClient,
                Number = message.MessageNumber
            };

            _awaitingReplies[awaiting] = rule.OutputTransforms;
        }

        #endregion

        #region Overrides

        public override Task HandleMessageStage(ref BLIPMessage message, bool fromClient)
        {
            var usedRules = new List<Rule>();
            foreach (var rule in ParsedConfig.Rules) {
                if (HandleAwaitingOut(ref message, fromClient)) {
                    usedRules.Add(rule);
                }

                if (IsValidDirection(fromClient, rule.RuleDirection) && rule.Criteria.Matches(message)) {
                    if (rule.Criteria.ApplyToReply) {
                        HandleAwaitingIn(message, fromClient, rule);
                        continue;
                    }
                    
                    foreach (var transform in rule.OutputTransforms) {
                        Log.Verbose("Applying rule for message {0} ({1})", 
                            fromClient ? "from client" : "from server",
                            transform);
                        transform.Transform(ref message);
                    }

                    usedRules.Add(rule);
                }
            }

            foreach (var usedRule in usedRules) {
                ParsedConfig.Used(usedRule);
            }

            return Task.CompletedTask;
        }

        protected override bool Init()
        {
            Log.Information("Applying the following rules to messages:");
            foreach (var rule in ParsedConfig.Rules) {
                Log.Information($"\t{rule}");
            }

            return true;
        }

        #endregion
    }
}
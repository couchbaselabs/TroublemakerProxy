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
using System.Linq;
using System.Threading.Tasks;

using TroublemakerInterfaces;

namespace MessageInterceptorPlugin
{
    public sealed class MessageInterceptorPlugin : TroublemakerPluginBase<Configuration>
    {
        #region Properties

        public override TamperStyle Style => TamperStyle.Response;

        #endregion

        #region Private Methods

        private bool IsValidDirection(bool fromClient, Rule.Direction direction)
        {
            if (fromClient) {
                return direction.HasFlag(Rule.Direction.ToServer);
            }

            return direction.HasFlag(Rule.Direction.ToClient);
        }

        #endregion

        #region Overrides

        public override Task<BLIPMessage> HandleResponseStage(BLIPMessage message, bool fromClient)
        {
            var response = new BLIPMessage
            {
                Type = MessageType.Response,
                MessageNumber = message.MessageNumber
            };
            var usedRules = new List<Rule>();
            foreach (var rule in ParsedConfig.Rules) {
                if (IsValidDirection(fromClient, rule.RuleDirection) && rule.Criteria.Matches(message)) {
                    foreach (var transform in rule.OutputTransforms) {
                        Log.Verbose("Applying rule for message {0} ({1})", 
                            fromClient ? "from client" : "from server",
                            transform);
                        transform.Transform(ref response);
                    }

                    usedRules.Add(rule);
                }
            }

            foreach (var rule in usedRules) {
                ParsedConfig.Used(rule);
            }

            return usedRules.Any() ? Task.FromResult(response) : Task.FromResult(default(BLIPMessage));
        }

        protected override bool Init()
        {
            Log.Information("Applying the following rules to messages:");
            foreach (var rule in ParsedConfig.Rules) {
                Log.Information("\tRule: {0}", rule);
            }

            return true;
        }

        #endregion
    }
}
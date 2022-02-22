// 
// NoCompressionPlugin.cs
// 
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

using System.Threading.Tasks;
using JetBrains.Annotations;
using Serilog;
using TroublemakerInterfaces;

namespace NoCompressionPlugin
{
    [UsedImplicitly]
    public sealed class NoCompressionPlugin : TroublemakerPluginBase
    {
        #region Properties

        public override TamperStyle Style => TamperStyle.Message;

        #endregion

        #region Constructors

        public NoCompressionPlugin(ILogger log) : base(log)
        {
        }

        #endregion

        #region Overrides

        public override Task HandleMessageStage(ref BLIPMessage message, bool fromClient)
        {
            var before = message.Flags;
            message.Flags &= ~FrameFlags.Compressed;
            var after = message.Flags;
            if (before != after) {
                Log.Information("Disabled compression on {0} #{1} {2}", message.Type, message.MessageNumber,
                    fromClient ? "to server" : "to client");
            } else {
                Log.Verbose("Ignored non-compressed {0} #{1} {2}, ", message.Type, message.MessageNumber,
                    fromClient ? "to server" : "to client");
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}
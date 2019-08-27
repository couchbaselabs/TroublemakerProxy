using System.Threading.Tasks;

using TroublemakerInterfaces;

namespace NoCompressionPlugin
{
    public sealed class NoCompressionPlugin : TroublemakerPluginBase
    {
        #region Properties

        public override TamperStyle Style => TamperStyle.Message;

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
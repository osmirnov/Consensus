using System.Collections.Concurrent;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class PreprocessingHandler
    {
        public static void Handle(
            PreprocessingMessage message,
            out byte[] replicaSecret)
        {
            replicaSecret = message.ReplicaSecret;
        }
    }
}

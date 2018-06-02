using System.Collections.Concurrent;
using System.Collections.Generic;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class PreprocessingHandler
    {
        public static void Handle(
            PreprocessingMessage message,
            Dictionary<int, byte[]> replicaSecrets)
        {
            replicaSecrets.Add(message.ReplicaSecretIndex, message.ReplicaSecret);
        }
    }
}

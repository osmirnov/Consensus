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
            replicaSecrets.Clear();

            for (var i = 0; i < message.ReplicaSecrets.Count; i++)
            {
                replicaSecrets.Add(i, message.ReplicaSecrets[i]);
            }
        }
    }
}

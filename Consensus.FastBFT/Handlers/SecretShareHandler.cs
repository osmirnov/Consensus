using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Handlers
{
    public class SecretShareHandler
    {
        public static void Handle(
            SecretShareMessage message,
            Tee tee,
            ConcurrentDictionary<int, Dictionary<int, uint>> allChildrenSecretHashes,
            ConcurrentDictionary<int, Dictionary<int, CancellationTokenSource>> allSecretShareMessageTokenSources)
        {
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources;
            if (allSecretShareMessageTokenSources.TryGetValue(message.CorrelationId, out secretShareMessageTokenSources) == false)
            {
                return;
            }

            secretShareMessageTokenSources[message.ReplicaId].Cancel();

            Dictionary<int, uint> childrenSecretHashes;
            if (allChildrenSecretHashes.TryGetValue(message.CorrelationId, out childrenSecretHashes) == false)
            {
                return;
            }

            if (tee.Crypto.GetHash(message.SecreShare) == childrenSecretHashes[message.ReplicaId])
            {

            }
        }
    }
}

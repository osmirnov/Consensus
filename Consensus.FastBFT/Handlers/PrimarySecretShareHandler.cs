using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Handlers
{
    public class PrimarySecretShareHandler : Handler
    {
        public static void Handle(
            SecretShareMessage message,
            Tee tee,
            Replica replica,
            string replicaSecretShare,
            Dictionary<int, uint> childSecretHashes,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources,
            ConcurrentDictionary<int, string> verifiedChildShareSecrets)
        {
            var childReplicaId = message.ReplicaId;
            var childSecretShare = message.SecreShare;

            secretShareMessageTokenSources[childReplicaId].Cancel();

            if (tee.Crypto.GetHash(childSecretShare) != childSecretHashes[childReplicaId])
            {
                return;
            }

            if (verifiedChildShareSecrets.TryAdd(childReplicaId, childSecretShare) == false)
            {
                return;
            }

            if (verifiedChildShareSecrets.Count != replica.childReplicas.Count)
            {
                return;
            }

            if (verifiedChildShareSecrets.Keys.OrderBy(_ => _)
                    .SequenceEqual(replica.childReplicas.Select(r => r.id).OrderBy(_ => _)) == false)
            {
                return;
            }

            var verifiedSecretShares = verifiedChildShareSecrets
                .Select(x => new { ReplicaId = x.Key, SecretShare = x.Value })
                .OrderBy(x => x.ReplicaId)
                .Select(x => x.SecretShare)
                .ToList();

            var secret = string.Join(string.Empty, verifiedSecretShares);

            Log("All ready to commit");

            // tee.Crypto.GetHash(())
        }
    }
}
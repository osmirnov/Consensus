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
            ConcurrentDictionary<int, string> allReplicaSecretShares,
            ConcurrentDictionary<int, Dictionary<int, uint>> allChildSecretHashes,
            ConcurrentDictionary<int, Dictionary<int, CancellationTokenSource>> allSecretShareMessageTokenSources,
            ConcurrentDictionary<int, Dictionary<int, string>> allVerifiedChildShareSecrets)
        {
            string replicaSecretShare;
            if (allReplicaSecretShares.TryGetValue(message.CorrelationId, out replicaSecretShare) == false)
            {
                return;
            }

            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources;
            if (allSecretShareMessageTokenSources.TryGetValue(message.CorrelationId, out secretShareMessageTokenSources) == false)
            {
                return;
            }

            var childReplicaId = message.ReplicaId;
            var childSecretShare = message.SecreShare;

            secretShareMessageTokenSources[childReplicaId].Cancel();

            Dictionary<int, uint> childSecretHashes;
            if (allChildSecretHashes.TryGetValue(message.CorrelationId, out childSecretHashes) == false)
            {
                return;
            }

            if (tee.Crypto.GetHash(childSecretShare) != childSecretHashes[childReplicaId])
            {
                return;
            }

            var currentVerifiedChildSecretShares = allVerifiedChildShareSecrets.AddOrUpdate(
                message.CorrelationId,
                _ => new Dictionary<int, string> { { childReplicaId, childSecretShare } },
                (_, verifiedChildSecretHashes) =>
                {
                    verifiedChildSecretHashes.Add(childReplicaId, childSecretShare);
                    return verifiedChildSecretHashes;
                });

            if (currentVerifiedChildSecretShares.Count != replica.childReplicas.Count)
            {
                return;
            }

            if (currentVerifiedChildSecretShares.Keys.OrderBy(_ => _)
                    .SequenceEqual(replica.childReplicas.Select(r => r.id).OrderBy(_ => _)) == false)
            {
                return;
            }

            var verifiedSecretShares = currentVerifiedChildSecretShares
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
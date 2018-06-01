using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class SecretShareHandler
    {
        public static void Handle(
            SecretShareMessage message,
            Replica replica,
            string replicaSecretShare,
            Dictionary<int, uint> childSecretHashes,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources,
            ConcurrentDictionary<int, string> verifiedChildShareSecrets)
        {
            var childReplicaId = message.ReplicaId;
            var childSecretShare = message.ReplicaSecretShares[message.ReplicaId];

            secretShareMessageTokenSources[childReplicaId].Cancel();

            if (Crypto.GetHash(childSecretShare) != childSecretHashes[childReplicaId])
            {
                replica.ParentReplica.SendMessage(
                    new SuspectMessage
                    {
                        ReplicaId = childReplicaId
                    });
            }

            if (verifiedChildShareSecrets.TryAdd(childReplicaId, childSecretShare) == false)
            {
                return;
            }

            if (verifiedChildShareSecrets.Count != replica.ChildReplicas.Count)
            {
                return;
            }

            if (verifiedChildShareSecrets.Keys.OrderBy(_ => _)
                    .SequenceEqual(replica.ChildReplicas.Select(r => r.Id).OrderBy(_ => _)) == false)
            {
                return;
            }

            var verifiedSecretShares = verifiedChildShareSecrets;

            verifiedSecretShares.TryAdd(replica.Id, replicaSecretShare);

            replica.ParentReplica.SendMessage(
                new SecretShareMessage
                {
                    ReplicaId = replica.Id,
                    ReplicaSecretShares = verifiedSecretShares.ToDictionary(kv => kv.Key, kv => kv.Value)
                });
        }
    }
}

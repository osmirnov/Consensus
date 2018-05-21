using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

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
            var childSecretShare = message.SecreShare;

            secretShareMessageTokenSources[childReplicaId].Cancel();

            if (replica.Tee.Crypto.GetHash(childSecretShare) != childSecretHashes[childReplicaId])
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

            var verifiedSecretShares = verifiedChildShareSecrets
                .Select(x => new { ReplicaId = x.Key, SecretShare = x.Value })
                .OrderBy(x => x.ReplicaId)
                .Select(x => x.SecretShare)
                .ToList();

            verifiedSecretShares.Insert(0, replicaSecretShare);

            replica.ParentReplica.SendMessage(
                new SecretShareMessage
                {
                    ReplicaId = replica.Id,
                    SecreShare = string.Join(string.Empty, verifiedSecretShares)
                });
        }
    }
}

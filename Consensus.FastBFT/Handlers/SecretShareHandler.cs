using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class SecretShareHandler : Handler
    {
        public static void Handle(
            SecretShareMessage message,
            Replica replica,
            string replicaSecretShare,
            Dictionary<int, uint> childSecretHashes,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources,
            ConcurrentDictionary<int, string> verifiedChildrenSecretShares)
        {
            var childReplicaId = message.ReplicaId;
            var childReplicaSecretShare = message.ReplicaSecretShares[message.ReplicaId];

            secretShareMessageTokenSources[childReplicaId].Cancel();
            secretShareMessageTokenSources.Remove(childReplicaId);

            if (Crypto.GetHash(childReplicaSecretShare) != childSecretHashes[childReplicaId])
            {
                replica.ParentReplica.SendMessage(
                    new SuspectMessage
                    {
                        ReplicaId = childReplicaId
                    });
            }

            foreach (var childSecretShare in message.ReplicaSecretShares)
            {
                if (verifiedChildrenSecretShares.TryAdd(childSecretShare.Key, childSecretShare.Value) == false)
                {
                    throw new InvalidOperationException($"The child secret share for replica #{childSecretShare.Key} has already been delivered.");
                }
            }

            if (replica.ChildReplicas.All(chr => verifiedChildrenSecretShares.ContainsKey(chr.Id)) == false)
            {
                return;
            }

            verifiedChildrenSecretShares.TryAdd(replica.Id, replicaSecretShare);

            // we send a message with a secret share to the parent replica
            Network.EmulateLatency();

            replica.ParentReplica.SendMessage(
                new SecretShareMessage
                {
                    ReplicaId = replica.Id,
                    ReplicaSecretShares = verifiedChildrenSecretShares.ToDictionary(kv => kv.Key, kv => kv.Value)
                });

            Log(replica, "Send a secret share to the parent replica (ParentReplicaId: {0})", replica.ParentReplica.Id);
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Handlers
{
    public class PrimarySecretShareHandler : Handler
    {
        public static void Handle(
            SecretShareMessage message,
            PrimaryReplica primaryReplica,
            IEnumerable<ReplicaBase> activeRelicas,
            int[] block,
            ref bool isCommitted,
            byte[] signedSecretHashAndCounterViewNumber,
            Dictionary<int, uint> childSecretHashes,
            ConcurrentDictionary<int, string> verifiedChildShareSecrets)
        {
            var childReplicaId = message.ReplicaId;
            var childSecretShare = message.SecreShare;

            if (Crypto.GetHash(childSecretShare) != childSecretHashes[childReplicaId])
            {
                return;
            }

            if (verifiedChildShareSecrets.TryAdd(childReplicaId, childSecretShare) == false)
            {
                return;
            }

            if (verifiedChildShareSecrets.Count != primaryReplica.ChildReplicas.Count)
            {
                return;
            }

            if (verifiedChildShareSecrets.Keys.OrderBy(_ => _)
                    .SequenceEqual(primaryReplica.ChildReplicas.Select(r => r.Id).OrderBy(_ => _)) == false)
            {
                return;
            }

            var verifiedSecretShares = verifiedChildShareSecrets
                .Select(x => new { ReplicaId = x.Key, SecretShare = x.Value })
                .OrderBy(x => x.ReplicaId)
                .Select(x => x.SecretShare)
                .ToList();

            var secret = string.Join(string.Empty, verifiedSecretShares);

            uint secretHash;
            uint counter;
            uint viewNumber;

            primaryReplica.Tee.GetHashAndCounterViewNumber(
                primaryReplica.PublicKey,
                signedSecretHashAndCounterViewNumber,
                out secretHash,
                out counter,
                out viewNumber);

            if (Crypto.GetHash(secret) != secretHash)
            {
                return;
            }

            var request = string.Join(string.Empty, block);
            var commitResult = block.Sum();
            var commitResultHash = Crypto.GetHash(request + commitResult);

            var signedCommitResultHashCounterViewNumber = primaryReplica.Tee.RequestCounter(commitResultHash);

            if (!isCommitted)
            {
                Log("All ready to commit.");

                Network.EmulateLatency();

                foreach (var activeRelica in activeRelicas)
                {
                    activeRelica.SendMessage(new CommitMessage
                    {
                        Secret = secret,
                        CommitResult = commitResult,
                        CommitResultHashCounterViewNumber = signedCommitResultHashCounterViewNumber
                    });
                }

                isCommitted = true;
            }
        }
    }
}
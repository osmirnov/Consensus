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
    public class PrimarySecretShareHandler : Handler
    {
        private static Random rnd = new Random(Environment.TickCount);

        public static int MinTimeToAddBlockIntoBlockchain = 10;
        public static int MaxTimeToAddBlockIntoBlockchain = 100;

        public static void Handle(
            SecretShareMessage message,
            PrimaryReplica primaryReplica,
            IEnumerable<ReplicaBase> activeRelicas,
            int[] block,
            ICollection<int[]> blockchain,
            ref bool isCommitted,
            ref bool hasConsensus,
            byte[] signedSecretHashAndCounterViewNumber,
            ConcurrentDictionary<int, string> verifiedChildShareSecrets)
        {
            var childReplicaId = message.ReplicaId;
            var childrenSecretShares = message.ReplicaSecretShares;

            Log(primaryReplica, "ChildReplicaId: {0}, ChildrenSecretShare: [{1}]", childReplicaId, string.Join(",", childrenSecretShares.Select(ridssh => $"{{{ridssh.Key}:{ridssh.Value}}}")));

            foreach (var childReplicaShare in childrenSecretShares)
            {
                // TODO: hashes of primary children should be checked as well
                verifiedChildShareSecrets.TryAdd(childReplicaShare.Key, childReplicaShare.Value);
            }

            if (verifiedChildShareSecrets.Keys.OrderBy(_ => _)
                    .SequenceEqual(activeRelicas.Select(r => r.Id).OrderBy(_ => _)) == false)
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

            if (Crypto.GetHash(secret + counter + viewNumber) != secretHash)
            {
                Log(primaryReplica, "Send RequestViewChangeMessage to all active replicas.");
                throw new InvalidOperationException("Invalid secret hash.");
            }

            if (!isCommitted)
            {
                verifiedChildShareSecrets.Clear();

                Thread.Sleep(rnd.Next(MinTimeToAddBlockIntoBlockchain, MaxTimeToAddBlockIntoBlockchain));

                blockchain.Add(block);

                var request = string.Join(string.Empty, block);
                var commitResult = blockchain.Count;
                var commitResultHash = Crypto.GetHash(request) | (uint)commitResult;

                var signedCommitResultHashCounterViewNumber = primaryReplica.Tee.RequestCounter(commitResultHash);

                Log(primaryReplica, "Broadcast a committed block.");

                // the block was added on primary replica to blockchain -> we need to sync this across replicas
                Network.EmulateLatency();

                foreach (var activeRelica in activeRelicas)
                {
                    activeRelica.SendMessage(new CommitMessage
                    {
                        ReplicaSecretIndex = blockchain.Count * 2 - 1,
                        Secret = secret,
                        CommitResult = commitResult,
                        CommitResultHashCounterViewNumber = signedCommitResultHashCounterViewNumber
                    });
                }

                isCommitted = true;
            }
            else
            {
                hasConsensus = true;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class CommitHandler : Handler
    {
        private static Random rnd = new Random(Environment.TickCount);

        public static int MinTimeToAddBlockIntoBlockchain = 10;
        public static int MaxTimeToAddBlockIntoBlockchain = 100;

        public static void Handle(
            CommitMessage message,
            Replica replica,
            uint secretHash,
            int[] block,
            ICollection<int[]> blockchain,
            byte[] encryptedReplicaSecret,
            out string nextSecretShare,
            out Dictionary<int, uint> nextChildSecretHashes,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            if (Crypto.GetHash(message.Secret + replica.Tee.LatestCounter + replica.Tee.ViewNumber) != secretHash)
            {
                Log(replica, "Send RequestViewChangeMessage to all active replicas.");
                throw new InvalidOperationException("Invalid secret hash.");
            }

            // add the same block as a primary replica
            Thread.Sleep(rnd.Next(MinTimeToAddBlockIntoBlockchain, MaxTimeToAddBlockIntoBlockchain));

            blockchain.Add(block);

            var commitResult = blockchain.Count;

            if (message.CommitResult != commitResult)
            {
                Log(replica, "Send RequestViewChangeMessage to all active replicas.");
                throw new InvalidOperationException("Inconsitent commit result.");
            }

            uint nextSecretHash;

            replica.Tee.VerifyCounter(
                replica.PrimaryReplica.PublicKey,
                message.CommitResultHashCounterViewNumber,
                encryptedReplicaSecret,
                out nextSecretShare,
                out nextChildSecretHashes,
                out nextSecretHash);

            if (replica.ChildReplicas.Any())
            {
                foreach (var childReplica in replica.ChildReplicas)
                {
                    var tokenSource = new CancellationTokenSource();

                    Task.Delay(5000, tokenSource.Token)
                        .ContinueWith(t =>
                        {
                            if (!t.IsCanceled)
                            {
                                // we send message about a suspected replica to the primary replica
                                Network.EmulateLatency();

                                replica.PrimaryReplica.SendMessage(
                                    new SuspectMessage
                                    {
                                        ReplicaId = childReplica.Id
                                    });
                            }
                        });

                    secretShareMessageTokenSources.Add(childReplica.Id, tokenSource);
                }
            }
            else
            {
                // we send a message with a secret share to the parent replica
                Network.EmulateLatency();

                replica.ParentReplica.SendMessage(
                    new SecretShareMessage
                    {
                        ReplicaId = replica.Id,
                        ReplicaSecretShares = new Dictionary<int, string>() { { replica.Id, nextSecretShare } }
                    });

                Log(replica, "Send a secret share to the parent replica (ParentReplicaId: {0})", replica.ParentReplica.Id);
            }
        }
    }
}

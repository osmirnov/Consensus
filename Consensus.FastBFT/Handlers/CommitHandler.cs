using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class CommitHandler
    {
        public static void Handle(
            CommitMessage message,
            Replica replica,
            uint secretHash,
            int[] block,
            byte[] encryptedReplicaSecret,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            if (Crypto.GetHash(message.Secret) == secretHash)
            {
                // perform the same op as a primary replica
                var commitResult = block.Sum();

                if (message.CommitResult == commitResult)
                {
                    string nextSecretShare;
                    Dictionary<int, uint> nextChildrenSecretHashes;
                    uint nextSecretHash;

                    replica.Tee.VerifyCounter(
                        replica.PrimaryReplica.PublicKey,
                        message.CommitResultHashCounterViewNumber,
                        encryptedReplicaSecret,
                        out nextSecretShare,
                        out nextChildrenSecretHashes,
                        out nextSecretHash);

                    if (replica.ChildReplicas.Any())
                    {
                        foreach (var childReplica in replica.ChildReplicas)
                        {
                            var tokenSource = new CancellationTokenSource();

                            Task.Delay(5000, tokenSource.Token)
                                .ContinueWith(t =>
                                {
                                    if (t.IsCompleted)
                                    {
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
                        replica.ParentReplica.SendMessage(
                            new SecretShareMessage
                            {
                                ReplicaId = replica.ParentReplica.Id,
                                SecreShare = nextSecretShare
                            });
                    }
                }
            }
        }
    }
}

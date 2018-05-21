using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class CommitHandler
    {
        public static void Handle(
            CommitMessage message,
            Replica replica,
            PrimaryReplica primaryReplica,
            uint secretHash,
            int[] block,
            byte[] encryptedReplicaSecret,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            if (replica.Tee.Crypto.GetHash(message.Secret) == secretHash)
            {
                // perform the same op as a primary replica
                var commitResult = block.Sum();

                if (message.CommitResult == commitResult)
                {
                    string nextSecretShare;
                    Dictionary<int, uint> nextChildrenSecretHashes;
                    uint nextSecretHash;

                    replica.Tee.VerifyCounter(
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
                                        primaryReplica.SendMessage(
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

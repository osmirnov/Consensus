using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Handlers
{
    public class PrepareHandler
    {
        public static void Handle(
            PrepareMessage message,
            Tee tee,
            byte[] replicaSecret,
            Dictionary<int, uint> childSecretHashes,
            Replica replica,
            out int[] block,
            out string secretShare,
            out uint secretHash,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            block = message.Block;
            var requestCounterViewNumber = message.RequestCounterViewNumber;

            Dictionary<int, uint> childrenSecretHashes;

            tee.VerifyCounter(
                replica.PrimaryReplica.Tee.PublicKey,
                requestCounterViewNumber,
                replicaSecret,
                out secretShare,
                out childrenSecretHashes,
                out secretHash);

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
                        ReplicaId = replica.Id,
                        SecreShare = secretShare
                    });
            }
        }
    }
}

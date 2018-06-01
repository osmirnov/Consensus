using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class PrepareHandler
    {
        public static void Handle(
            PrepareMessage message,
            Replica replica,
            byte[] replicaSecret,
            out int[] block,
            out string secretShare,
            out Dictionary<int, uint> childSecretHashes,
            out uint secretHash,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            block = message.Block;
            var requestCounterViewNumber = message.RequestCounterViewNumber;

            replica.Tee.VerifyCounter(
                replica.PrimaryReplica.PublicKey,
                requestCounterViewNumber,
                replicaSecret,
                out secretShare,
                out childSecretHashes,
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
                        ReplicaSecretShares = new Dictionary<int, string> { { replica.Id, secretShare } }
                    });
            }
        }
    }
}

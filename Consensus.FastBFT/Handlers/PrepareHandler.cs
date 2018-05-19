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
            PrimaryReplica primaryReplica,
            int replicaId,
            Replica parentReplica,
            IEnumerable<int> childReplicaIds,
            string replicaSecretShare,
            Dictionary<int, CancellationTokenSource> secretShareMessageTokenSources)
        {
            var requestCounterViewNumber = message.RequestCounterViewNumber;

            string secretShare;
            Dictionary<int, uint> childrenSecretHashes;
            uint secretHash;

            tee.VerifyCounter(
                requestCounterViewNumber,
                replicaSecret,
                out secretShare,
                out childrenSecretHashes,
                out secretHash);

            if (childReplicaIds.Any())
            {
                secretShareMessageTokenSources = childReplicaIds
                    .ToDictionary(
                        rid => rid,
                        rid =>
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
                                                ReplicaId = rid
                                            });
                                    }
                                });

                            return tokenSource;
                        });
            }
            else
            {
                parentReplica.SendMessage(
                    new SecretShareMessage
                    {
                        ReplicaId = replicaId,
                        SecreShare = secretShare
                    });
            }
        }
    }
}

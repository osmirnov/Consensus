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
            ConcurrentDictionary<int, byte[]> allReplicaSecrets,
            ConcurrentDictionary<int, Dictionary<int, uint>> allChildrenSecretHashes,
            PrimaryReplica primaryReplica,
            int replicaId,
            Replica parentReplica,
            IEnumerable<int> childReplicaIds,
            ConcurrentDictionary<int, string> allReplicaSecretShares,
            ConcurrentDictionary<int, Dictionary<int, CancellationTokenSource>> allSecretShareMessageTokenSources)
        {
            byte[] replicaSecret;
            if (allReplicaSecrets.TryGetValue(message.CorrelationId, out replicaSecret) == false)
            {
                return;
            }

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
                var secretShareMessageTokenSources = childReplicaIds
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
                                                CorrelationId = message.CorrelationId,
                                                ReplicaId = rid
                                            });
                                    }
                                });

                            return tokenSource;
                        });

                allSecretShareMessageTokenSources.TryAdd(message.CorrelationId, secretShareMessageTokenSources);
            }
            else
            {
                parentReplica.SendMessage(
                    new SecretShareMessage
                    {
                        CorrelationId = message.CorrelationId,
                        ReplicaId = replicaId,
                        SecreShare = secretShare
                    });
            }
        }
    }
}

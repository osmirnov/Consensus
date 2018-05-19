using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    public class PrimaryReplica : Replica
    {
        ConcurrentQueue<int> transactionBuffer = new ConcurrentQueue<int>();

        public new PrimaryTee tee;
        private Replica[] activeReplicas = new Replica[0];

        public void Run(CancellationToken cancellationToken)
        {
            activeReplicas = tee.GetReplicas(this);
            var blockExchange = new ConcurrentQueue<int[]>();

            // process transactions
            Task.Factory.StartNew(() =>
            {
                var block = new List<int>(TransactionHandler.MinTransactionsCountInBlock);

                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (messageBus.TryPeek(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var transactionMessage = message as TransactionMessage;
                    if (transactionMessage != null)
                    {
                        TransactionHandler.Handle(transactionMessage, block, blockExchange);
                        messageBus.TryDequeue(out message);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var isSecretDistributed = false;

            // process blocks
            Task.Factory.StartNew(() =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (!isSecretDistributed)
                    {
                        DistributeSecret();
                        isSecretDistributed = true;
                    }

                    int[] block;
                    if (blockExchange.TryDequeue(out block) == false)
                    {
                        Thread.Sleep(5000);
                        continue;
                    }

                    InitiateConsesusProcess(block);
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // handle replicas communication
            Task.Factory.StartNew(() =>
            {
                var replicaSecretShare = string.Empty;
                var childSecretHashes = new Dictionary<int, uint>();
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();
                var secretShareMessageTokenSources = new Dictionary<int, CancellationTokenSource>();

                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (messageBus.TryPeek(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        PrimarySecretShareHandler.Handle(
                            secretShareMessage,
                            tee,
                            this,
                            replicaSecretShare,
                            childSecretHashes,
                            secretShareMessageTokenSources,
                            verifiedChildShareSecrets);
                        messageBus.TryDequeue(out message);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void DistributeSecret()
        {
            // preprocessing is designed to issue many secrets per request
            // we assume we have merged transactions in a block to request a single secret 
            var signedSecretHashAndEncryptedReplicaSecrets = tee.Preprocessing(1).First();
            var encryptedReplicaSecrets = signedSecretHashAndEncryptedReplicaSecrets.Value;

            // we distribute secret shares among active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var encryptedReplicaSecret in encryptedReplicaSecrets)
            {
                var replicaId = encryptedReplicaSecret.Key;

                activeReplicas[replicaId].SendMessage(new PreprocessingMessage
                {
                    ReplicaSecret = encryptedReplicaSecret.Value
                });
            }
        }

        private void InitiateConsesusProcess(int[] block)
        {
            // signed block request
            var message = string.Join(string.Empty, block.Select(tx => tx.ToString()));
            var signedRequestCounterViewNumber = tee.RequestCounter(tee.Crypto.GetHash(message));

            // we start preparation for request handling on active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var secondaryReplica in activeReplicas)
            {
                secondaryReplica.SendMessage(new PrepareMessage
                {
                    RequestCounterViewNumber = signedRequestCounterViewNumber
                });
            }
        }
    }
}

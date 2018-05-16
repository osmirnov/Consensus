using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    class PrimaryReplica : Replica
    {
        const int minTransactionsCountInBlock = 10;

        ConcurrentQueue<int> transactionBuffer = new ConcurrentQueue<int>();

        public new PrimaryTee tee;
        private Replica[] secondaryReplicas = new Replica[0];

        public void Run(CancellationToken cancellationToken)
        {
            secondaryReplicas = tee.GetReplicas(this);
            var blockBuffer = new ConcurrentQueue<int[]>();

            // process transactions
            Task.Factory.StartNew(() =>
            {
                var block = new List<int>(minTransactionsCountInBlock);

                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (messageBus.TryDequeue(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var transactionMessage = message as TransactionMessage;
                    if (transactionMessage != null)
                    {
                        var transaction = transactionMessage.Transaction;

                        lock (block)
                        {
                            block.Add(transaction);
                        }

                        if (block.Count >= minTransactionsCountInBlock)
                        {
                            var blockCopy = block.ToArray();

                            lock (block)
                            {
                                block.Clear();
                            }

                            // publish block to start working on consensus
                            blockBuffer.Enqueue(blockCopy);
                        }

                        continue;
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // handle replicas communication
            Task.Factory.StartNew(() =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // process blocks
            Task.Factory.StartNew(() =>
            {
                var isSecretDistributed = false;

                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (!isSecretDistributed)
                    {
                        DistributeSecret();
                    }

                    int[] block;
                    if (blockBuffer.TryDequeue(out block) == false) continue;

                    HandleClientRequest(block);

                    GetConsensusOnBlock(block);
                    AddBlockIntoChain(block);

                    isSecretDistributed = false;
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void GetConsensusOnBlock(int[] block)
        {
            throw new NotImplementedException();
        }

        private void AddBlockIntoChain(int[] block)
        {
            throw new NotImplementedException();
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

                secondaryReplicas[replicaId].SendMessage(new PreprocessingMessage
                {
                    ReplicaSecret = encryptedReplicaSecret.Value
                });
            }
        }

        private void HandleClientRequest(int[] block)
        {
            // signed client request
            var message = string.Join(string.Empty, block.Select(tx => tx.ToString()));
            var signedRequestCounterViewNumber = tee.RequestCounter(tee.Crypto.GetHash(message));

            // we start preparation for request handling on active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var secondaryReplica in secondaryReplicas)
            {
                secondaryReplica.SendMessage(new PrepareMessage
                {
                    RequestCounterViewNumber = signedRequestCounterViewNumber
                });
            }
        }
    }
}

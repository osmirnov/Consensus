using System;
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
    public class PrimaryReplica : ReplicaBase
    {
        private readonly List<int[]> blockchain = new List<int[]>();

        public PrimaryTee Tee { get; }

        public PrimaryReplica(int id) : base(id)
        {
            Tee = new PrimaryTee(PrivateKey, PublicKey);
        }

        public void Run(IEnumerable<ReplicaBase> activeReplicas, CancellationToken cancellationToken)
        {
            // bootstrapping
            // if the current replica has missing blocks in it blockchain
            // it must process blocks that were created after and
            // majority of active replicas agreed upon
            var aheadBlocks = new List<int[]>();

            ReplicaTopology.Discover(this, activeReplicas);

            var replicaTree = ReplicaTopology.GetReplicaTree(this);
            var aheadBlocksOrTree = (aheadBlocks.LastOrDefault() ?? new int[0]).Sum() | replicaTree.Sum();
            var signedAheadBlocksOrTreeHashAndCounterViewNumber = Tee.RequestCounter(Crypto.GetHash(aheadBlocksOrTree.ToString()));
            var encryptedViewKeys = Tee.BePrimary(activeReplicas);

            SyncReplicas(
                aheadBlocks,
                replicaTree,
                signedAheadBlocksOrTreeHashAndCounterViewNumber,
                encryptedViewKeys,
                activeReplicas);

            Log("All replicas in sync with latest counter and view number");

            // process transactions
            var blockExchange = new ConcurrentQueue<int[]>();

            Task.Factory.StartNew(() =>
            {
                var newBlock = new List<int>();

                Log("Running transaction listening...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = PeekMessage();
                    if (message == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var transactionMessage = message as TransactionMessage;
                    if (transactionMessage != null)
                    {
                        TransactionHandler.Handle(transactionMessage, newBlock, blockExchange);
                        ReceiveMessage();
                    }
                }

                Log("Stopped transaction listening.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var isSecretDistributed = false;
            var signedSecretHashAndCounterViewNumber = new byte[0];
            var consensusBlock = default(int[]);

            // process blocks
            Task.Factory.StartNew(() =>
            {
                Log("Running block aggregation...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (!isSecretDistributed)
                    {
                        DistributeSecret(activeReplicas, out signedSecretHashAndCounterViewNumber);
                        isSecretDistributed = true;

                        Log("All secret shares are distributed among replicas");
                    }

                    if (consensusBlock == null)
                    {
                        if (blockExchange.TryDequeue(out consensusBlock) == false)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        InitiateConsesusProcess(activeReplicas, consensusBlock);
                        Log($"The consenus was started on block #{string.Join(string.Empty, consensusBlock)}");
                        continue;
                    }

                    Thread.Sleep(5000);
                }

                Log("Stopped block aggregation.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // handle replicas communication
            Task.Factory.StartNew(() =>
            {
                var isCommitted = false;
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();

                Log("Running message exchange...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = PeekMessage();
                    if (message == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        Log("Received SecretShareMessage");

                        PrimarySecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            activeReplicas,
                            consensusBlock,
                            blockchain.Count,
                            ref isCommitted,
                            signedSecretHashAndCounterViewNumber,
                            verifiedChildShareSecrets);

                        if (isCommitted)
                        {
                            blockchain.Add(consensusBlock);
                            consensusBlock = null;
                            Log($"The consensus was reached on block #{string.Join(string.Empty, consensusBlock)}");
                        }

                        ReceiveMessage();
                    }

                    var suspectMessage = message as SuspectMessage;
                    if (suspectMessage != null)
                    {
                        Log("Received SuspectMessage (SourceReplicaId: {0})", suspectMessage.ReplicaId);

                        ReceiveMessage();
                    }
                }

                    Log("Stopped message exchange.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private static void SyncReplicas(
            IEnumerable<int[]> aheadBlocks,
            IEnumerable<int> replicaTree,
            byte[] signedAheadBlocksOrTreeHashAndCounterViewNumber,
            IReadOnlyDictionary<int, string> encryptedViewKeys,
            IEnumerable<ReplicaBase> activeReplicas)
        {
            foreach (var activeReplica in activeReplicas)
            {
                activeReplica.SendMessage(new NewViewMessage
                {
                    AheadBlocks = aheadBlocks,
                    ReplicaTree = replicaTree,
                    SignedAheadBlocksOrTreeHashAndCounterViewNumber = signedAheadBlocksOrTreeHashAndCounterViewNumber,
                    EncryptedViewKey = encryptedViewKeys[activeReplica.Id]
                });
            }

            // we will wait for 1 second to make sure all replicas in sync
            Thread.Sleep(1000);
        }

        private void DistributeSecret(IEnumerable<ReplicaBase> activeReplicas, out byte[] signedSecretHashAndCounterViewNumber)
        {
            // preprocessing is designed to issue many secrets per request
            // we assume we have merged transactions in a block to request a single secret 
            var signedSecretHashAndEncryptedReplicaSecrets = Tee.Preprocessing(1).First();
            signedSecretHashAndCounterViewNumber = signedSecretHashAndEncryptedReplicaSecrets.Key;
            var encryptedReplicaSecrets = signedSecretHashAndEncryptedReplicaSecrets.Value;

            // we distribute secret shares among active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var encryptedReplicaSecret in encryptedReplicaSecrets)
            {
                var replicaId = encryptedReplicaSecret.Key;
                var activeReplica = activeReplicas.SingleOrDefault(r => r.Id == replicaId);

                activeReplica?.SendMessage(new PreprocessingMessage
                {
                    ReplicaSecret = encryptedReplicaSecret.Value
                });
            }
        }

        private void InitiateConsesusProcess(IEnumerable<ReplicaBase> activeReplicas, int[] block)
        {
            // signed block request
            var request = string.Join(string.Empty, block);
            var signedRequestCounterViewNumber = Tee.RequestCounter(Crypto.GetHash(request));

            // we start preparation for request handling on active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var secondaryReplica in activeReplicas)
            {
                secondaryReplica.SendMessage(new PrepareMessage
                {
                    Block = block,
                    RequestCounterViewNumber = signedRequestCounterViewNumber
                });
            }
        }

        private void Log(string message, params object[] args)
        {
            Console.WriteLine($"Primary Replica #{Id}: {string.Format(message, args)}");
        }
    }
}

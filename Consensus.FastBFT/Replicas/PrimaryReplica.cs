using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ConcurrentQueue<TransactionMessage> transactionBus = new ConcurrentQueue<TransactionMessage>();

        public PrimaryTee Tee { get; }

        public Action<ConsensusResult> OnConsensusReached;

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

            LogReplicaTopology(this);

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
                var newBlock = new ConcurrentBag<int>();

                Log("Running transaction listening...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    TransactionMessage transactionMessage;
                    if (transactionBus.TryDequeue(out transactionMessage) == false)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    TransactionHandler.Handle(transactionMessage, this, ref newBlock, blockExchange);
                }

                Log("Stopped transaction listening.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            var isSecretDistributed = false;
            var signedSecretHashesAndCounterViewNumber = new List<byte[]>(0);
            var consensusBlock = default(int[]);
            var consensusStartedAt = DateTime.Now;

            // process blocks
            Task.Factory.StartNew(() =>
            {
                Log("Running block aggregation...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (!isSecretDistributed)
                    {
                        DistributeSecret(activeReplicas, out signedSecretHashesAndCounterViewNumber);
                        isSecretDistributed = true;

                        Log("All secret shares are distributed among replicas");
                    }

                    if (consensusBlock == null)
                    {
                        // get a block of transactions to agree upon to be added to blockchain
                        if (blockExchange.TryDequeue(out consensusBlock) == false)
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        consensusStartedAt = DateTime.Now;

                        InitiateConsesusProcess(activeReplicas, consensusBlock);
                        Log($"The consenus was started on block #{Blockchain.Count} (TransactionsCount: {consensusBlock.Length}, StartedAt: {consensusStartedAt}).");
                        continue;
                    }

                    Thread.Sleep(500);
                }

                Log("Stopped block aggregation.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // handle replicas communication
            Task.Factory.StartNew(() =>
            {
                var isCommitted = false;
                var hasConsensus = false;
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();

                Log("Running message exchange...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = PeekMessage();
                    if (message == null)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        Log("Received SecretShareMessage (SourceReplicaId: {0})", secretShareMessage.ReplicaId);

                        var blockchainLength = Blockchain.Count;

                        PrimarySecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            activeReplicas,
                            consensusBlock,
                            Blockchain,
                            ref isCommitted,
                            ref hasConsensus,
                            !isCommitted ? signedSecretHashesAndCounterViewNumber.First() : signedSecretHashesAndCounterViewNumber.Last(),
                            verifiedChildShareSecrets);

                        if (hasConsensus)
                        {
                            var consensusReachedAt = DateTime.Now;

                            Log($"The consensus was reached on #{Blockchain.Count - 1} (TransactionsCount: {consensusBlock.Length}, ReachedAt: {consensusReachedAt}, LastedDue: {consensusReachedAt - consensusStartedAt}).");

                            OnConsensusReached(new ConsensusResult
                            {
                                StartedAt = consensusStartedAt,
                                ReachedAt = consensusReachedAt,
                                BlockNumber = Blockchain.Count - 1,
                                TransactionsCount = consensusBlock.Length
                            });

                            isSecretDistributed = false;
                            consensusBlock = null;
                            isCommitted = false;
                            hasConsensus = false;
                            verifiedChildShareSecrets.Clear();
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

        public void SendTransaction(int transaction)
        {
            transactionBus.Enqueue(new TransactionMessage
            {
                Transaction = transaction
            });
        }

        [Conditional("DEBUG")]
        private void LogReplicaTopology(ReplicaBase replica)
        {
            Log("ParentReplicaId: {0}, ChildReplicaIds: [{1}]", replica.Id, string.Join(",", replica.ChildReplicas.Select(r => r.Id)));

            foreach (var childReplica in replica.ChildReplicas)
            {
                LogReplicaTopology(childReplica);
            }
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
            Thread.Sleep(500 * activeReplicas.Count());
        }

        private void DistributeSecret(IEnumerable<ReplicaBase> activeReplicas, out List<byte[]> signedSecretHashesAndCounterViewNumbers)
        {
            // preprocessing is designed to issue many secrets per request
            // we assume we have merged transactions in a block to request a single secret
            // and also we need an extra one for reply phase 
            var signedSecretHashesAndEncryptedReplicaSecrets = Tee.Preprocessing(2);
            signedSecretHashesAndCounterViewNumbers = signedSecretHashesAndEncryptedReplicaSecrets.Select(x => x.Key).ToList();
            var allEncryptedReplicaSecrets = signedSecretHashesAndEncryptedReplicaSecrets.Select(x => x.Value).ToList();

            // we distribute secret shares among active replicas
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            for (var i = 0; i < allEncryptedReplicaSecrets.Count; i++)
            {
                var encryptedReplicaSecrets = allEncryptedReplicaSecrets[i];

                foreach (var encryptedReplicaSecret in encryptedReplicaSecrets)
                {
                    var replicaId = encryptedReplicaSecret.Key;
                    var activeReplica = activeReplicas.SingleOrDefault(r => r.Id == replicaId);

                    activeReplica?.SendMessage(new PreprocessingMessage
                    {
                        ReplicaSecretIndex = i,
                        ReplicaSecret = encryptedReplicaSecret.Value
                    });
                }
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

        [Conditional("DEBUG")]
        private void Log(string message, params object[] args)
        {
            Console.WriteLine($"Primary Replica #{Id}: {string.Format(message, args)}");
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    public class Replica : ReplicaBase
    {
        public Tee Tee { get; }
        public PrimaryReplica PrimaryReplica { get; set; }

        public Replica(int id, bool isActive) : base(id)
        {
            Tee = new Tee(PrivateKey, PublicKey) { IsActive = isActive };
        }

        public void Run(Replica[] activeReplicas, CancellationToken cancellationToken)
        {
            // process messages
            Task.Factory.StartNew(() =>
            {
                var signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber = new byte[0];
                var encryptedViewKey = string.Empty;
                var viewChangesCount = new ConcurrentDictionary<int, int>();

                var allReplicaSecrets = new Dictionary<int, byte[]>();
                var allBlocks = new ConcurrentDictionary<int, int[]>();
                var allReplicaSecretShares = new ConcurrentDictionary<int, string>();
                var allSecretHashes = new ConcurrentDictionary<int, uint>();
                var allChildSecretHashes = new ConcurrentDictionary<int, Dictionary<int, uint>>();
                var allVerifiedChildShareSecrets = new ConcurrentDictionary<int, ConcurrentDictionary<int, string>>();
                var allSecretShareMessageTokenSources = new ConcurrentDictionary<int, Dictionary<int, CancellationTokenSource>>();

                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = ReceiveMessage();
                    if (message == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (Tee.ViewKey == 0)
                    {
                        var newViewMessage = message as NewViewMessage;
                        if (newViewMessage != null)
                        {
                            Log("Received NewViewMessage");

                            NewViewHandler.Handle(
                                newViewMessage,
                                this,
                                activeReplicas,
                                out signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber,
                                out encryptedViewKey);
                        }

                        var viewChangeMessage = message as ViewChangeMessage;
                        if (viewChangeMessage != null)
                        {
                            Log("Received ViewChangeMessage (SourceReplicaId: {0})", viewChangeMessage.ReplicaId);

                            ViewChangeHandler.Handle(
                                viewChangeMessage,
                                this,
                                activeReplicas,
                                viewChangesCount,
                                signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber,
                                encryptedViewKey);
                        }

                        continue;
                    }

                    var preprocessingMessage = message as PreprocessingMessage;
                    if (preprocessingMessage != null)
                    {
                        Log("Received PreprocessingMessage");

                        PreprocessingHandler.Handle(preprocessingMessage, allReplicaSecrets);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        Log("Received PrepareMessage");

                        int[] block;
                        string replicaSecretShare;
                        Dictionary<int, uint> childSecretHashes;
                        uint secretHash;

                        var replicaSecretIndex = prepareMessage.ReplicaSecretIndex;

                        PrepareHandler.Handle(
                            prepareMessage,
                            this,
                            allReplicaSecrets[replicaSecretIndex],
                            out block,
                            out replicaSecretShare,
                            out childSecretHashes,
                            out secretHash,
                            allSecretShareMessageTokenSources.GetOrAdd(replicaSecretIndex, new Dictionary<int, CancellationTokenSource>())
                        );

                        if (allBlocks.TryAdd(replicaSecretIndex, block) == false)
                        {
                            throw new InvalidOperationException();
                        }

                        if (allReplicaSecretShares.TryAdd(replicaSecretIndex, replicaSecretShare) == false)
                        {
                            throw new InvalidOperationException();
                        }

                        if (allChildSecretHashes.TryAdd(replicaSecretIndex, childSecretHashes) == false)
                        {
                            throw new InvalidOperationException();
                        }

                        if (allSecretHashes.TryAdd(replicaSecretIndex, secretHash) == false)
                        {
                            throw new InvalidOperationException();
                        }
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        Log("Received SecretShareMessage (SourceReplicaId: {0})", secretShareMessage.ReplicaId);

                        var replicaSecretIndex = secretShareMessage.ReplicaSecretIndex;

                        SecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            allReplicaSecretShares[replicaSecretIndex],
                            allChildSecretHashes[replicaSecretIndex],
                            allSecretShareMessageTokenSources[replicaSecretIndex],
                            allVerifiedChildShareSecrets.GetOrAdd(replicaSecretIndex, new ConcurrentDictionary<int, string>()));
                    }

                    var commitMessage = message as CommitMessage;
                    if (commitMessage != null)
                    {
                        Log("Received CommitMessage");

                        var nextReplicaSecretIndex = commitMessage.NextReplicaSecretIndex;
                        var replicaSecretIndex = nextReplicaSecretIndex - 1;

                        string nextReplicaSecretShare;
                        Dictionary<int, uint> nextChildSecretHashes;

                        CommitHandler.Handle(
                            commitMessage,
                            this,
                            allSecretHashes[replicaSecretIndex],
                            allBlocks[replicaSecretIndex],
                            Blockchain,
                            allReplicaSecrets[nextReplicaSecretIndex],
                            out nextReplicaSecretShare,
                            out nextChildSecretHashes,
                            allSecretShareMessageTokenSources.GetOrAdd(nextReplicaSecretIndex, new Dictionary<int, CancellationTokenSource>())
                        );

                        if (allReplicaSecretShares.TryAdd(nextReplicaSecretIndex, nextReplicaSecretShare) == false)
                        {
                            throw new InvalidOperationException();
                        }

                        if (allChildSecretHashes.TryAdd(nextReplicaSecretIndex, nextChildSecretHashes) == false)
                        {
                            throw new InvalidOperationException();
                        }
                    }
                }

                Log("Stopped.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        [Conditional("DEBUG")]
        private void Log(string message, params object[] args)
        {
            Console.WriteLine($"Replica #{Id}: {string.Format(message, args)}");
        }
    }
}

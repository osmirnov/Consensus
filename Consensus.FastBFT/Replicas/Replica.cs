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

                var replicaSecrets = new Dictionary<int, byte[]>(2);
                var block = new int[0];
                var replicaSecretShare = string.Empty;
                var secretHash = default(uint);
                var childSecretHashes = new Dictionary<int, uint>();
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();
                var secretShareMessageTokenSources = new Dictionary<int, CancellationTokenSource>();

                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = ReceiveMessage();
                    if (message == null)
                    {
                        Thread.Sleep(10);
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

                        childSecretHashes.Clear();
                        secretShareMessageTokenSources.Clear();
                        verifiedChildShareSecrets.Clear();

                        PreprocessingHandler.Handle(preprocessingMessage, replicaSecrets);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        Log("Received PrepareMessage");

                        PrepareHandler.Handle(
                            prepareMessage,
                            this,
                            replicaSecrets[0],
                            out block,
                            out replicaSecretShare,
                            out childSecretHashes,
                            out secretHash,
                            secretShareMessageTokenSources
                        );
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        Log("Received SecretShareMessage (SourceReplicaId: {0})", secretShareMessage.ReplicaId);

                        SecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            replicaSecretShare,
                            childSecretHashes,
                            secretShareMessageTokenSources,
                            verifiedChildShareSecrets);
                    }

                    var commitMessage = message as CommitMessage;
                    if (commitMessage != null)
                    {
                        Log("Received CommitMessage");

                        childSecretHashes.Clear();
                        secretShareMessageTokenSources.Clear();
                        verifiedChildShareSecrets.Clear();

                        CommitHandler.Handle(
                            commitMessage,
                            this,
                            secretHash,
                            block,
                            Blockchain,
                            replicaSecrets[1],
                            out replicaSecretShare,
                            out childSecretHashes,
                            secretShareMessageTokenSources);

                        replicaSecrets.Clear();
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

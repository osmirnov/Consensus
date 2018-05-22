using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    public class Replica
    {
        protected ConcurrentQueue<Message> MessageBus = new ConcurrentQueue<Message>();

        public int Id { get; set; }
        public Tee Tee { get; set; }
        public Replica PrimaryReplica { get; set; }
        public Replica ParentReplica { get; set; }
        public IList<Replica> ChildReplicas { get; set; } = new List<Replica>(2);


        public Replica()
        {
            PrivateKey = Guid.NewGuid().ToString("N");
            PublicKey = PrivateKey;
        }

        public string PublicKey { get; }

        public string PrivateKey { get; }

        public void Run(CancellationToken cancellationToken)
        {
            // process messages
            Task.Factory.StartNew(() =>
            {
                var replicaSecret = new byte[0];
                var block = new int[0];
                var replicaSecretShare = string.Empty;
                var secretHash = default(uint);
                var childSecretHashes = new Dictionary<int, uint>();
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();
                var secretShareMessageTokenSources = new Dictionary<int, CancellationTokenSource>();

                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (MessageBus.TryPeek(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var preprocessingMessage = message as PreprocessingMessage;
                    if (preprocessingMessage != null)
                    {
                        PreprocessingHandler.Handle(preprocessingMessage, out replicaSecret);
                        MessageBus.TryDequeue(out message);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        PrepareHandler.Handle(
                            prepareMessage,
                            Tee,
                            replicaSecret,
                            childSecretHashes,
                            this,
                            out block,
                            out replicaSecretShare,
                            out secretHash,
                            secretShareMessageTokenSources
                        );
                        MessageBus.TryDequeue(out message);
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        SecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            replicaSecretShare,
                            childSecretHashes,
                            secretShareMessageTokenSources,
                            verifiedChildShareSecrets);
                        MessageBus.TryDequeue(out message);
                    }

                    var commitMessage = message as CommitMessage;
                    if (commitMessage != null)
                    {
                        CommitHandler.Handle(
                            commitMessage,
                            this,
                            secretHash,
                            block,
                            replicaSecret,
                            secretShareMessageTokenSources);
                        MessageBus.TryDequeue(out message);
                    }
                }

                Log("Stopped.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SendMessage(Message message)
        {
            MessageBus.Enqueue(message);
        }

        private void Log(string message)
        {
            Console.WriteLine($"Replica #{Id}: {message}");
        }
    }
}

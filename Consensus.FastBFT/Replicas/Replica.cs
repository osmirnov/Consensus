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
        private PrimaryReplica primaryReplica;

        protected ConcurrentQueue<Message> MessageBus = new ConcurrentQueue<Message>();

        public int Id;
        public Tee Tee;
        public Replica ParentReplica;
        public IList<Replica> ChildReplicas = new List<Replica>(2);

        public void Run(PrimaryReplica primaryReplica, CancellationToken cancellationToken)
        {
            this.primaryReplica = primaryReplica;

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
                            primaryReplica,
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
                            primaryReplica,
                            secretHash,
                            block,
                            replicaSecret,
                            secretShareMessageTokenSources);
                        MessageBus.TryDequeue(out message);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SendMessage(Message message)
        {
            MessageBus.Enqueue(message);
        }
    }
}

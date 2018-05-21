using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    public class Replica
    {
        protected ConcurrentQueue<Message> messageBus = new ConcurrentQueue<Message>();
        PrimaryReplica primaryReplica;

        public int id;
        public Tee tee;
        public Replica parentReplica;
        public IList<Replica> childReplicas = new List<Replica>(2);

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
                    if (messageBus.TryPeek(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var preprocessingMessage = message as PreprocessingMessage;
                    if (preprocessingMessage != null)
                    {
                        PreprocessingHandler.Handle(preprocessingMessage, out replicaSecret);
                        messageBus.TryDequeue(out message);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        PrepareHandler.Handle(
                            prepareMessage,
                            tee,
                            replicaSecret,
                            childSecretHashes,
                            primaryReplica,
                            id,
                            parentReplica,
                            childReplicas.Select(r => r.id),
                            out block,
                            out replicaSecretShare,
                            out secretHash,
                            secretShareMessageTokenSources
                        );
                        messageBus.TryDequeue(out message);
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
                        messageBus.TryDequeue(out message);
                    }

                    var commitMessage = message as CommitMessage;
                    if (commitMessage != null)
                    {
                        CommitHandler.Handle(
                            commitMessage,
                            this,
                            secretHash,
                            block,
                            replicaSecret);
                        messageBus.TryDequeue(out message);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SendMessage(Message message)
        {
            messageBus.Enqueue(message);
        }
    }
}

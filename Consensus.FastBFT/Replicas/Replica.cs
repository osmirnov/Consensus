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
                var replicaSecrets = new ConcurrentDictionary<int, byte[]>();
                var childrenSecretHashes = new ConcurrentDictionary<int, Dictionary<int, uint>>();
                var secretShareMessageTokenSources = new ConcurrentDictionary<int, Dictionary<int, CancellationTokenSource>>();

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
                        PreprocessingHandler.Handle(preprocessingMessage, replicaSecrets);
                        messageBus.TryDequeue(out message);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        PrepareHandler.Handle(
                            prepareMessage,
                            tee,
                            replicaSecrets,
                            childrenSecretHashes,
                            primaryReplica,
                            id,
                            parentReplica,
                            childReplicas.Select(r => r.id),
                            secretShareMessageTokenSources
                        );
                        messageBus.TryDequeue(out message);
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        SecretShareHandler.Handle(
                            secretShareMessage,
                            tee,
                            childrenSecretHashes,
                            secretShareMessageTokenSources);
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

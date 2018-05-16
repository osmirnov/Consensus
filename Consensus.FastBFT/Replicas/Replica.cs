using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    class Replica
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
                var childrenSecretHashes = new Dictionary<int, uint>();
                var secretShareMessageTokenSources = new Dictionary<int, CancellationTokenSource>();

                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (messageBus.TryDequeue(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var preprocessingMessage = message as PreprocessingMessage;
                    if (preprocessingMessage != null)
                    {
                        replicaSecret = preprocessingMessage.ReplicaSecret;
                        continue;
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        var requestCounterViewNumber = prepareMessage.RequestCounterViewNumber;

                        string secretShare;
                        uint secretHash;

                        tee.VerifyCounter(
                            requestCounterViewNumber,
                            replicaSecret,
                            out secretShare,
                            out childrenSecretHashes,
                            out secretHash);

                        if (childReplicas.Any())
                        {
                            secretShareMessageTokenSources.Clear();

                            var secretShareMessageTasks = childReplicas
                                .Select(childReplica =>
                                {
                                    var cancellationTokenSource = new CancellationTokenSource();
                                    secretShareMessageTokenSources.Add(childReplica.id, cancellationTokenSource);
                                    return Task.Delay(1000, cancellationTokenSource.Token)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsCompleted)
                                            {
                                                primaryReplica.SendMessage(new SuspectMessage { ReplicaId = childReplica.id });
                                            }
                                        });
                                });
                        }

                        parentReplica.SendMessage(new SecretShareMessage { ReplicaId = id, SecreShare = secretShare });

                        continue;
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        var replicaId = secretShareMessage.ReplicaId;

                        secretShareMessageTokenSources[replicaId].Cancel();

                        if (tee.Crypto.GetHash(secretShareMessage.SecreShare) == childrenSecretHashes[replicaId])
                        {

                        }

                        continue;
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

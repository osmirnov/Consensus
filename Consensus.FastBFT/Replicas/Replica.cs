using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                        string childrenSecretHash;
                        string secretHash;

                        tee.VerifyCounter(
                            requestCounterViewNumber,
                            replicaSecret,
                            out secretShare,
                            out childrenSecretHash,
                            out secretHash);

                        if (childReplicas.Any())
                        {

                        }
                        else
                        {
                            parentReplica.SendMessage(new SecretShareMessage { secreShare = secretShare });
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

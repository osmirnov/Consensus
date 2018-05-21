using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT
{
    public class Client
    {
        private Random rnd = new Random(Environment.TickCount);
        private int id;
        private ConcurrentQueue<Message> MessageBus = new ConcurrentQueue<Message>();
        private PrimaryReplica primaryReplica;

        public Client(int id)
        {
            this.id = id;
        }

        public void Run(PrimaryReplica primaryReplica, CancellationToken cancellationToken)
        {
            this.primaryReplica = primaryReplica;

            // process transactions
            Task.Factory.StartNew(() =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // process messages
            Task.Factory.StartNew(() =>
            {
                while (cancellationToken.IsCancellationRequested == false)
                {
                    Message message;
                    if (MessageBus.TryPeek(out message) == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SendMessage(Message message)
        {
            MessageBus.Enqueue(message);
        }

        private void GenerateTransaction(CancellationToken cancellationToken, out CancellationTokenSource tokenSource)
        {
            if (primaryReplica == null)
            {
                tokenSource = null;
                return;
            }

            if (rnd.Next(100) % 33 == 1)
            {
                var transaction = rnd.Next();

                Console.WriteLine($"The transaction #{transaction} was generated");

                // transaction was sent to primary replica
                Network.EmulateLatency();

                primaryReplica.SendMessage(new TransactionMessage
                {
                    Transaction = transaction
                });

                tokenSource = new CancellationTokenSource();

                Task.Delay(15 * 1000, tokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted)
                        {
                            // timeout
                        }
                    });
            }
            else
            {
                tokenSource = null;
            }
        }
    }
}

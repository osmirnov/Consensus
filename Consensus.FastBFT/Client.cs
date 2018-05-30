using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT
{
    public class Client
    {
        private readonly Random rnd = new Random(Environment.TickCount);
        private readonly int id;
        private readonly ConcurrentQueue<Message> messageBus = new ConcurrentQueue<Message>();

        public Client(int id)
        {
            this.id = id;
        }

        public void Run(PrimaryReplica primaryReplica, CancellationToken cancellationToken)
        {
            // process transactions
            // process messages
            Task.Factory.StartNew(async () =>
            {
                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var transaction = GenerateTransaction();

                    // Log($"The transaction #{transaction} was generated.");

                    // a new transaction was generated -> send this to primary replica
                    SendTransactionToPrimaryReplica(primaryReplica, transaction);

                    // Log($"The transaction #{transaction} was sent to the primary replica.");

                    await Task.Delay(10);
                }

                Log("Stopped.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SendMessage(Message message)
        {
            messageBus.Enqueue(message);
        }

        private void Log(string message)
        {
            Console.WriteLine($"Client #{id}: {message}");
        }

        private int GenerateTransaction()
        {
            return rnd.Next();
        }

        private static void SendTransactionToPrimaryReplica(PrimaryReplica primaryReplica, int transaction)
        {
            primaryReplica.SendMessage(new TransactionMessage
            {
                Transaction = transaction
            });
        }

        private async Task WaitUntilTimeoutOrReplyFromPrimaryReplica(CancellationToken cancellationToken)
        {
            // wait for reply 10 seconds
            using (var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationTokenSource.Token))
            {
                var linkedToken = linkedTokenSource.Token;

                await Task.Factory.StartNew(async () =>
                {
                    while (linkedToken.IsCancellationRequested == false)
                    {
                        Message message;
                        if (messageBus.TryDequeue(out message) == false)
                        {
                            // there is no reply message so far
                            await Task.Delay(1000, linkedToken);
                            continue;
                        }

                        // message was received
                        Log(message.ToString());
                        break;
                    }
                }, linkedToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }
    }
}

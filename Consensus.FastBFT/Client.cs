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
                int? transaction = null;

                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    if (transaction != null)
                    {
                        // there is an outstanding transaction -> wait for consensus on it
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    // no transaction -> give a chance to generate it
                    transaction = GenerateTransactionOrPass();

                    if (transaction == null)
                    {
                        // the client has no transaction at this time
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    Log($"The transaction #{transaction} was generated.");

                    // a new transaction was generated -> send this to primary replica
                    SendTransactionToPrimaryReplica(primaryReplica, transaction.Value);

                    Log($"The transaction #{transaction} was sent to the primary replica.");

                    // the primary replica got the transaction -> wait until consensus on this will be reached
                    var replyTask = WaitUntilTimeoutOrReplyFromPrimaryReplica(cancellationToken);

                    await replyTask.ConfigureAwait(false);

                    if (replyTask.IsCanceled)
                    {
                        Log($"The transaction #{transaction} was NOT approved.");
                    }
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

        private int? GenerateTransactionOrPass()
        {
            if (rnd.Next(100) % (33 + id) != 1) return null;

            return rnd.Next();
        }

        private static void SendTransactionToPrimaryReplica(Replica primaryReplica, int transaction)
        {
            // transaction was sent to primary replica
            Network.EmulateLatency();

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

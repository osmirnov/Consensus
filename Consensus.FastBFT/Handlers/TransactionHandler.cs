using System;
using System.Collections.Concurrent;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class TransactionHandler : Handler
    {
        public static int MinTransactionsCountInBlock = 100;

        public static void Handle(
            TransactionMessage message,
            PrimaryReplica primaryReplica,
            ref ConcurrentBag<int> block,
            ConcurrentQueue<int[]> blockExchange)
        {
            var transaction = message.Transaction;

            block.Add(transaction);

            var now = DateTime.Now;
            if (block.Count >= MinTransactionsCountInBlock)
            {
                var blockCopy = block.ToArray();

                block = new ConcurrentBag<int>();

                // publish block to start working on consensus
                blockExchange.Enqueue(blockCopy);

                Log(primaryReplica, $"New block arrived for consensus (TransactionCounts: {blockCopy.Length}).");
            }
        }
    }
}

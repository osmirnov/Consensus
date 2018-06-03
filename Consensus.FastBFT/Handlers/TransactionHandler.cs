using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class TransactionHandler : Handler
    {
        public static int MinTransactionsCountInBlock = 100;

        public static void Handle(
            TransactionMessage message,
            IList<int> block,
            ConcurrentQueue<int[]> blockExchange)
        {
            var transaction = message.Transaction;

            lock (block)
            {
                block.Add(transaction);
            }

            var now = DateTime.Now;
            if (block.Count >= MinTransactionsCountInBlock)
            {
                var blockCopy = block.ToArray();

                lock (block)
                {
                    block.Clear();
                }

                // publish block to start working on consensus
                blockExchange.Enqueue(blockCopy);

                // Log($"New block arrived for consensus (tx count: {blockCopy.Length})");
            }
        }
    }
}

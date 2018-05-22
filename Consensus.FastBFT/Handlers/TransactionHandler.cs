using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class TransactionHandler
    {
        public const int IntervalBetweenBlocks = 1;

        public static void Handle(
            TransactionMessage message,
            IList<int> block,
            ref DateTime lastBlockCreatedAt,
            ConcurrentQueue<int[]> blockExchange)
        {
            var transaction = message.Transaction;

            lock (block)
            {
                block.Add(transaction);
            }

            var now = DateTime.Now;
            if ((now - lastBlockCreatedAt).TotalSeconds > IntervalBetweenBlocks && block.Any())
            {
                var blockCopy = block.ToArray();

                lock (block)
                {
                    lastBlockCreatedAt = now;
                    block.Clear();
                }

                // publish block to start working on consensus
                blockExchange.Enqueue(blockCopy);
            }
        }
    }
}

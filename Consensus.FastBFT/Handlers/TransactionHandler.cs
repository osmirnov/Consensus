﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class TransactionHandler
    {
        public const int MinTransactionsCountInBlock = 10;

        public static void Handle(TransactionMessage message, IList<int> block, ConcurrentQueue<int[]> blockExchange)
        {
            var transaction = message.Transaction;

            lock (block)
            {
                block.Add(transaction);
            }

            if (block.Count >= MinTransactionsCountInBlock)
            {
                var blockCopy = block.ToArray();

                lock (block)
                {
                    block.Clear();
                }

                // publish block to start working on consensus
                blockExchange.Enqueue(blockCopy);
            }
        }
    }
}

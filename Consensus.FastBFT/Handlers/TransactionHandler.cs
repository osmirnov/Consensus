using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Handlers
{
    public class TransactionHandler
    {
        public const int MinTransactionsCountInBlock = 10;

        public static void Handle(TransactionMessage message, IList<int> block, ConcurrentQueue<int[]> blockBuffer)
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
                blockBuffer.Enqueue(blockCopy);
            }
        }
    }
}

using System;

namespace Consensus.FastBFT
{
    public class ConsensusResult
    {
        public DateTime StartedAt { get; set; }

        public DateTime ReachedAt { get; set; }

        public int BlockNumber { get; set; }

        public int TransactionsCount { get; set; }
    }
}

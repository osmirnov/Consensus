using System.Collections.Generic;

namespace Consensus.FastBFT.Messages
{
    public class NewViewMessage : Message
    {
        public IEnumerable<int[]> AheadBlocks { get; internal set; }
        public IEnumerable<int> ReplicaTree { get; internal set; }
        public byte[] SignedAheadBlocksOrTreeHashAndCounterViewNumber { get; internal set; }
        public string EncryptedViewKey { get; set; }
    }
}
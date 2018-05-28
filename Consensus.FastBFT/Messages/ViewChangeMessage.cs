namespace Consensus.FastBFT.Messages
{
    public class ViewChangeMessage : Message
    {
        public int ReplicaId { get; set; }
        public byte[] SignedAheadBlocksOrTreeHashAndCounterViewNumber { get; set; }
    }
}
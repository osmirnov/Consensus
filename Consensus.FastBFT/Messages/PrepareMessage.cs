namespace Consensus.FastBFT.Messages
{
    public class PrepareMessage : Message
    {
        public int ReplicaSecretIndex { get; set; }

        public int[] Block { get; set; }

        public byte[] RequestCounterViewNumber { get; set; }
    }
}
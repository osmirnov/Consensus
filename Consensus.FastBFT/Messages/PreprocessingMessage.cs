namespace Consensus.FastBFT.Messages
{
    public class PreprocessingMessage : Message
    {
        public byte[] ReplicaSecret { get; set; }
    }
}
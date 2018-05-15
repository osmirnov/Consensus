namespace Consensus.FastBFT.Messages
{
    internal class PreprocessingMessage : Message
    {
        public byte[] ReplicaSecret { get; set; }
    }
}
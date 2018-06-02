namespace Consensus.FastBFT.Messages
{
    public class PreprocessingMessage : Message
    {
        public int ReplicaSecretIndex { get; set; }
        public byte[] ReplicaSecret { get; set; }
    }
}
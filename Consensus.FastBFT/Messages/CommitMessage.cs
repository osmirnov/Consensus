namespace Consensus.FastBFT.Messages
{
    public class CommitMessage : Message
    {
        public int ReplicaSecretIndex { get; set; }

        public string Secret { get; set; }

        public int CommitResult { get; set; }

        public byte[] CommitResultHashCounterViewNumber { get; set; }
    }
}
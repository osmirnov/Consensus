namespace Consensus.FastBFT.Messages
{
    public class CommitMessage : Message
    {
        public int NextReplicaSecretIndex { get; set; }

        public string Secret { get; set; }

        public int CommitResult { get; set; }

        public byte[] CommitResultHashCounterViewNumber { get; set; }
    }
}
namespace Consensus.FastBFT.Messages
{
    public class CommitMessage : Message
    {
        public string Secret { get; set; }

        public int CommitResult { get; set; }

        public string CommitResultHashCounterViewNumber { get; set; }
    }
}
namespace Consensus.FastBFT.Messages
{
    public class ReplyMessage : Message
    {
        public string Request { get; set; }

        public int CommitResult { get; set; }

        public string PrevSecret { get; set; }

        public string Secret { get; set; }

        public string PrevSecretHashCounterViewNumber { get; set; }

        public string SecretHashCounterViewNumber { get; set; }

        public string RequestHashPrevCounterViewNumber { get; set; }

        public string RequestCommitResultHashCounterViewNumber { get; set; }
    }
}
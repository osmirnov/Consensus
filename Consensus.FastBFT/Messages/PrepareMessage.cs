namespace Consensus.FastBFT.Messages
{
    public class PrepareMessage : Message
    {
        public string RequestCounterViewNumber { get; set; }
    }
}
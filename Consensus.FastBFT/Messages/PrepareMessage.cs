namespace Consensus.FastBFT.Messages
{
    internal class PrepareMessage : Message
    {
        public string RequestCounterViewNumber { get; set; }
    }
}
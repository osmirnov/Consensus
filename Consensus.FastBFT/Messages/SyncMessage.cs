namespace Consensus.FastBFT.Messages
{
    public class SyncMessage : Message
    {
        public string EncryptedViewKey { get; set; }
        public uint SignedPayloadAndCounterViewNumber { get; set; }
    }
}
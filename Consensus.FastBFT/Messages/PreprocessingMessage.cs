using System.Collections.Generic;

namespace Consensus.FastBFT.Messages
{
    public class PreprocessingMessage : Message
    {
        public List<byte[]> ReplicaSecrets { get; set; }
    }
}
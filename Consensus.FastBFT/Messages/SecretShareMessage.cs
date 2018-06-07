using System.Collections.Generic;

namespace Consensus.FastBFT.Messages
{
    public class SecretShareMessage : Message
    {
        public int ReplicaSecretIndex { get; set; }

        public int ReplicaId { get; set; }

        public Dictionary<int, string> ReplicaSecretShares { get; set; }
    }
}

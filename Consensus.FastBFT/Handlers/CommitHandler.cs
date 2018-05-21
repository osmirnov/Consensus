using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class CommitHandler
    {
        public static void Handle(
            CommitMessage message,
            Replica replica,
            uint secretHash,
            int[] block,
            byte[] encryptedReplicaSecret)
        {
            if (replica.tee.Crypto.GetHash(message.Secret) == secretHash)
            {
                // perform the same op as a primary replica
                var commitResult = block.Sum();

                if (message.CommitResult == commitResult)
                {
                    string nextSecretShare;
                    Dictionary<int, uint> nextChildrenSecretHashes;
                    uint nextSecretHash;

                    replica.tee.VerifyCounter(
                        message.CommitResultHashCounterViewNumber,
                        encryptedReplicaSecret,
                        out nextSecretShare,
                        out nextChildrenSecretHashes,
                        out nextSecretHash);

                    if (replica.childReplicas.Any())
                    {

                    }
                    else
                    {

                    }
                }
            }
        }
    }
}

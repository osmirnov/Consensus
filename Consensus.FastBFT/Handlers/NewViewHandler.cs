using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class NewViewHandler : Handler
    {
        public static void Handle(
            NewViewMessage message,
            Replica replica,
            Replica[] activeReplicas,
            out byte[] signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber,
            out string encryptedViewKey)
        {
            signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber =
                message.SignedAheadBlocksOrTreeHashAndCounterViewNumber;

            encryptedViewKey = message.EncryptedViewKey;

            // assume validity of the blockchain for now
            var aheadBlocksOrTree = (message.AheadBlocks.LastOrDefault() ?? new int[0]).Sum() | message.ReplicaTree.Sum();
            var signedByReplicaAheadBlocksOrTreeHashAndCounterViewNumber = replica.Tee.RequestCounter(Crypto.GetHash(aheadBlocksOrTree.ToString()));

            foreach (var activeReplica in activeReplicas)
            {
                activeReplica.SendMessage(new ViewChangeMessage
                {
                    ReplicaId = replica.Id,
                    SignedAheadBlocksOrTreeHashAndCounterViewNumber = signedByReplicaAheadBlocksOrTreeHashAndCounterViewNumber
                });
            }
        }
    }
}

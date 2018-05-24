using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class NewViewHandler
    {
        public static void Handle(
            NewViewMessage message,
            Replica replica,
            out byte[] signedHashAndCounterViewNumber)
        {
            var aheadBlocksOrTree = (message.AheadBlocks.LastOrDefault() ?? new int[0]).Sum() | message.ReplicaTree.Sum();
            signedHashAndCounterViewNumber = replica.Tee.RequestCounter(Crypto.GetHash(aheadBlocksOrTree.ToString()));
        }
    }
}

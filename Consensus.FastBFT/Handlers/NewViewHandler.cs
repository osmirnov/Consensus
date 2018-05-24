using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Handlers
{
    public class NewViewHandler
    {
        public static void Handle(
            NewViewMessage message,
            Tee tee,
            out byte[] signedHashAndCounterViewNumber)
        {
            var aheadBlocksOrTree = (message.AheadBlocks.LastOrDefault() ?? new int[0]).Sum() | message.ReplicaTree.Sum();
            signedHashAndCounterViewNumber = tee.RequestCounter(Crypto.GetHash(aheadBlocksOrTree.ToString()));
        }
    }
}

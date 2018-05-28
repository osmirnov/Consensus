using System.Collections.Concurrent;
using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class ViewChangeHandler : Handler
    {
        public static void Handle(
            ViewChangeMessage message,
            Replica replica,
            Replica[] activeReplicas,
            ConcurrentDictionary<int, int> viewChangesCount,
            byte[] signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber,
            string encryptedViewKey
        )
        {
            // execute the requests in O that have not been executed
            // extract and store information from T

            var activeReplica = activeReplicas.SingleOrDefault(r => r.Id == message.ReplicaId);
            if (activeReplica == null)
            {
                return;
            }

            byte[] buffer;

            if (Crypto.Verify(replica.PublicKey, message.SignedAheadBlocksOrTreeHashAndCounterViewNumber, out buffer) ==
                false)
            {
                return;
            }

            viewChangesCount.AddOrUpdate(buffer.Sum(v => v), 1, (_, count) => count + 1);

            var maxViewChangeCount = viewChangesCount
                .OrderByDescending(vchc => vchc.Value)
                .FirstOrDefault();

            if (maxViewChangeCount.Key == 0 || maxViewChangeCount.Value < activeReplicas.Length - 1)
            {
                return;
            }

            replica.Tee.UpdateView(
                replica.PrimaryReplica.PublicKey,
                signedByPrimaryReplicaAheadBlocksOrTreeHashAndCounterViewNumber,
                encryptedViewKey);
        }
    }
}
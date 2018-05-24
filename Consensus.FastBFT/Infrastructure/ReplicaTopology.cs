using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Infrastructure
{
    public class ReplicaTopology
    {
        public static void Discover(ReplicaBase parentReplica, IEnumerable<ReplicaBase> secondaryReplicas)
        {
            if (parentReplica == null || secondaryReplicas.Any() == false) return;

            var leftReplicas = secondaryReplicas.Where(r => r.Id != parentReplica.Id);
            var rightReplicas = leftReplicas.Skip(1);
            var restReplicas = rightReplicas.Skip(1);

            if (leftReplicas.Any())
            {
                var leftReplica = leftReplicas.FirstOrDefault();

                leftReplica.ParentReplica = parentReplica;

                parentReplica.ChildReplicas.Add(leftReplica);

                Discover(leftReplica, restReplicas.Take(restReplicas.Count() / 2));
            }

            if (rightReplicas.Any())
            {
                var rightReplica = rightReplicas.FirstOrDefault();

                rightReplica.ParentReplica = parentReplica;

                parentReplica.ChildReplicas.Add(rightReplica);

                Discover(rightReplica, restReplicas.Skip(restReplicas.Count() / 2));
            }
        }

        public static IEnumerable<int> GetReplicaTree(ReplicaBase replica)
        {
            var replicaIds = new List<int>();

            foreach (var childReplica in replica.ChildReplicas)
            {
                replicaIds.Add(childReplica.Id);
                GetReplicaTree(childReplica, replicaIds);
            }

            return replicaIds;
        }

        private static void GetReplicaTree(ReplicaBase replica, ICollection<int> replicaIds)
        {
            foreach (var childReplica in replica.ChildReplicas)
            {
                replicaIds.Add(childReplica.Id);
                GetReplicaTree(childReplica, replicaIds);
            }
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Infrastructure
{
    public class ReplicaTopology
    {
        public static void Discover(Replica parentReplica, IEnumerable<Replica> secondaryReplicas)
        {
            if (parentReplica == null) return;

            var leftReplicas = secondaryReplicas.Skip(1);
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

        public static IReadOnlyDictionary<int, int[]> ToGraph(Replica replica)
        {
            var replicaGraph = new Dictionary<int, int[]>();

            ToGraph(replica, replicaGraph);

            return replicaGraph;
        }

        private static void ToGraph(Replica replica, IDictionary<int, int[]> replicaChildren)
        {
            replicaChildren.Add(replica.Id, replica.ChildReplicas.Select(chr => chr.Id).OrderBy(chrid => chrid).ToArray());

            foreach (var childReplica in replica.ChildReplicas)
            {
                ToGraph(childReplica, replicaChildren);
            }
        }
    }
}
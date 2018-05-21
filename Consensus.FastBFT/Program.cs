using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT
{
    class Program
    {
        class Interval
        {
            public DateTime from = DateTime.Now;
            public DateTime to = DateTime.Now;
        }

        const int clientsCount = 12;
        const int replicasCount = 6;
        const int intervalBetweenTransactions = 2; // seconds

        static int[] clientIds = Enumerable.Range(0, clientsCount).ToArray();
        static int[] replicaIds = Enumerable.Range(0, replicasCount).ToArray();
        static PrimaryReplica primaryReplica;

        static ConcurrentDictionary<string, Interval> consensusIntervals = new ConcurrentDictionary<string, Interval>();
        static Random rnd = new Random(Environment.TickCount);

        static void Main(string[] args)
        {
            var from = DateTime.Now;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var token = cancellationTokenSource.Token;

                RunClients(token);
                RunReplicas(token);

                Console.ReadKey();
                cancellationTokenSource.Cancel();
            }

            var to = DateTime.Now;

            Thread.Sleep(2500);

            var intervals = consensusIntervals
                .Select(ci => ci.Value)
                .Where(i => (i.to - i.from).TotalSeconds > 3)
                .OrderBy(i => i.to - i.from)
                .ToList();
            var minInterval = intervals.FirstOrDefault();
            var maxInterval = intervals.LastOrDefault();
            var avgInterval = intervals.Sum(i => (i.to - i.from).TotalSeconds) / intervals.Count;

            Console.WriteLine($"The time spent on emulation was {(to - from).TotalSeconds}s");
            //Console.WriteLine($"The time between transactions was {intervalBetweenTransactions}s");
            //Console.WriteLine($"The time for after speaker election was {waitTimeAfterSpeakerElection}ms");
            //Console.WriteLine($"Avg network latency was {(maxNetworkLatency - minNetworkLatency) / 2}ms");
            //Console.WriteLine($"The consesus were reached {intervals.Count}");

            if (minInterval != null)
                Console.WriteLine($"The min consensus took {(minInterval.to - minInterval.from).TotalSeconds}s");

            if (maxInterval != null)
                Console.WriteLine($"The max consensus took {(maxInterval.to - maxInterval.from).TotalSeconds}s");

            Console.WriteLine($"The avg consensus took {avgInterval}s");

            Console.ReadKey();
        }

        private static void RunClients(CancellationToken token)
        {
            for (var i = 0; i < clientIds.Length; i++)
            {
                var clientId = clientIds[i];

                Task.Factory.StartNew(() =>
                {
                    while (token.IsCancellationRequested == false)
                    {
                        GenerateTransaction();
                    }
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            }
        }

        private static void GenerateTransaction()
        {
            if (primaryReplica == null) return;

            var sec = DateTime.Now.Second;
            if (sec % 2 == rnd.Next(1) && sec % intervalBetweenTransactions == 0)
            {
                var transaction = rnd.Next();

                Console.WriteLine($"The transaction #{transaction} was generated");

                // transaction was sent to primary replica
                Network.EmulateLatency();

                primaryReplica.SendMessage(new TransactionMessage {
                    Transaction = transaction
                });
            }
        }

        private static void RunReplicas(CancellationToken token)
        {
            var workingReplicaIds = replicaIds
                .OrderBy(r => rnd.Next(replicaIds.Length))
                .Take(replicaIds.Length * 2 / 3)
                .ToArray();

            var faultyReplicaIds = replicaIds
                .Except(workingReplicaIds)
                .ToArray();

            var activeReplicaIds = workingReplicaIds
                .Take(workingReplicaIds.Length * 2 / 3)
                .ToArray();

            var passiveReplicaIds = workingReplicaIds
                .Except(activeReplicaIds)
                .ToArray();

            var primaryReplicaId = activeReplicaIds.First();

            var secondaryReplicas = activeReplicaIds
                .Where(rid => rid != primaryReplicaId)
                .Select(rid =>
                {
                    var replica = new Replica();
                    replica.Id = rid;
                    replica.Tee = new Tee();
                    replica.Tee.isActive = true;
                    return replica;
                })
                .ToArray();

            for (var i = 0; i < secondaryReplicas.Length; i++)
            {
                var secondaryReplica = secondaryReplicas[i];

                secondaryReplica.Run(primaryReplica, token);
            }

            primaryReplica = new PrimaryReplica();
            primaryReplica.Id = primaryReplicaId;

            DiscoverReplicaTopology(primaryReplica, secondaryReplicas);

            var activeReplicas = new Dictionary<int, int[]>(activeReplicaIds.Length);

            ConvertReplicaTopologyToGraph(primaryReplica, activeReplicas);

            primaryReplica.Tee = new PrimaryTee(activeReplicas);

            primaryReplica.Run(secondaryReplicas, token);
        }

        private static void DiscoverReplicaTopology(Replica parentReplica, IEnumerable<Replica> secondaryReplicas)
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

                DiscoverReplicaTopology(leftReplica, restReplicas.Take(restReplicas.Count() / 2));
            }

            if (rightReplicas.Any())
            {
                var rightReplica = rightReplicas.FirstOrDefault();

                rightReplica.ParentReplica = parentReplica;

                parentReplica.ChildReplicas.Add(rightReplica);

                DiscoverReplicaTopology(rightReplica, restReplicas.Skip(restReplicas.Count() / 2));
            }
        }

        private static void ConvertReplicaTopologyToGraph(Replica replica, IDictionary<int, int[]> replicaChildren)
        {
            replicaChildren.Add(replica.Id, replica.ChildReplicas.Select(chr => chr.Id).OrderBy(chrid => chrid).ToArray());

            foreach (var childReplica in replica.ChildReplicas)
            {
                ConvertReplicaTopologyToGraph(childReplica, replicaChildren);
            }
        }
    }
}

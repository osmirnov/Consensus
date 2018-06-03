using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT
{
    internal class Program
    {
        private const int clientsCount = 1;
        private const int replicasCount = 19;
        private const int workingReplicasCount = replicasCount * 2 / 3;
        private const int activeReplicasCount = workingReplicasCount * 2 / 3;

        private static readonly List<ConsensusResult> consensusResults = new List<ConsensusResult>();

        private static void Main()
        {
            var from = DateTime.Now;

            Network.MinNetworkLatency = 10;
            Network.MaxNetworkLatency = 100;
            TransactionHandler.MinTransactionsCountInBlock = 100;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var token = cancellationTokenSource.Token;

                var primaryReplica = RunReplicas(token);

                primaryReplica.OnConsensusReached = cr => consensusResults.Add(cr);

                RunClients(primaryReplica, token);

                Console.ReadKey();
                cancellationTokenSource.Cancel();
            }

            var to = DateTime.Now;

            Thread.Sleep(2500);

            PrintRunSummary(to, from);

            Console.ReadKey();
        }

        private static void RunClients(PrimaryReplica primaryReplica, CancellationToken cancellationToken)
        {
            var clients = Enumerable.Range(0, clientsCount)
                .Select(cid => new Client(cid))
                .ToArray();

            foreach (var client in clients)
            {
                client.Run(primaryReplica, cancellationToken);
            }
        }

        private static PrimaryReplica RunReplicas(CancellationToken cancellationToken)
        {
            var rnd = new Random(Environment.TickCount);
            var replicaIds = Enumerable.Range(0, replicasCount).ToArray();

            var workingReplicaIds = replicaIds
                //.OrderBy(r => rnd.Next(replicaIds.Length))
                .Take(workingReplicasCount)
                .ToArray();

            var faultyReplicaIds = replicaIds
                .Except(workingReplicaIds)
                .ToArray();

            var activeReplicaIds = workingReplicaIds
                .Take(activeReplicasCount)
                .ToArray();

            var passiveReplicaIds = workingReplicaIds
                .Except(activeReplicaIds)
                .ToArray();

            var primaryReplicaId = activeReplicaIds.First();
            var primaryReplica = new PrimaryReplica(primaryReplicaId);

            var secondaryReplicas = activeReplicaIds
                .Where(rid => rid != primaryReplicaId)
                .Select(rid => new Replica(rid, true)
                {
                    PrimaryReplica = primaryReplica
                })
                .ToArray();


            foreach (var secondaryReplica in secondaryReplicas)
            {
                secondaryReplica.Run(secondaryReplicas, cancellationToken);
            }


            primaryReplica.Run(secondaryReplicas, cancellationToken);

            return primaryReplica;
        }

        private static void PrintRunSummary(DateTime to, DateTime from)
        {
            var orderedConsensusResults = consensusResults
                .OrderBy(cr => cr.ReachedAt - cr.StartedAt)
                .ToList();
            var minInterval = orderedConsensusResults.FirstOrDefault();
            var maxInterval = orderedConsensusResults.LastOrDefault();
            var avgInterval = orderedConsensusResults.Sum(cr => (cr.ReachedAt - cr.StartedAt).TotalSeconds) / orderedConsensusResults.Count;

            Console.WriteLine($"The time spent on emulation was {to - from}");
            Console.WriteLine($"Avg network latency was {(Network.MaxNetworkLatency + Network.MinNetworkLatency) / 2}ms");
            Console.WriteLine($"The replicas count was total {replicasCount}, active replicas {activeReplicasCount}, passive replicas {workingReplicasCount - activeReplicasCount}");
            Console.WriteLine($"The clients count was {clientsCount}");
            Console.WriteLine($"The consensus were reached {orderedConsensusResults.Count} times");

            if (minInterval != null)
                Console.WriteLine($"The min consensus took {(minInterval.ReachedAt - minInterval.StartedAt).TotalSeconds}s");

            if (maxInterval != null)
                Console.WriteLine($"The max consensus took {(maxInterval.ReachedAt - maxInterval.StartedAt).TotalSeconds}s");

            Console.WriteLine($"The avg consensus took {avgInterval}s");
        }
    }
}

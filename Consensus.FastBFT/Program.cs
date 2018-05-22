using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT
{
    internal class Program
    {
        private class Interval
        {
            public readonly DateTime From = DateTime.Now;
            public readonly DateTime To = DateTime.Now;
        }

        private const int clientsCount = 12;
        private const int replicasCount = 9;

        private static readonly ConcurrentDictionary<string, Interval> consensusIntervals = new ConcurrentDictionary<string, Interval>();

        private static void Main()
        {
            var from = DateTime.Now;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var token = cancellationTokenSource.Token;

                var primaryReplica = RunReplicas(token);
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
            var primaryReplica = new PrimaryReplica { Id = primaryReplicaId };

            var secondaryReplicas = activeReplicaIds
                .Where(rid => rid != primaryReplicaId)
                .Select(rid => new Replica
                {
                    Id = rid,
                    Tee = new Tee { IsActive = true },
                    PrimaryReplica = primaryReplica
                })
                .ToArray();

            ReplicaTopology.Discover(primaryReplica, secondaryReplicas);

            foreach (var secondaryReplica in secondaryReplicas)
            {
                secondaryReplica.Run(cancellationToken);
            }

            var primaryTee = new PrimaryTee();
            var encryptedViewKeys = primaryTee.Initialize(ReplicaTopology.GetActiveReplicas(primaryReplica));

            primaryReplica.Tee = primaryTee;
            primaryReplica.Run(secondaryReplicas, cancellationToken);

            SyncReplicas(encryptedViewKeys, secondaryReplicas);

            return primaryReplica;
        }

        private static void SyncReplicas(IReadOnlyDictionary<int, string> encryptedViewKeys, IEnumerable<Replica> activeReplicas)
        {
            // we sync tee counter and the current view
            // we assume it is done in parallel and this network delay represents all of them
            Network.EmulateLatency();

            foreach (var activeReplica in activeReplicas)
            {
                activeReplica.SendMessage(new SyncMessage
                {
                    //SignedPayloadAndCounterViewNumber = Tee.LatestCounter,
                    EncryptedViewKey = encryptedViewKeys[activeReplica.Id]
                });
            }
        }

        private static void PrintRunSummary(DateTime to, DateTime from)
        {
            var intervals = consensusIntervals
                .Select(ci => ci.Value)
                .Where(i => (i.To - i.From).TotalSeconds > 3)
                .OrderBy(i => i.To - i.From)
                .ToList();
            var minInterval = intervals.FirstOrDefault();
            var maxInterval = intervals.LastOrDefault();
            var avgInterval = intervals.Sum(i => (i.To - i.From).TotalSeconds) / intervals.Count;

            Console.WriteLine($"The time spent on emulation was {(to - from).TotalSeconds}s");
            Console.WriteLine($"The consensus were reached {intervals.Count}");

            if (minInterval != null)
                Console.WriteLine($"The min consensus took {(minInterval.To - minInterval.From).TotalSeconds}s");

            if (maxInterval != null)
                Console.WriteLine($"The max consensus took {(maxInterval.To - maxInterval.From).TotalSeconds}s");

            Console.WriteLine($"The avg consensus took {avgInterval}s");
        }
    }
}

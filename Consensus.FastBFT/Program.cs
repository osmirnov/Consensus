using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT
{
    internal class Program
    {
        private static int ClientsCount;
        private static int ActiveReplicasCount;
        private static int PassiveReplicasCount;
        private static int FaultyReplicasCount;

        private static readonly List<ConsensusResult> consensusResults = new List<ConsensusResult>();

        private static void Main()
        {
            var from = DateTime.Now;

            ClientsCount = int.Parse(ConfigurationManager.AppSettings["ClientsCount"]);
            ActiveReplicasCount = int.Parse(ConfigurationManager.AppSettings["ActiveReplicasCount"]);
            PassiveReplicasCount = int.Parse(ConfigurationManager.AppSettings["PassiveReplicasCount"]);
            FaultyReplicasCount = int.Parse(ConfigurationManager.AppSettings["FaultyReplicasCount"]);

            Network.MinNetworkLatency = int.Parse(ConfigurationManager.AppSettings["MinNetworkLatency"]);
            Network.MaxNetworkLatency = int.Parse(ConfigurationManager.AppSettings["MaxNetworkLatency"]);

            TransactionHandler.MinTransactionsCountInBlock = int.Parse(ConfigurationManager.AppSettings["MinTransactionsCountInBlock"]);

            IEnumerable<int[]> blockchain;

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                var token = cancellationTokenSource.Token;

                var primaryReplica = RunReplicas(token);

                blockchain = primaryReplica.Blockchain;
                primaryReplica.OnConsensusReached = cr => consensusResults.Add(cr);

                RunClients(primaryReplica, token);

                Console.ReadKey();
                cancellationTokenSource.Cancel();
            }

            var to = DateTime.Now;

            Thread.Sleep(2500);

            PrintRunSummary(to, from, blockchain);

            Console.ReadKey();
        }

        private static void RunClients(PrimaryReplica primaryReplica, CancellationToken cancellationToken)
        {
            var clients = Enumerable.Range(0, ClientsCount)
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
            var replicaIds = Enumerable.Range(0, ActiveReplicasCount + PassiveReplicasCount + FaultyReplicasCount).ToArray();

            var workingReplicaIds = replicaIds
                //.OrderBy(r => rnd.Next(replicaIds.Length))
                .Take(ActiveReplicasCount + PassiveReplicasCount)
                .ToArray();

            var faultyReplicaIds = replicaIds
                .Except(workingReplicaIds)
                .ToArray();

            var activeReplicaIds = workingReplicaIds
                .Take(ActiveReplicasCount)
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

        private static void PrintRunSummary(DateTime to, DateTime from, IEnumerable<int[]> blockchain)
        {
            var logBuilder = new StringBuilder();
            var orderedConsensusResults = consensusResults
                .OrderBy(cr => cr.ReachedAt - cr.StartedAt)
                .ToList();
            var minInterval = orderedConsensusResults.FirstOrDefault();
            var maxInterval = orderedConsensusResults.LastOrDefault();
            var avgInterval = orderedConsensusResults.Sum(cr => (cr.ReachedAt - cr.StartedAt).TotalSeconds) / orderedConsensusResults.Count;
            var transactionsCount = blockchain.Sum(b => b.Length);
            var avgTransactionRate = orderedConsensusResults.Sum(cr => (cr.ReachedAt - cr.StartedAt).TotalSeconds) / transactionsCount;

            logBuilder.AppendLine($"The time spent on emulation was {to - from}");
            logBuilder.AppendLine($"The avg network latency was {(Network.MaxNetworkLatency + Network.MinNetworkLatency) / 2}ms");
            logBuilder.AppendLine($"The replicas count was total {ActiveReplicasCount + PassiveReplicasCount + FaultyReplicasCount}, active replicas {ActiveReplicasCount}, passive replicas {PassiveReplicasCount}");
            logBuilder.AppendLine($"The clients count was {ClientsCount}");
            logBuilder.AppendLine($"The avg transactions count in the block was {TransactionHandler.MinTransactionsCountInBlock}");
            logBuilder.AppendLine($"The handled transactions count was {transactionsCount}");
            logBuilder.AppendLine($"The consensus was reached {orderedConsensusResults.Count} times");

            if (minInterval != null)
                logBuilder.AppendLine($"The min consensus took {(minInterval.ReachedAt - minInterval.StartedAt).TotalSeconds}s");

            if (maxInterval != null)
                logBuilder.AppendLine($"The max consensus took {(maxInterval.ReachedAt - maxInterval.StartedAt).TotalSeconds}s");

            logBuilder.AppendLine($"The avg consensus took {avgInterval}s");
            logBuilder.AppendLine($"The avg transaction per second rate was {avgTransactionRate}");

            var log = logBuilder.ToString();

            Console.WriteLine(log);

            File.WriteAllText(DateTime.Now.ToString("ddMMyyyyHHmmsszz") + ".log", log);
        }
    }
}

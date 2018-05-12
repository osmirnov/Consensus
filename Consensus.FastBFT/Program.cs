using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consensus.FastBFT
{
    class Program
    {
        class Replica
        {
            public int id;
            public Tee tee;
        }

        class PrimaryReplica
        {
            public int id;
            public PrimaryTee tee;
        }

        class Block
        {
            public string id;
            public int h;
            public int k;
            public int i;
            public List<int> txs = new List<int>();
            public int sig;

            public Block(int h)
            {
                this.h = h;
            }
        }

        class Interval
        {
            public DateTime from = DateTime.Now;
            public DateTime to = DateTime.Now;
        }

        const int clientsCount = 12;
        const int replicasCount = 6;
        const int minTransactionsCountInBlock = 10;
        const int intervalBetweenTransactions = 2; // seconds
        private const int minNetworkLatency = 10;
        private const int maxNetworkLatency = 100;
        static int[] clientIds = Enumerable.Range(0, clientsCount).ToArray();
        static int[] replicaIds = Enumerable.Range(0, replicasCount).ToArray();

        static ConcurrentQueue<int> transactionPool = new ConcurrentQueue<int>();
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
                // cancellationTokenSource.Cancel();
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

        private static void EmulateNetworkLatency()
        {
            Thread.Sleep(rnd.Next(minNetworkLatency, maxNetworkLatency));
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
            var sec = DateTime.Now.Second;
            if (sec % 2 == rnd.Next(1) && sec % intervalBetweenTransactions == 0)
            {
                var tx = rnd.Next();
                transactionPool.Enqueue(tx);
                Console.WriteLine($"The transaction #{tx} was generated");

                // transaction was generated -> propagate this to network
                EmulateNetworkLatency();
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

            var secondaryReplicas = workingReplicaIds
                .Where(rid => rid != primaryReplicaId)
                .Select(rid =>
                {
                    var replica = new Replica();
                    replica.id = rid;
                    replica.tee = new Tee();
                    replica.tee.isActive = activeReplicaIds.Contains(rid);
                    return replica;
                })
                .ToArray();

            var leftTree = DiscoverReplicaTopology(null, secondaryReplicas.Take(secondaryReplicas.Count() / 2));
            var rightTree = DiscoverReplicaTopology(null, secondaryReplicas.Skip(secondaryReplicas.Count() / 2));

            var primaryReplica = new PrimaryReplica();
            primaryReplica.id = primaryReplicaId;
            var tree = new TreeNode { leftNode = leftTree, rightNode = rightTree, replica = primaryReplicaId };
            primaryReplica.tee = new PrimaryTee(tree);

            for (var i = 0; i < secondaryReplicas.Length; i++)
            {
                var secondaryReplica = secondaryReplicas[i];

                Task.Factory.StartNew(() =>
                {
                    while (token.IsCancellationRequested == false)
                    {
                        // 
                    }
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            }
        }

        private static TreeNode DiscoverReplicaTopology(TreeNode parentNode, IEnumerable<Replica> secondaryReplicas)
        {
            var parentReplica = secondaryReplicas.FirstOrDefault();
            if (parentReplica == null) return null;

            var childReplicas = secondaryReplicas.Except(new[] { parentReplica });
            var tree = new TreeNode();

            tree.parentNode = parentNode;
            tree.leftNode = DiscoverReplicaTopology(tree, childReplicas.Take(childReplicas.Count() / 2));
            tree.rightNode = DiscoverReplicaTopology(tree, childReplicas.Skip(childReplicas.Count() / 2));
            tree.replica = parentReplica.id;

            return tree;
        }
    }
}

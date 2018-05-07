using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Consensus.dBFT
{
    class Program
    {
        enum ConsensusStates
        {
            NotStarted = 0,
            Started = 1,
            SendInitialView = 2,
            PreparedRequest = 3,
            PreparedResponse = 4,
            SignedBlock = 5
        }

        class Block
        {
            internal string id;
            internal int h;
            internal int k;
            internal int i;
            internal List<int> txs = new List<int>();
            internal int sig;

            public Block(int h)
            {
                this.h = h;
            }
        }

        class Interval
        {
            internal DateTime from = DateTime.Now;
            internal DateTime to = DateTime.Now;
        }

        const int holdersCount = 12;
        const int delegatesCount = 4;
        const int minTransactionsInBlock = 10;
        const int waitTimeAfterSpeakerElection = 15 * 100;
        const int intervalBetweenTransactions = 2; // seconds
        private const int minNetworkLatency = 10;
        private const int maxNetworkLatency = 100;
        static int[] holderIds = Enumerable.Range(0, holdersCount).ToArray();
        static List<int> delegateIds = new List<int>();
        static ConcurrentDictionary<int, int> delegateVotes = new ConcurrentDictionary<int, int>();
        static ConcurrentQueue<int> transactionPool = new ConcurrentQueue<int>();
        static int consensusState = (int)ConsensusStates.NotStarted;
        static int speakerId;
        static ConcurrentDictionary<int, Block> delegateAccumulationPool = new ConcurrentDictionary<int, Block>();
        static ConcurrentDictionary<int, Block> delegateInitializationPool = new ConcurrentDictionary<int, Block>();
        static ConcurrentDictionary<int, Block> delegateNegotiationPool = new ConcurrentDictionary<int, Block>();
        static int delegateResponsesCount = 0;
        static ConcurrentDictionary<string, Interval> consensusIntervals = new ConcurrentDictionary<string, Interval>();
        static Random rnd = new Random(Environment.TickCount);

        static void Main(string[] args)
        {
            var from = DateTime.Now;
            var cancel = new CancellationTokenSource();

            // run citizens to do work and vote for delegates
            var holderTasks = holderIds
                .Select(holderId => Task.Run(() =>
                {
                    while (cancel.Token.IsCancellationRequested == false)
                    {
                        ProcessDelegateVoting();
                        GenerateTransaction(holderId);
                        ProcessConcensus(holderId);
                    }
                }, cancel.Token))
                .ToArray();

            Console.ReadKey();
            cancel.Cancel();
           
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
            Console.WriteLine($"The time between transactions was {intervalBetweenTransactions}s");
            Console.WriteLine($"The time for after speaker election was {waitTimeAfterSpeakerElection}ms");
            Console.WriteLine($"Avg network latency was {(maxNetworkLatency - minNetworkLatency) / 2}ms");
            Console.WriteLine($"The consesus were reached {intervals.Count}");

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

        private static void ProcessDelegateVoting()
        {
            if (NeedToVoteForDelegate())
            {
                lock (delegateIds)
                {
                    if (NeedToVoteForDelegate())
                        VoteForDelegate();
                }
            }
            else
            {
                if (IsDelegateFaulty())
                {
                    lock (delegateIds)
                    {
                        if (IsDelegateFaulty())
                        {
                            // faulty delegate is detected -> propagate dismission
                            EmulateNetworkLatency();

                            var delegateIndex = rnd.Next(delegateIds.Count);
                            int delegateId = DismissDelegateAt(delegateIndex);

                            // if faulty delegate is speaker -> dismiss them as well
                            Interlocked.CompareExchange(ref speakerId, 0, delegateId);
                        }
                    }
                }
            }
        }

        private static bool NeedToVoteForDelegate()
        {
            return delegateIds.Count < delegatesCount;
        }

        private static void VoteForDelegate()
        {
            var votedDelegateId = rnd.Next(holdersCount);

            // voting is done -> propagate result
            EmulateNetworkLatency();

            delegateVotes.AddOrUpdate(votedDelegateId, 0, (_, vote) => vote + 1);
            delegateVotes
                .Where(dv => delegateIds.Contains(dv.Key) == false)
                .Where(dv => dv.Value == holdersCount / 3)
                .Take(delegatesCount - delegateIds.Count)
                .ToList()
                .ForEach(dv =>
                {
                    delegateIds.Add(dv.Key);
                    Console.WriteLine("New Delegate #{0} ({1} votes)", dv.Key, dv.Value);
                    delegateVotes.Clear();
                });
        }

        private static bool IsDelegateFaulty()
        {
            return delegateIds.Count > 3 && rnd.Next(100) % 11 == 1;
        }

        private static int DismissDelegateAt(int delegateIndex)
        {
            var delegateId = delegateIds[delegateIndex];
            delegateIds.RemoveAt(delegateIndex);
            Console.WriteLine("Dimissed Delegate #{0}", delegateId);
            EndConsesus();
            return delegateId;
        }

        private static bool IAmDelgate(int holderId)
        {
            return delegateIds.Contains(holderId);
        }

        private static void GenerateTransaction(int holderId)
        {
            if (IAmDelgate(holderId) == false)
            {
                lock (delegateIds)
                {
                    if (IAmDelgate(holderId) == false)
                    {
                        if (DateTime.Now.Second % intervalBetweenTransactions == 0)
                        {
                            var tx = rnd.Next();
                            transactionPool.Enqueue(tx);
                            // Console.WriteLine($"The transaction #{tx} was generated");

                            // transaction was generated -> propagate this to network
                            EmulateNetworkLatency();
                        }
                    }
                }
            }
        }

        private static void ProcessConcensus(int holderId)
        {
            if (IAmDelgate(holderId))
            {
                lock (delegateIds)
                {
                    if (IAmDelgate(holderId))
                    {
                        try
                        {
                            switch (consensusState)
                            {
                                case (int)ConsensusStates.NotStarted:
                                    GenerateConsensusBlock(holderId);
                                    break;

                                case (int)ConsensusStates.Started:
                                    break;

                                case (int)ConsensusStates.SendInitialView:
                                    IdentifySpeaker(holderId);
                                    PrepareRequest(holderId);
                                    break;

                                case (int)ConsensusStates.PreparedRequest:
                                    PrepareResponse(holderId);
                                    break;

                                case (int)ConsensusStates.PreparedResponse:
                                    ConsensusReached(holderId);
                                    break;

                                case (int)ConsensusStates.SignedBlock:
                                    AppendBlockToLedger(holderId);
                                    break;

                                default:
                                    throw new Exception("Unknown consensus state");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            EndConsesus();
                        }   
                    }
                }
            }
        }

        private static void GenerateConsensusBlock(int delegateId)
        {
            // process transaction into blocks
            int tx;
            if (transactionPool.TryDequeue(out tx) == false)
            {
                // there are no transaction to merge into blocks
                return;
            }

            delegateAccumulationPool.AddOrUpdate(delegateId, new Block(rnd.Next(8, 16)), (_, blk) =>
            {
                blk.txs.Add(tx);
                return blk;
            });

            var block = delegateAccumulationPool[delegateId];
            if (block.txs.Count <= minTransactionsInBlock)
            {
                // not enough transactions in block
                return;
            }

            block.id = string.Join(string.Empty, block.txs.Select(t => t.ToString())).Substring(0, 12);

            if (Interlocked.CompareExchange(ref consensusState, (int)ConsensusStates.Started, (int)ConsensusStates.NotStarted) != (int)ConsensusStates.NotStarted)
            {
                // somebody else started consensus -> hold on own block and be involved
                return;
            }

            // we will assume block can serve as initial view + transactions
            // signed initial view
            SignBlock(block, delegateId);

            consensusIntervals.AddOrUpdate(block.id, new Interval(), (_, i) => i);
            Console.WriteLine($"The consensus started on block #{block.id} (del: {delegateId}, h: {block.h}, tx: {block.txs.Count})");

            SendInitialView(block);

            Interlocked.Exchange(ref consensusState, (int)ConsensusStates.SendInitialView);
        }

        private static void SendInitialView(Block block)
        {
            for (int i = 0; i < delegateIds.Count; i++)
            {
                var delegateId = delegateIds[i];
                var blockCopy = CopyBlock(block, delegateId);
                delegateInitializationPool.AddOrUpdate(delegateId, blockCopy, (did, blk) => blockCopy);
            }

            // all requests for initial view were sent in parallel -> start speaker selection
            EmulateNetworkLatency();
        }

        private static void IdentifySpeaker(int delegateId)
        {
            Block block;
            if (delegateInitializationPool.TryGetValue(delegateId, out block) == false)
            {
                // block is not in memory of delegate -> new or faulty delegate -> should not be part of consensus
                throw new Exception($"Missing block in IdentifySpeaker for delegate #{delegateId}");
            }

            var speakerIndex = (block.h - block.k) % delegateIds.Count;

            Interlocked.Exchange(ref speakerId, delegateIds[speakerIndex]);

            Console.WriteLine($"The speaker #{speakerId} for block #{block.id} (del: {delegateId}, h: {block.h}, k: {block.k})");

            // wait by design
            Thread.Sleep(waitTimeAfterSpeakerElection);
        }

        private static void PrepareRequest(int delegateId)
        {
            if (speakerId != delegateId)
            {
                // only speaker could initiate negotiations
                return;
            }

            Block block;
            if (delegateInitializationPool.TryGetValue(speakerId, out block) == false)
            {
                // block is not in memory of delegate -> new or faulty delegate -> should not be part of consensus
                throw new Exception($"Missing block in PrepareRequest for delegate #{delegateId}");
            }

            block.i = speakerId;
            SignBlock(block, speakerId);

            Console.WriteLine($"The speaker #{speakerId} proposed block #{block.id} (h: {block.h}, h: {block.k}, tx: {block.txs.Count})");

            SendPreparedRequest(block);

            Interlocked.Exchange(ref consensusState, (int)ConsensusStates.PreparedRequest);
        }

        private static void SendPreparedRequest(Block block)
        {
            for (int i = 0; i < delegateIds.Count; i++)
            {
                var delegateIdCopy = delegateIds[i];
                if (delegateIdCopy == speakerId)
                {
                    continue;
                }

                var blockCopy = CopyBlock(block, delegateIdCopy);
                delegateNegotiationPool.AddOrUpdate(delegateIdCopy, blockCopy, (did, blk) => blockCopy);
            }

            // all requests for speaker's view were sent in parallel -> start delegate's validation 
            EmulateNetworkLatency();
        }

        private static void PrepareResponse(int delegateId)
        {
            if (speakerId == delegateId)
            {
                // it is a speaker
                return;
            }

            Block block;
            if (delegateNegotiationPool.TryGetValue(delegateId, out block) == false)
            {
                // block is not in memory of delegate -> new or faulty delegate -> should not be part of consensus
                throw new Exception($"Missing block in PrepareResponse for delegate #{delegateId}");
            }

            if (block.sig != GetSig(speakerId))
            {
                return;
            }

            EvaluateSpeakerRequest(delegateId, block);

            Interlocked.Increment(ref delegateResponsesCount);

            if (delegateResponsesCount >= delegateIds.Count - 1)
            {
                Interlocked.Exchange(ref consensusState, (int)ConsensusStates.PreparedResponse);
            }
        }

        private static void EvaluateSpeakerRequest(int delegateId, Block block)
        {
            block.i = delegateId;

            if (IsProposalValid())
            {
                Console.WriteLine($"The delegate {delegateId} agreed on block #{block.id} (h: {block.h}, k: {block.k}, tx: {block.txs.Count})");
            }
            else
            {
                block.k += 1;
                Console.WriteLine($"The delegate {delegateId} did NOT agree on block #{block.id} (h: {block.h}, k: {block.k}, tx: {block.txs.Count})");
            }

            // all requests for delegate's response were sent in parallel -> check consensus results 
            EmulateNetworkLatency();
        }

        private static void ConsensusReached(int delegateId)
        {
            Block speakerBlock;
            if (delegateInitializationPool.TryGetValue(speakerId, out speakerBlock) == false)
            {
                // block is not in memory of delegate -> new or faulty delegate -> should not be part of consensus
                throw new Exception($"Missing block in ConsensusReached for delegate #{delegateId}");
            }

            var blocks = delegateNegotiationPool.Values.Where(b => b.i != speakerId).ToList();
            if (blocks.Count != delegateIds.Count - 1 ||
                blocks.Count < 3 ||
                blocks.All(b => delegateIds.Contains(b.i)) == false)
            {
                // something went wrong
                throw new Exception($"Got {blocks.Count} blocks and {delegateIds.Count} delegates");
            }

            var speakerVotesCount = blocks
                .Count(b => b.k == speakerBlock.k);

            if (speakerVotesCount >= ((delegateIds.Count - 1) * 2 / 3))
            {
                PropagateConsensus(speakerBlock);

                Interlocked.Exchange(ref consensusState, (int)ConsensusStates.SignedBlock);
            }
            else
            {
                RestartConsensus(speakerBlock);
            }
        }

        private static void PropagateConsensus(Block speakerBlock)
        {
            // consensus reached
            SignBlock(speakerBlock, delegateIds.Sum() - speakerId);
            Console.WriteLine($"The consensus was reached on block #{speakerBlock.id} (h: {speakerBlock.h}, k: {speakerBlock.k}, tx: {speakerBlock.txs.Count})");

            // consensus reached -> send signed block 
            EmulateNetworkLatency();
        }

        private static void AppendBlockToLedger(int delegateId)
        {
            if (speakerId != delegateId)
            {
                // only speaker can do it
                return;
            }

            Block speakerBlock;
            if (delegateInitializationPool.TryGetValue(speakerId, out speakerBlock) == false)
            {
                // block is not in memory of delegate -> new or faulty delegate -> cancel consensus
                EndConsesus();
                throw new Exception($"Missing block in AppendBlockToLedger for delegate #{delegateId}");
            }

            if (speakerBlock.sig == GetSig(delegateIds.Sum() - speakerId))
            {
                Block block = null;
                delegateAccumulationPool.TryRemove(delegateId, out block);
                EndConsesus();
                Console.WriteLine($"The consesus was reached with the speaker #{speakerId} and the block added to the ledger.");
                consensusIntervals.AddOrUpdate(speakerBlock.id, new Interval(), (_, i) =>
                {
                    i.to = DateTime.Now;
                    return i;
                });
            }
            else
            {
                RestartConsensus(speakerBlock);
            }
        }

        private static void RestartConsensus(Block speakerBlock)
        {
            Console.WriteLine($"The consensus was NOT reached yet on block #{speakerBlock.id} (h: {speakerBlock.h}, k: {speakerBlock.k}, tx: {speakerBlock.txs.Count})");

            foreach (var block in delegateInitializationPool.Values)
            {
                block.k = speakerBlock.k + 1;
            }

            // next round -> increase k for everybody
            EmulateNetworkLatency();

            Interlocked.Exchange(ref consensusState, (int)ConsensusStates.SendInitialView);
        }

        private static void SignBlock(Block block, int num)
        {
            block.sig = GetSig(num);
        }

        private static int GetSig(int num)
        {
            return num ^ 397;
        }

        private static Block CopyBlock(Block block, int delegateId)
        {
            var blockCopy = new Block(block.h);

            blockCopy.id = block.id;
            blockCopy.k = block.k;
            blockCopy.i = delegateId;
            blockCopy.txs = block.txs.ToList();
            blockCopy.sig = block.sig;

            return blockCopy;
        }

        private static bool IsProposalValid()
        {
            return rnd.Next(100) % 11 != 1;
        }

        private static void EndConsesus()
        {
            delegateInitializationPool.Clear();
            delegateNegotiationPool.Clear();
            Interlocked.Exchange(ref delegateResponsesCount, 0);
            Interlocked.Exchange(ref consensusState, (int)ConsensusStates.NotStarted);
        }
    }
}

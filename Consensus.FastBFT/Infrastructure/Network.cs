using System;
using System.Threading;

namespace Consensus.FastBFT.Infrastructure
{
    public class Network
    {
        private const int minNetworkLatency = 10;
        private const int maxNetworkLatency = 100;

        static Random rnd = new Random(Environment.TickCount);

        public static void EmulateLatency()
        {
            Thread.Sleep(rnd.Next(minNetworkLatency, maxNetworkLatency));
        }
    }
}

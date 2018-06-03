using System;
using System.Threading;

namespace Consensus.FastBFT.Infrastructure
{
    public class Network
    {
        private static Random rnd = new Random(Environment.TickCount);

        public static int MinNetworkLatency = 10;
        public static int MaxNetworkLatency = 100;

        public static void EmulateLatency()
        {
            Thread.Sleep(rnd.Next(MinNetworkLatency, MaxNetworkLatency));
        }
    }
}

using System;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public abstract class Handler
    {
        public static void Log(ReplicaBase replica, string message, params object[] args)
        {
            Console.WriteLine($"Replica #{replica.Id}: {string.Format(message, args)}");
        }
    }
}
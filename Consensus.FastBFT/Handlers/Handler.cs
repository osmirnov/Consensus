using System;

namespace Consensus.FastBFT.Handlers
{
    public abstract class Handler
    {
        public static void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
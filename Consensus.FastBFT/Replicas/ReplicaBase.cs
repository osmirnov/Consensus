using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Consensus.FastBFT.Messages;

namespace Consensus.FastBFT.Replicas
{
    public abstract class ReplicaBase
    {
        private readonly ConcurrentQueue<Message> messageBus = new ConcurrentQueue<Message>();

        protected readonly string PrivateKey;

        protected readonly List<int[]> Blockchain = new List<int[]>();

        public int Id { get; }
        public ReplicaBase ParentReplica { get; set; }
        public IList<ReplicaBase> ChildReplicas { get; } = new List<ReplicaBase>(2);

        protected ReplicaBase(int id)
        {
            Id = id;
            PrivateKey = Guid.NewGuid().ToString("N");
            PublicKey = PrivateKey;
        }

        public string PublicKey { get; }

        public void SendMessage(Message message)
        {
            messageBus.Enqueue(message);
        }

        public Message ReceiveMessage()
        {
            Message message;

            return messageBus.TryDequeue(out message) ? message : null;
        }

        public Message PeekMessage()
        {
            Message message;

            return messageBus.TryPeek(out message) ? message : null;
        }
    }
}
﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Consensus.FastBFT.Handlers;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Tees;

namespace Consensus.FastBFT.Replicas
{
    public class Replica : ReplicaBase
    {
        public Tee Tee { get; }
        public PrimaryReplica PrimaryReplica { get; set; }

        public Replica(int id, bool isActive) : base(id)
        {
            Tee = new Tee(PrivateKey, PublicKey) { IsActive = isActive };
        }

        public void Run(CancellationToken cancellationToken)
        {
            // process messages
            Task.Factory.StartNew(() =>
            {
                var replicaSecret = new byte[0];
                var block = new int[0];
                var replicaSecretShare = string.Empty;
                var secretHash = default(uint);
                var childSecretHashes = new Dictionary<int, uint>();
                var verifiedChildShareSecrets = new ConcurrentDictionary<int, string>();
                var secretShareMessageTokenSources = new Dictionary<int, CancellationTokenSource>();
                var signedHashAndCounterViewNumber = new byte[0];

                Log("Running...");

                while (cancellationToken.IsCancellationRequested == false)
                {
                    var message = ReceiveMessage();
                    if (message == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var preprocessingMessage = message as PreprocessingMessage;
                    if (preprocessingMessage != null)
                    {
                        PreprocessingHandler.Handle(preprocessingMessage, out replicaSecret);
                    }

                    var prepareMessage = message as PrepareMessage;
                    if (prepareMessage != null)
                    {
                        PrepareHandler.Handle(
                            prepareMessage,
                            Tee,
                            replicaSecret,
                            childSecretHashes,
                            this,
                            out block,
                            out replicaSecretShare,
                            out secretHash,
                            secretShareMessageTokenSources
                        );
                    }

                    var secretShareMessage = message as SecretShareMessage;
                    if (secretShareMessage != null)
                    {
                        SecretShareHandler.Handle(
                            secretShareMessage,
                            this,
                            replicaSecretShare,
                            childSecretHashes,
                            secretShareMessageTokenSources,
                            verifiedChildShareSecrets);
                    }

                    var commitMessage = message as CommitMessage;
                    if (commitMessage != null)
                    {
                        CommitHandler.Handle(
                            commitMessage,
                            this,
                            secretHash,
                            block,
                            replicaSecret,
                            secretShareMessageTokenSources);
                    }

                    var newViewMessage = message as NewViewMessage;
                    if (newViewMessage != null)
                    {
                        NewViewHandler.Handle(
                            newViewMessage,
                            this,
                            out signedHashAndCounterViewNumber);
                    }
                }

                Log("Stopped.");
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void Log(string message)
        {
            Console.WriteLine($"Replica #{Id}: {message}");
        }
    }
}

﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Messages;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Handlers
{
    public class PrimarySecretShareHandler : Handler
    {
        public static void Handle(
            SecretShareMessage message,
            PrimaryReplica primaryReplica,
            IEnumerable<ReplicaBase> activeRelicas,
            int[] block,
            int nextBlockIndex,
            ref bool isCommitted,
            byte[] signedSecretHashAndCounterViewNumber,
            ConcurrentDictionary<int, string> verifiedChildShareSecrets)
        {
            var childReplicaId = message.ReplicaId;
            var childSecretShare = message.ReplicaSecretShares;

            foreach (var childReplicaShare in message.ReplicaSecretShares)
            {
                // TODO: hashes of primary children should be checked as well
                verifiedChildShareSecrets.TryAdd(childReplicaShare.Key, childReplicaShare.Value);
            }

            if (verifiedChildShareSecrets.Keys.OrderBy(_ => _)
                    .SequenceEqual(activeRelicas.Select(r => r.Id).OrderBy(_ => _)) == false)
            {
                return;
            }

            var verifiedSecretShares = verifiedChildShareSecrets
                .Select(x => new { ReplicaId = x.Key, SecretShare = x.Value })
                .OrderBy(x => x.ReplicaId)
                .Select(x => x.SecretShare)
                .ToList();

            var secret = string.Join(string.Empty, verifiedSecretShares);

            uint secretHash;
            uint counter;
            uint viewNumber;

            primaryReplica.Tee.GetHashAndCounterViewNumber(
                primaryReplica.PublicKey,
                signedSecretHashAndCounterViewNumber,
                out secretHash,
                out counter,
                out viewNumber);

            if (Crypto.GetHash(secret + counter + viewNumber) != secretHash)
            {
                return;
            }

            var request = string.Join(string.Empty, block);
            var commitResult = nextBlockIndex;
            var commitResultHash = Crypto.GetHash(request) | (uint)nextBlockIndex;

            var signedCommitResultHashCounterViewNumber = primaryReplica.Tee.RequestCounter(commitResultHash);

            if (!isCommitted)
            {
                Log("All ready to commit.");

                Network.EmulateLatency();

                foreach (var activeRelica in activeRelicas)
                {
                    activeRelica.SendMessage(new CommitMessage
                    {
                        Secret = secret,
                        CommitResult = commitResult,
                        CommitResultHashCounterViewNumber = signedCommitResultHashCounterViewNumber
                    });
                }

                isCommitted = true;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Consensus.FastBFT.Tees
{
    public class PrimaryTee : Tee
    {
        // primary replica -> current active replicas and their view keys
        private readonly Dictionary<int, byte> replicaViewKeys = new Dictionary<int, byte>();
        private readonly IReadOnlyDictionary<int, int[]> activeReplicas;

        public PrimaryTee(IReadOnlyDictionary<int, int[]> activeReplicas)
        {
            isActive = true;
            latestCounter = 0;
            viewNumber++;
            this.activeReplicas = activeReplicas;

            foreach (var activeReplicaId in activeReplicas.Keys)
            {
                var secretViewKey = (byte)(new Random(Environment.TickCount)).Next(byte.MaxValue);
                var encryptedViewKey = Crypto.Encrypt(secretViewKey.ToString());

                replicaViewKeys.Add(activeReplicaId, secretViewKey);
            }
        }

        // primary replica
        public IDictionary<string, IDictionary<int, byte[]>> Preprocessing(int counterValuesCount)
        {
            var result = new Dictionary<string, IDictionary<int, byte[]>>(counterValuesCount - 1);

            for (uint i = 1; i <= counterValuesCount; i++)
            {
                var counter = latestCounter + i;
                // generate secret
                var secret = Guid.NewGuid().ToString();
                var secretHash = Crypto.GetHash(secret + counter + viewNumber);
                var activeReplicasCount = activeReplicas.Count;
                var secretShareLength = secret.Length / activeReplicasCount;
                var secretShares = Enumerable
                    .Range(0, activeReplicasCount)
                    .Select(j => j != activeReplicasCount - 1
                        ? secret.Substring(j * secretShareLength, secretShareLength)
                        : secret.Substring(j * secretShareLength))
                    .ToArray();

                var encryptedReplicaSecrets = new Dictionary<int, byte[]>();

                foreach (var activeReplica in activeReplicas)
                {
                    ShareSecretAmongReplicas(
                        activeReplica,
                        secretShares,
                        counter,
                        secretHash,
                        encryptedReplicaSecrets);
                }

                var signedSecretHash = Crypto.Sign(secretHash.ToString() + counter + viewNumber);

                result.Add(signedSecretHash, encryptedReplicaSecrets);
            }

            return result;
        }

        public string RequestCounter(uint x)
        {
            latestCounter++;
            return Crypto.Sign(x.ToString() + latestCounter + viewNumber);
        }

        private void ShareSecretAmongReplicas(
            KeyValuePair<int, int[]> replica,
            IReadOnlyList<string> secretShares,
            uint counter,
            uint secretHash,
            IDictionary<int, byte[]> encryptedReplicaSecrets)
        {
            var replicaId = replica.Key;
            var childReplicaIds = replica.Value;
            var childrenSecretHashes = childReplicaIds
                .ToDictionary(
                    rid => rid,
                    rid =>
                    {
                        var childSecretShares = new[] { secretShares[rid] }
                            .Concat(activeReplicas[rid].OrderBy(chrid => chrid).Select(chrid => secretShares[chrid]))
                            .ToArray();

                        return Crypto.GetHash(string.Join(string.Empty, childSecretShares));
                    });

            var secretShare = secretShares[replicaId];
            byte[] replicaSecret;

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(secretShare.Length);
                writer.Write(secretShare);
                writer.Write(counter);
                writer.Write(viewNumber);

                writer.Write(childrenSecretHashes.Count);
                foreach (var childrenSecretHash in childrenSecretHashes)
                {
                    writer.Write(childrenSecretHash.Key);
                    writer.Write(childrenSecretHash.Value);
                }

                writer.Write(secretHash);

                replicaSecret = memory.ToArray();
            }

            encryptedReplicaSecrets.Add(replicaId, Crypto.EncryptAuth(replicaSecret, replicaViewKeys[replicaId]));
        }
    }
}

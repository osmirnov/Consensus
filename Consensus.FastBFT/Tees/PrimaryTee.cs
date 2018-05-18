using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Tees
{
    public class PrimaryTee : Tee
    {
        static Random rnd = new Random(Environment.TickCount);

        // primary replica -> current active replicas and their view keys
        private Dictionary<int, byte> replicaViewKeys = new Dictionary<int, byte>();
        private PrimaryReplica primaryReplica;

        public PrimaryTee(PrimaryReplica primaryReplica)
        {
            isActive = true;
            counterLatest = 0;
            viewNumber++;
            this.primaryReplica = primaryReplica;

            foreach (var replica in GetReplicas(primaryReplica))
            {
                var viewKey = (byte)rnd.Next(byte.MaxValue);
                var encryptedViewKey = Crypto.Encrypt(viewKey.ToString());

                replicaViewKeys.Add(replica.id, viewKey);
            }
        }

        public Replica[] GetReplicas(Replica parentReplica)
        {
            return parentReplica.childReplicas
                .SelectMany(r => GetReplicas(r))
                .Concat(new[] { parentReplica })
                .Except(new[] { primaryReplica })
                .OrderBy(r => r.id)
                .ToArray();
        }

        // primary replica
        public IDictionary<string, IDictionary<int, byte[]>> Preprocessing(int counterValuesCount)
        {
            var result = new Dictionary<string, IDictionary<int, byte[]>>(counterValuesCount - 1);

            for (uint i = 1; i <= counterValuesCount; i++)
            {
                var counter = counterLatest + i;
                // generate secret
                var secret = Guid.NewGuid().ToString();
                var secretHash = Crypto.GetHash(secret + counter + viewNumber);
                var replicas = GetReplicas(primaryReplica);
                var replicasCount = replicas.Count();
                var secretShareLength = secret.Length / replicasCount;
                var secretShares = Enumerable
                    .Range(0, replicasCount)
                    .Select(j => j != replicasCount - 1
                        ? secret.Substring(j * secretShareLength, secretShareLength)
                        : secret.Substring(j * secretShareLength))
                    .ToArray();

                var encryptedReplicaSecrets = new Dictionary<int, byte[]>();

                foreach (var replica in replicas)
                {
                    DistributeSecretAmongReplicas(
                        replica,
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
            counterLatest++;
            return Crypto.Sign(x.ToString() + counterLatest + viewNumber);
        }

        private void DistributeSecretAmongReplicas(
            Replica replica,
            string[] secretShares,
            uint counter,
            uint secretHash,
            IDictionary<int, byte[]> encryptedReplicaSecrets)
        {
            var childrenSecretHashes = replica.childReplicas
                .ToDictionary(
                r => r.id,
                r =>
                {
                    var childrenSecretShares = GetChildrenSecretShares(r, secretShares);

                    return Crypto.GetHash(string.Join(string.Empty, childrenSecretShares));
                });

            var secretShare = secretShares[replica.id];
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

            encryptedReplicaSecrets.Add(replica.id, Crypto.EncryptAuth(replicaSecret, replicaViewKeys[replica.id]));
        }

        private string[] GetChildrenSecretShares(Replica replica, string[] secretShares)
        {
            return new[] { secretShares[replica.id] }
                .Concat(replica.childReplicas.SelectMany(r => GetChildrenSecretShares(r, secretShares)))
                .ToArray();
        }
    }
}

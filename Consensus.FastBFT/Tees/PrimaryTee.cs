using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Tees
{
    class PrimaryTee : Tee
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
                var encryptedViewKey = Encrypt(viewKey.ToString());

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
                var secretHash = GetHash(secret + counter + viewNumber);
                var replicasCount = replicaViewKeys.Count();
                var secretShareLength = secret.Length / replicasCount;
                var secretShares = Enumerable
                    .Range(0, replicasCount - 1)
                    .Select(j => j != replicasCount - 1
                        ? secret.Substring(j * secretShareLength, secretShareLength)
                        : secret.Substring(j * secretShareLength))
                    .ToArray();

                var encryptedReplicaSecrets = new Dictionary<int, byte[]>();

                foreach (var childReplica in primaryReplica.childReplicas)
                {
                    DistributeSecretAmongReplicas(
                        childReplica,
                        secretShares,
                        counter,
                        secretHash,
                        encryptedReplicaSecrets);
                }

                var signedSecretHash = Sign(secretHash.ToString() + counter + viewNumber);

                result.Add(signedSecretHash, encryptedReplicaSecrets);
            }

            return result;
        }

        public string RequestCounter(uint x)
        {
            counterLatest++;
            return Sign(x.ToString() + counterLatest + viewNumber);
        }

        private void DistributeSecretAmongReplicas(
            Replica replica,
            string[] secretShares,
            uint counter,
            uint secretHash,
            IDictionary<int, byte[]> encryptedReplicaSecrets)
        {
            var childSecretHashes = replica.childReplicas
                .Select(r => string.Join(string.Empty, GetChildrenSecretShares(r, secretShares)));

            var secretShare = secretShares[replica.id];
            var childrenSecretHash = GetHash(string.Join(string.Empty, childSecretHashes));
            byte[] replicaSecret;

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(secretShare.Length);
                writer.Write(secretShare);
                writer.Write(counter);
                writer.Write(viewNumber);
                writer.Write(childrenSecretHash);
                writer.Write(secretHash);

                replicaSecret = memory.ToArray();
            }

            encryptedReplicaSecrets.Add(replica.id, EncryptAuth(replicaSecret, replicaViewKeys[replica.id]));

            foreach (var childReplica in replica.childReplicas)
            {
                DistributeSecretAmongReplicas(childReplica, secretShares, counter, secretHash, encryptedReplicaSecrets);
            }
        }

        private string[] GetChildrenSecretShares(Replica replica, string[] secretShares)
        {
            return new[] { secretShares[replica.id] }
                .Concat(replica.childReplicas.SelectMany(r => GetChildrenSecretShares(r, secretShares)))
                .ToArray();
        }
    }
}

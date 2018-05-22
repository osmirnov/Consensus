using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Consensus.FastBFT.Infrastructure;
using Consensus.FastBFT.Replicas;

namespace Consensus.FastBFT.Tees
{
    public class PrimaryTee : Tee
    {
        // primary replica -> current active replicas and their view keys
        private readonly Dictionary<int, byte> replicaViewKeys = new Dictionary<int, byte>();
        private PrimaryReplica primaryReplica;
        private IReadOnlyCollection<Replica> activeReplicas;

        public IReadOnlyDictionary<int, string> Initialize(PrimaryReplica primaryReplica, IReadOnlyCollection<Replica> activeReplicas)
        {
            IsActive = true;
            LatestCounter = 0;
            ViewNumber++;

            this.primaryReplica = primaryReplica;
            this.activeReplicas = activeReplicas;

            var encryptedViewKeys = new Dictionary<int, string>();

            foreach (var activeReplica in activeReplicas)
            {
                var secretViewKey = (byte)(new Random(Environment.TickCount)).Next(byte.MaxValue);
                replicaViewKeys.Add(activeReplica.Id, secretViewKey);

                var encryptedViewKey = Crypto.Encrypt(activeReplica.PublicKey, secretViewKey.ToString());
                encryptedViewKeys.Add(activeReplica.Id, encryptedViewKey);
            }

            return encryptedViewKeys;
        }

        // primary replica
        public IDictionary<string, IDictionary<int, byte[]>> Preprocessing(int counterValuesCount)
        {
            var result = new Dictionary<string, IDictionary<int, byte[]>>(counterValuesCount - 1);

            for (uint i = 1; i <= counterValuesCount; i++)
            {
                var counter = LatestCounter + i;
                // generate secret
                var secret = Guid.NewGuid().ToString();
                var secretHash = Crypto.GetHash(secret + counter + ViewNumber);
                var activeReplicasCount = activeReplicas.Count;
                var secretShareLength = secret.Length / activeReplicasCount;
                var secretShares = activeReplicas
                    .ToDictionary(
                        r => r.Id,
                        _ =>
                        {
                            var secretShare = secret.Substring(0, Math.Min(secretShareLength, secret.Length));

                            secret = secret.Substring(secretShare.Length - 1);

                            return secretShare;
                        });

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

                var signedSecretHash = Crypto.Sign(primaryReplica.PrivateKey, secretHash.ToString() + counter + ViewNumber);

                result.Add(signedSecretHash, encryptedReplicaSecrets);
            }

            return result;
        }

        public string RequestCounter(uint x)
        {
            LatestCounter++;
            return Crypto.Sign(primaryReplica.PrivateKey, x.ToString() + LatestCounter + ViewNumber);
        }

        private void ShareSecretAmongReplicas(
            Replica replica,
            IReadOnlyDictionary<int, string> secretShares,
            uint counter,
            uint secretHash,
            IDictionary<int, byte[]> encryptedReplicaSecrets)
        {
            var childrenSecretHashes = replica.ChildReplicas
                .OrderBy(chr => chr.Id)
                .ToDictionary(
                    chr => chr.Id,
                    chr =>
                    {
                        var childSecretShares = new[] { secretShares[chr.Id] }
                            .Concat(chr.ChildReplicas.Select(chr2 => secretShares[chr2.Id]))
                            .ToArray();

                        return Crypto.GetHash(string.Join(string.Empty, childSecretShares));
                    });

            byte[] replicaSecret;
            var secretShare = secretShares[replica.Id];

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(secretShare.Length);
                writer.Write(secretShare);
                writer.Write(counter);
                writer.Write(ViewNumber);

                writer.Write(childrenSecretHashes.Count);
                foreach (var childrenSecretHash in childrenSecretHashes)
                {
                    writer.Write(childrenSecretHash.Key);
                    writer.Write(childrenSecretHash.Value);
                }

                writer.Write(secretHash);

                replicaSecret = memory.ToArray();
            }

            encryptedReplicaSecrets.Add(replica.Id, Crypto.EncryptAuth(replicaViewKeys[replica.Id], replicaSecret));
        }
    }
}

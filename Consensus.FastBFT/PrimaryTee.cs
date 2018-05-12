using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Consensus.FastBFT
{
    class PrimaryTee : Tee
    {
        public Dictionary<int, byte> replicaViewKeys =
            new Dictionary<int, byte>(); // primary replica -> current active replicas and their view keys
        public TreeNode tree; // primary replica -> current tree structure

        static Random rnd = new Random(Environment.TickCount);

        public PrimaryTee(TreeNode tree)
        {
            isActive = true;
            counterLatest = 0;
            viewNumber++;
            this.tree = tree;

            foreach (var replica in GetReplicas(tree))
            {
                var viewKey = (byte)rnd.Next(byte.MaxValue);
                var encryptedViewKey = Encrypt(viewKey.ToString());

                replicaViewKeys.Add(replica, viewKey);
            }
        }

        private IEnumerable<int> GetReplicas(TreeNode tree)
        {
            var leftReplicas = tree.leftNode != null ? GetReplicas(tree.leftNode) : new[] { tree.replica };
            var rightReplicas = tree.rightNode != null ? GetReplicas(tree.rightNode) : new[] { tree.replica };

            return leftReplicas
                .Concat(rightReplicas)
                .Distinct();
        }

        // primary replica
        public IDictionary<string, List<byte[]>> Preprocessing(int counterValuesCount)
        {
            var result = new Dictionary<string, List<byte[]>>(counterValuesCount - 1);

            for (uint i = 1; i <= counterValuesCount; i++)
            {
                var counter = counterLatest + i;
                // generate secret
                var secret = Guid.NewGuid().ToString();
                var secretHash = GetHash(secret + counter + viewNumber);
                var replicasCount = GetReplicas(tree).Count();
                var secretShareLength = secret.Length / replicasCount;
                var secretShares = Enumerable
                    .Range(0, replicasCount - 1)
                    .Select(j => j != replicasCount - 1
                        ? secret.Substring(j * secretShareLength, secretShareLength)
                        : secret.Substring(j * secretShareLength))
                    .ToArray();

                var encryptedReplicaSecrets = new List<byte[]>();

                DistributeSecretAmongReplicas(
                    tree,
                    secretShares,
                    counter,
                    secretHash,
                    encryptedReplicaSecrets);

                var signedSecretHash = Sign(secretHash + counter + viewNumber);

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
            TreeNode tree,
            string[] secretShares,
            uint counter,
            string secretHash,
            List<byte[]> encryptedReplicaSecrets)
        {
            var childrenSecretHashes = new List<string>();

            if (tree.leftNode != null)
            {
                var leftChildrenSecretShares = GetChildrenSecretShares(tree.leftNode, secretShares);
                var leftChildrenSecret = string.Join(string.Empty, leftChildrenSecretShares);
                childrenSecretHashes.Add(GetHash(leftChildrenSecret));
            }

            if (tree.rightNode != null)
            {
                var rightChildrenSecretShares = GetChildrenSecretShares(tree.rightNode, secretShares);
                var rightChildrenSecret = string.Join(string.Empty, rightChildrenSecretShares);
                childrenSecretHashes.Add(GetHash(rightChildrenSecret));
            }

            var secretShare = secretShares[tree.replica];
            var childrenSecretHash = string.Join(string.Empty, childrenSecretHashes);
            byte[] replicaSecret;

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(secretShare.Length);
                writer.Write(secretShare);
                writer.Write(counter);
                writer.Write(viewNumber);
                writer.Write(childrenSecretHash.Length);
                writer.Write(childrenSecretHash);
                writer.Write(secretHash.Length);
                writer.Write(secretHash);

                replicaSecret = memory.ToArray();
            }

            encryptedReplicaSecrets.Add(EncryptAuth(replicaSecret, replicaViewKeys[tree.replica]));

            if (tree.leftNode != null)
            {
                DistributeSecretAmongReplicas(tree.leftNode, secretShares, counter, secretHash, encryptedReplicaSecrets);
            }

            if (tree.rightNode != null)
            {
                DistributeSecretAmongReplicas(tree.rightNode, secretShares, counter, secretHash, encryptedReplicaSecrets);
            }
        }

        private string[] GetChildrenSecretShares(TreeNode tree, string[] secretHashShares)
        {
            var leftChildrenSecretHashShares = tree.leftNode != null ? GetChildrenSecretShares(tree.leftNode, secretHashShares) : new string[0];
            var rightChildrenSecretHashShares = tree.rightNode != null ? GetChildrenSecretShares(tree.rightNode, secretHashShares) : new string[0];

            return new[] { secretHashShares[tree.replica] }
                .Concat(leftChildrenSecretHashShares)
                .Concat(rightChildrenSecretHashShares)
                .ToArray();
        }
    }
}

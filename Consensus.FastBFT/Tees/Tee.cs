using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Consensus.FastBFT.Infrastructure;

namespace Consensus.FastBFT.Tees
{
    public class Tee
    {
        public Crypto Crypto { get; private set; }

        public uint latestCounter;  // all replicas -> latest counter
        public uint viewNumber;     // all replicas -> current view number
        public byte viewKey;        // active replica -> current view key agreed with the primary
        public bool isActive;

        public Tee()
        {
            // fake public/private keys
            var key = Guid.NewGuid().ToString("N");
            Crypto = new Crypto(key, key);
        }

        // non primary replica
        // signedCounterViewNumber <- by primary replica
        public void UpdateView(string signedCounterViewNumber, string encryptedViewKey)
        {
            byte[] buffer;
            if (Crypto.Verify(signedCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            var counter = BitConverter.ToUInt32(buffer, 0);
            var viewNumber = BitConverter.ToUInt32(buffer, 4);

            if (counter != latestCounter + 1) throw new Exception("Invalid counter");

            latestCounter = 0;
            viewNumber++;
            if (isActive) viewKey = byte.Parse(Crypto.Decrypt(encryptedViewKey));
        }

        // used by active replicas
        public void VerifyCounter(
            string signedXCounterViewNumber,
            byte[] encryptedReplicaSecret,
            out string secretShare,
            out Dictionary<int, uint> childrenSecretHashes,
            out uint secretHash)
        {
            byte[] buffer;
            if (Crypto.Verify(signedXCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            var counter = BitConverter.ToUInt32(buffer, 4);
            var viewNumber = BitConverter.ToUInt32(buffer, 8);

            using (var memory = new MemoryStream(Crypto.DecryptAuth(encryptedReplicaSecret, viewKey)))
            using (var reader = new BinaryReader(memory))
            {
                secretShare = reader.ReadString();
                var counter2 = reader.ReadUInt32();
                var viewNumber2 = reader.ReadUInt32();

                var childrenSecretHashesCount = reader.ReadInt32();
                childrenSecretHashes = new Dictionary<int, uint>(childrenSecretHashesCount);
                for (int i = 0; i < childrenSecretHashesCount; i++)
                {
                    childrenSecretHashes.Add(reader.ReadInt32(), reader.ReadUInt32());
                }

                secretHash = reader.ReadUInt32();

                if (counter != counter2 || viewNumber == viewNumber2) throw new Exception("Invalid counter value");
                if (counter != latestCounter + 1) throw new Exception("Invalid counter value");

                latestCounter++;
            }
        }

        // used by passive replicas
        public void UpdateCounter(string secret, string signedSecretHashCounterViewNumber)
        {
            byte[] buffer;
            if (Crypto.Verify(signedSecretHashCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var secretHash = reader.ReadUInt32();
                var counter = reader.ReadUInt32();
                var viewNumber = reader.ReadUInt32();

                if (counter != latestCounter + 1) throw new Exception("Invalid counter value");
                if (Crypto.GetHash(secret + counter + viewNumber) != secretHash) throw new Exception("Invalid secret");

                latestCounter++;
            }
        }

        // by replica
        public void ResetCounter(IDictionary<string, string> lAndSignedLHashCounterViewNumbers)
        {
            // Number of faulty replicas
            const int f = 2 + 1;

            var atLeastFPlus1ConsistentL = lAndSignedLHashCounterViewNumbers
                .Select(lAndSignedLHashCounterViewNumber =>
                {
                    byte[] buffer;
                    if (Crypto.Verify(lAndSignedLHashCounterViewNumber.Value, out buffer) == false) throw new Exception("Invalid signature");

                    using (var memory = new MemoryStream(buffer))
                    using (var reader = new BinaryReader(memory))
                    {
                        var LHash = reader.ReadString();
                        var counter = reader.ReadUInt32();
                        var viewNumber = reader.ReadUInt32();

                        return new
                        {
                            L = lAndSignedLHashCounterViewNumber.Key,
                            counter,
                            viewNumber
                        };
                    }
                })
                .GroupBy(x => x.L + x.counter + x.viewNumber)
                .Where(gx => gx.Count() >= f)
                .OrderByDescending(gx => gx.Count())
                .First().First();

            if (atLeastFPlus1ConsistentL != null)
            {
                latestCounter = atLeastFPlus1ConsistentL.counter;
                viewNumber = atLeastFPlus1ConsistentL.viewNumber;
            }
        }
    }
}

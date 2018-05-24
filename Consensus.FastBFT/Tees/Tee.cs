using System;
using System.Collections.Generic;
using System.IO;
using Consensus.FastBFT.Infrastructure;

namespace Consensus.FastBFT.Tees
{
    public class Tee
    {
        private readonly string privateKey;
        private readonly string publicKey;

        public uint LatestCounter { get; protected set; }  // all replicas -> latest counter
        public uint ViewNumber { get; protected set; }     // all replicas -> current view number
        public byte ViewKey { get; protected set; }        // active replica -> current view key agreed with the primary
        public bool IsActive { get; set; }

        public Tee(string privateKey, string publicKey)
        {
            this.privateKey = privateKey;
            this.publicKey = publicKey;
        }

        // non primary replica
        // signedByPrimaryReplicaHashAndCounterViewNumber <- by primary replica
        public void UpdateView(
            string primaryReplicaPublicKey,
            byte[] signedByPrimaryReplicaHashAndCounterViewNumber,
            string replicaPrivateKey,
            string encryptedViewKey)
        {
            uint hash;
            uint counter;
            uint viewNumber;

            GetHashAndCounterViewNumber(
                primaryReplicaPublicKey,
                signedByPrimaryReplicaHashAndCounterViewNumber,
                out hash,
                out counter,
                out viewNumber);

            if (counter != LatestCounter + 1) throw new Exception("Invalid counter");

            LatestCounter = 0;
            ViewNumber++;
            if (IsActive) ViewKey = byte.Parse(Crypto.Decrypt(replicaPrivateKey, encryptedViewKey));
        }

        // used by active replicas
        public void VerifyCounter(
            string primaryReplicaPublicKey,
            byte[] signedByPrimaryReplicaHashAndCounterViewNumber,
            byte[] encryptedReplicaSecret,
            out string secretShare,
            out Dictionary<int, uint> childrenSecretHashes,
            out uint secretHash)
        {
            uint hash;
            uint counter;
            uint viewNumber;

            GetHashAndCounterViewNumber(
                primaryReplicaPublicKey,
                signedByPrimaryReplicaHashAndCounterViewNumber,
                out hash,
                out counter,
                out viewNumber);

            var replicaSecret = Crypto.DecryptAuth(ViewKey, encryptedReplicaSecret);

            using (var memory = new MemoryStream(replicaSecret))
            using (var reader = new BinaryReader(memory))
            {
                secretShare = reader.ReadString();
                var replicaCounter = reader.ReadUInt32();
                var replicaViewNumber = reader.ReadUInt32();

                var childrenSecretHashesCount = reader.ReadInt32();
                childrenSecretHashes = new Dictionary<int, uint>(childrenSecretHashesCount);
                for (var i = 0; i < childrenSecretHashesCount; i++)
                {
                    childrenSecretHashes.Add(reader.ReadInt32(), reader.ReadUInt32());
                }

                secretHash = reader.ReadUInt32();

                if (counter != replicaCounter || viewNumber == replicaViewNumber) throw new Exception("Invalid counter value");
                if (counter != LatestCounter + 1) throw new Exception("Invalid counter value");

                LatestCounter++;
            }
        }

        public byte[] RequestCounter(uint hash)
        {
            LatestCounter++;

            byte[] buffer;

            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(hash);
                writer.Write(LatestCounter);
                writer.Write(ViewNumber);

                buffer = memory.ToArray();
            }

            return Crypto.Sign(privateKey, buffer);
        }

        // used by passive replicas
        public void UpdateCounter(
            string primaryReplicaPublicKey,
            byte[] signedByPrimaryReplicaHashAndCounterViewNumber,
            string secret)
        {
            uint hash;
            uint counter;
            uint viewNumber;

            GetHashAndCounterViewNumber(
                primaryReplicaPublicKey,
                signedByPrimaryReplicaHashAndCounterViewNumber,
                out hash,
                out counter,
                out viewNumber);

            if (counter != LatestCounter + 1) throw new Exception("Invalid counter value");
            if (Crypto.GetHash(secret + counter + viewNumber) != hash) throw new Exception("Invalid secret");

            LatestCounter++;
        }

        // by replica
        //public void ResetCounter(IDictionary<string, string> lAndSignedLHashCounterViewNumbers)
        //{
        //    // Number of faulty replicas
        //    const int f = 2 + 1;

        //    var atLeastFPlus1ConsistentL = lAndSignedLHashCounterViewNumbers
        //        .Select(lAndSignedLHashCounterViewNumber =>
        //        {
        //            byte[] buffer;
        //            if (Crypto.Verify(lAndSignedLHashCounterViewNumber.Value, out buffer) == false) throw new Exception("Invalid signature");

        //            using (var memory = new MemoryStream(buffer))
        //            using (var reader = new BinaryReader(memory))
        //            {
        //                var lHash = reader.ReadString();
        //                var counter = reader.ReadUInt32();
        //                var viewNumber = reader.ReadUInt32();

        //                return new
        //                {
        //                    L = lAndSignedLHashCounterViewNumber.Key,
        //                    counter,
        //                    viewNumber
        //                };
        //            }
        //        })
        //        .GroupBy(x => x.L + x.counter + x.viewNumber)
        //        .Where(gx => gx.Count() >= f)
        //        .OrderByDescending(gx => gx.Count())
        //        .First().First();

        //    if (atLeastFPlus1ConsistentL != null)
        //    {
        //        LatestCounter = atLeastFPlus1ConsistentL.counter;
        //        ViewNumber = atLeastFPlus1ConsistentL.viewNumber;
        //    }
        //}

        private void GetHashAndCounterViewNumber(
            string primaryReplicaPublicKey,
            byte[] signedByPrimaryReplicaHashAndCounterViewNumber,
            out uint hash,
            out uint counter,
            out uint viewNumber
        )
        {
            byte[] buffer;
            if (Crypto.Verify(primaryReplicaPublicKey, signedByPrimaryReplicaHashAndCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                hash = reader.ReadUInt32();
                counter = reader.ReadUInt32();
                viewNumber = reader.ReadUInt32();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Consensus.FastBFT.Infrastructure;

namespace Consensus.FastBFT.Tees
{
    public class Tee
    {
        protected readonly string privateKey;

        public uint LatestCounter { get; protected set; }  // all replicas -> latest counter
        public uint ViewNumber { get; protected set; }     // all replicas -> current view number
        public byte ViewKey { get; protected set; }        // active replica -> current view key agreed with the primary
        public bool IsActive { get; set; }

        public Tee()
        {
            privateKey = Guid.NewGuid().ToString("N");
            PublicKey = privateKey;
        }

        public string PublicKey { get; }

        // non primary replica
        // signedByPrimaryReplicaPayloadAndCounterViewNumber <- by primary replica
        public void UpdateView(
            string primaryReplicaPublicKey,
            string replicaPrivateKey,
            byte[] signedByPrimaryReplicaPayloadAndCounterViewNumber,
            string encryptedViewKey)
        {
            byte[] buffer;
            if (Crypto.Verify(primaryReplicaPublicKey, signedByPrimaryReplicaPayloadAndCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            var counter = BitConverter.ToUInt32(buffer, 0);
            var viewNumber = BitConverter.ToUInt32(buffer, 4);

            if (counter != LatestCounter + 1) throw new Exception("Invalid counter");

            LatestCounter = 0;
            viewNumber++;
            if (IsActive) ViewKey = byte.Parse(Crypto.Decrypt(replicaPrivateKey, encryptedViewKey));
        }

        // used by active replicas
        public void VerifyCounter(
            string primaryReplicaPublicKey,
            byte[] signedByPrimaryReplicaPayloadAndCounterViewNumber,
            byte[] encryptedReplicaSecret,
            out string secretShare,
            out Dictionary<int, uint> childrenSecretHashes,
            out uint secretHash)
        {
            byte[] buffer;
            if (Crypto.Verify(primaryReplicaPublicKey, signedByPrimaryReplicaPayloadAndCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            uint counter;
            uint viewNumber;

            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var hash = reader.ReadUInt32();
                counter = reader.ReadUInt32();
                viewNumber = reader.ReadUInt32();
            }

            using (var memory = new MemoryStream(Crypto.DecryptAuth(ViewKey, encryptedReplicaSecret)))
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
                if (counter != LatestCounter + 1) throw new Exception("Invalid counter value");

                LatestCounter++;
            }
        }

        // used by passive replicas
        public void UpdateCounter(
            string primaryReplicaPublicKey,
            string secret,
            byte[] signedByPrimaryReplicaSecretHashAndCounterViewNumber)
        {
            byte[] buffer;
            if (Crypto.Verify(primaryReplicaPublicKey, signedByPrimaryReplicaSecretHashAndCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var secretHash = reader.ReadUInt32();
                var counter = reader.ReadUInt32();
                var viewNumber = reader.ReadUInt32();

                if (counter != LatestCounter + 1) throw new Exception("Invalid counter value");
                if (Crypto.GetHash(secret + counter + viewNumber) != secretHash) throw new Exception("Invalid secret");

                LatestCounter++;
            }
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
    }
}

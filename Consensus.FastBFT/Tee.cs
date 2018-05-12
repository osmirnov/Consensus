using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Consensus.FastBFT
{
    public class Tee
    {
        public readonly string publicKey;
        protected readonly string privateKey;

        public uint counterLatest; // all replicas -> latest counter
        public uint viewNumber; // all replicas -> current view number

        public byte viewKey; // active replica -> current view key agreed with the primary

        public bool isActive;

        public Tee()
        {
            // fake public/private keys
            publicKey = Guid.NewGuid().ToString("N");
            privateKey = publicKey;
        }

        public string Encrypt(string str)
        {
            return publicKey + str;
        }

        public byte[] EncryptAuth(byte[] buffer, byte? viewKey = null)
        {
            return (new byte[] { (viewKey ?? this.viewKey), 0xff })
                .Concat(buffer)
                .ToArray();
        }

        public string Decrypt(string encryptedStr)
        {
            return encryptedStr.Substring(encryptedStr.IndexOf(publicKey), publicKey.Length);
        }

        public byte[] DecryptAuth(byte[] encryptedBuffer, byte? viewKey = null)
        {
            var key = (viewKey ?? this.viewKey);

            if (encryptedBuffer[0] != key) throw new Exception("Invalid encryption");
            if (encryptedBuffer[1] != 0xff) throw new Exception("Invalid encryption");

            return encryptedBuffer.Skip(2).ToArray();
        }

        public string Sign(string str)
        {
            return privateKey + str;
        }

        public bool Verify(string signedStr, out byte[] buffer)
        {
            buffer = Encoding.UTF8.GetBytes(signedStr.Substring(signedStr.IndexOf(publicKey), publicKey.Length));
            return true;
        }

        public string GetHash(string str)
        {
            var buffer = Encoding.UTF8.GetBytes(str);
            return BitConverter.ToString(buffer.Where((_, i) => i % 2 == 0).ToArray());
        }

        // non primary replica
        // signedCounterViewNumber <- by primary replica
        public void UpdateView(string signedCounterViewNumber, string encryptedViewKey)
        {
            byte[] buffer;
            if (Verify(signedCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            var counter = BitConverter.ToUInt32(buffer, 0);
            var viewNumber = BitConverter.ToUInt32(buffer, 4);

            if (counter != counterLatest + 1) throw new Exception("Invalid counter");

            counterLatest = 0;
            viewNumber++;
            if (isActive) viewKey = byte.Parse(Decrypt(encryptedViewKey));
        }

        // used by passive replicas
        public void VerifyCounter(
            string signedXCounterViewNumber,
            byte[] encryptedReplicaSecret,
            out string secretShare,
            out string childrenSecretHash,
            out string secretHash)
        {
            byte[] buffer;
            if (Verify(signedXCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            var counter = BitConverter.ToUInt32(buffer, 4);
            var viewNumber = BitConverter.ToUInt32(buffer, 8);

            using (var memory = new MemoryStream(DecryptAuth(encryptedReplicaSecret)))
            using (var reader = new BinaryReader(memory))
            {
                // var secretShareLength = reader.ReadInt32();
                secretShare = reader.ReadString();
                var counter2 = reader.ReadUInt32();
                var viewNumber2 = reader.ReadUInt32();
                // var childrenSecretHashLength = reader.ReadString();
                childrenSecretHash = reader.ReadString();
                // var secretHashLength = reader.ReadString();
                secretHash = reader.ReadString();

                if (counter != counter2 || viewNumber == viewNumber2) throw new Exception("Invalid counter value");
                if (counter != counterLatest + 1) throw new Exception("Invalid counter value");

                counterLatest++;
            }
        }

        // by passive replicas
        public void UpdateCounter(string secret, string signedSecretHashCounterViewNumber)
        {
            byte[] buffer;
            if (Verify(signedSecretHashCounterViewNumber, out buffer) == false) throw new Exception("Invalid signature");

            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var secretHash = reader.ReadString();
                var counter = reader.ReadUInt32();
                var viewNumber = reader.ReadUInt32();

                if (counter != counterLatest + 1) throw new Exception("Invalid counter value");
                if (GetHash(secret + counter + viewNumber) != secretHash) throw new Exception("Invalid secret");

                counterLatest++;
            }
        }

        // by replica
        public void ResetCounter(IDictionary<string, string> lAndSignedLHashCounterViewNumbers)
        {
            const int f = 2;

            var atLeastFPlus1ConsistentL = lAndSignedLHashCounterViewNumbers
                .Select(lAndSignedLHashCounterViewNumber =>
                {
                    byte[] buffer;
                    if (Verify(lAndSignedLHashCounterViewNumber.Value, out buffer) == false) throw new Exception("Invalid signature");

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
                counterLatest = atLeastFPlus1ConsistentL.counter;
                viewNumber = atLeastFPlus1ConsistentL.viewNumber;
            }
        }
    }
}

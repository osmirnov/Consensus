using System;
using System.Linq;
using System.Text;

namespace Consensus.FastBFT.Infrastructure
{
    public class Crypto
    {
        private readonly string publicKey;
        private readonly string privateKey;

        public Crypto(string publicKey, string privateKey)
        {
            this.publicKey = publicKey;
            this.privateKey = privateKey;
        }

        public string Encrypt(string str)
        {
            return publicKey + str;
        }

        public byte[] EncryptAuth(byte[] buffer, byte key)
        {
            return (new byte[] { key, 0xff })
                .Concat(buffer)
                .ToArray();
        }

        public string Decrypt(string encryptedStr)
        {
            return encryptedStr.Substring(encryptedStr.IndexOf(publicKey), publicKey.Length);
        }

        public byte[] DecryptAuth(byte[] encryptedBuffer, byte key)
        {
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

        public uint GetHash(string str)
        {
            return (uint)str.GetHashCode() ^ 397;
        }
    }
}

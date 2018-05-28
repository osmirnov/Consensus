using System;
using System.Linq;
using System.Text;

namespace Consensus.FastBFT.Infrastructure
{
    public static class Crypto
    {
        public static string Encrypt(string publicKey, string str)
        {
            return publicKey + str;
        }

        public static byte[] EncryptAuth(byte key, byte[] buffer)
        {
            return (new byte[] { key, 0xff })
                .Concat(buffer)
                .ToArray();
        }

        public static string Decrypt(string privateKey, string encryptedStr)
        {
            return encryptedStr.Substring(encryptedStr.IndexOf(privateKey), privateKey.Length);
        }

        public static byte[] DecryptAuth(byte key, byte[] encryptedBuffer)
        {
            if (encryptedBuffer[0] != key) throw new Exception("Invalid encryption");
            if (encryptedBuffer[1] != 0xff) throw new Exception("Invalid encryption");

            return encryptedBuffer.Skip(2).ToArray();
        }

        public static byte[] Sign(string privateKey, byte[] buffer)
        {
            return Encoding.UTF8.GetBytes(privateKey)
                .Concat(new byte[] { 0xff })
                .Concat(buffer)
                .ToArray();
        }

        public static bool Verify(string publicKey, byte[] signedBuffer, out byte[] buffer)
        {
            var keyLength = Encoding.UTF8.GetByteCount(publicKey);

            if (signedBuffer[keyLength] != 0xff) throw new Exception("Invalid signature");

            buffer = signedBuffer.Skip(keyLength + 1).ToArray();

            return true;
        }

        public static uint GetHash(string str)
        {
            return (uint)str.GetHashCode() ^ 397;
        }
    }
}

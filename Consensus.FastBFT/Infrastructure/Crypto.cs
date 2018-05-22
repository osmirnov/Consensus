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

        public static string Sign(string privateKey, string str)
        {
            return privateKey + str;
        }

        public static bool Verify(string publicKey, string signedStr, out byte[] buffer)
        {
            buffer = Encoding.UTF8.GetBytes(signedStr.Substring(signedStr.IndexOf(publicKey), publicKey.Length));
            return true;
        }

        public static uint GetHash(string str)
        {
            return (uint)str.GetHashCode() ^ 397;
        }
    }
}

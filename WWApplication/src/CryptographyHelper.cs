using System;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WW
{
    // RSA公開鍵暗号ヘルパクラス
    public class RsaCriptoHelper
    {
        public static void CreateKeyPair(int keySize, out String publicKey, out String privateKey)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(keySize);

            publicKey = rsa.ToXmlString(false);
            privateKey = rsa.ToXmlString(true);
        }

        // 暗号化
        public static byte[] Encript(String publicKey, byte[] src)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKey);

            return rsa.Encrypt(src, false);
        }

        // 複合化
        public static byte[] Decript(String privateKey, byte[] src)
        {
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(privateKey);

            return rsa.Decrypt(src, false);
        }
    }
    
    // TripleDES共通鍵暗号ヘルパクラス
    public class TripleDESHelper
    {
        // 暗号化
        public static byte[] Encript(byte[] key, byte[] iv, byte[] src)
        {
            MemoryStream memStream = new MemoryStream();
            CryptoStream cryptStream = new CryptoStream(
                memStream,
                new TripleDESCryptoServiceProvider().CreateEncryptor(key, iv),
                CryptoStreamMode.Write);

            cryptStream.Write(src, 0, src.Length);
            cryptStream.FlushFinalBlock();

            byte[] encrypted = memStream.ToArray();

            cryptStream.Close();
            memStream.Close();

            return encrypted;
        }

        // 複合化
        public static byte[] Decrypt(byte[] key, byte[] iv, byte[] src)
        {
            MemoryStream memStream = new MemoryStream(src);
            CryptoStream cryptStream = new CryptoStream(
                memStream,
                new TripleDESCryptoServiceProvider().CreateDecryptor(key, iv),
                CryptoStreamMode.Read);

            byte[] data = new byte[src.Length];

            cryptStream.Read(data, 0, data.Length);

            return data;
        }

        private static void CreateKey(out byte[] key, out byte[] iv)
        {
            TripleDESCryptoServiceProvider des = new TripleDESCryptoServiceProvider();

            KeySizes[] ks = des.LegalKeySizes;
            foreach (var k in ks)
            {
                des.KeySize = k.MaxSize;
            }
            ks = des.LegalBlockSizes;
            foreach (var k in ks)
            {
                des.BlockSize = k.MaxSize;
            }
            des.GenerateKey();
            des.GenerateIV();

            key = des.Key;
            iv = des.IV;
        }
    }
}

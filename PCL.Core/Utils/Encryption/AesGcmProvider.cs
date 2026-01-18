using System;
using System.Security.Cryptography;

namespace PCL.Core.Utils.Encryption
{
    public class AesGcmProvider : IEncryptionProvider
    {
        public static AesGcmProvider Instance { get; } = new();

        private const int NonceSize = 12; // 96 bits
        private const int TagSize = 16;   // 128 bits

        public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            byte[] nonce = new byte[NonceSize];
            RandomNumberGenerator.Fill(nonce);

            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[data.Length];

            using (var aesGcm = new AesGcm(key, TagSize))
            {
                aesGcm.Encrypt(nonce, data, ciphertext, tag);
            }

            // [Nonce(12)] [Tag(16)] [Ciphertext(n)]
            byte[] result = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

            return result;
        }

        public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            if (data.Length < NonceSize + TagSize)
                throw new ArgumentException("加密数据长度不足。");

            ReadOnlySpan<byte> nonce = data.Slice(0, NonceSize);
            ReadOnlySpan<byte> tag = data.Slice(NonceSize, TagSize);
            ReadOnlySpan<byte> ciphertext = data.Slice(NonceSize + TagSize);

            byte[] plaintext = new byte[ciphertext.Length];

            using (var aesGcm = new AesGcm(key, TagSize))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            return plaintext;
        }

        public bool IsSupported { get => AesGcm.IsSupported; }
    }
}
using System;
using System.IO;
using System.Security.Cryptography;

namespace PCL.Core.Utils.Encryption
{
    [Obsolete("Do not use this AES mode for Encryption")]
    public sealed class AesCbcProvider : IEncryptionProvider
    {
        public static AesCbcProvider Instance { get; } = new();

        private const int SaltSize = 32;
        private const int IvSize = 16;

        public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.BlockSize = 128;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var salt = data[..SaltSize];

            var iv = data[SaltSize..(SaltSize + IvSize)];
            aes.IV = iv.ToArray();

            if (data.Length < salt.Length + iv.Length)
            {
                throw new ArgumentException("AES-CBC: Can not decrypt data, the encrypted data is broken");
            }

#pragma warning disable SYSLIB0041
            using (var deriveBytes = new Rfc2898DeriveBytes(key.ToArray(), salt.ToArray(), 1000))
            {
                aes.Key = deriveBytes.GetBytes(aes.KeySize / 8);
            }
#pragma warning restore SYSLIB0041

            using var ret = new MemoryStream();
            using var ms = new MemoryStream(data[(SaltSize + IvSize)..].ToArray());
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            cs.CopyTo(ret);
            return ret.ToArray();
        }

        public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            throw new NotSupportedException("You should no longer use AES-CBC as your encryption method");
        }

        public bool IsSupported { get => false; }
    }
}

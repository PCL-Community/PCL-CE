// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
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
            var result = new byte[NonceSize + TagSize + data.Length];
            var nonce = result.AsSpan(0, NonceSize);
            var tag = result.AsSpan(NonceSize, TagSize);
            var ciphertext = result.AsSpan(NonceSize + TagSize);
            RandomNumberGenerator.Fill(nonce);

            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Encrypt(nonce, data, ciphertext, tag);

            return result;
        }

        public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            if (data.Length < NonceSize + TagSize)
                throw new ArgumentException("加密数据长度不足。");

            var nonce = data[..NonceSize];
            var tag = data.Slice(NonceSize, TagSize);
            var ciphertext = data[(NonceSize + TagSize)..];

            byte[] plaintext = new byte[ciphertext.Length];

            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }

        [SuppressMessage("Performance", "CA1822", Justification = "Instance property is retained for provider API compatibility.")]
        public bool IsSupported => AesGcm.IsSupported;
    }
}

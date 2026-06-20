// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace PCL.Core.Utils.Encryption;

public sealed class ChaCha20Poly1305Provider : IEncryptionProvider
{
    public static ChaCha20Poly1305Provider Instance { get; } = new();

    private const int NonceSize = 12;    // 96-bit nonce for ChaCha20Poly1305
    private const int TagSize = 16;      // 128-bit authentication tag
    private const int KeySize = 32;      // 256-bit key
    private const int SaltSize = 16;     // 128-bit salt for HKDF

    public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        var result = new byte[SaltSize + NonceSize + TagSize + data.Length];
        var resultSpan = result.AsSpan();
        var salt = resultSpan[..SaltSize];
        var nonce = resultSpan.Slice(SaltSize, NonceSize);
        var tag = resultSpan.Slice(SaltSize + NonceSize, TagSize);
        var ciphertext = resultSpan[(SaltSize + NonceSize + TagSize)..];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);

        Span<byte> outputKey = stackalloc byte[KeySize];
        _DeriveKey(key, salt, outputKey);
        using var chacha = new ChaCha20Poly1305(outputKey);
        chacha.Encrypt(nonce, data, ciphertext, tag);

        return result;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
    {
        // Verify minimum data length
        if (data.Length < SaltSize + NonceSize + TagSize)
            throw new ArgumentException("Invalid encrypted data length");

        // Encryption data: salt + nonce + tag + ciphertext
        var salt = data[..SaltSize];
        var nonce = data.Slice(SaltSize, NonceSize);
        var tag = data.Slice(SaltSize + NonceSize, TagSize);
        var ciphertext = data[(SaltSize + NonceSize + TagSize)..];

        // Derive key using the extracted salt
        Span<byte> outputKey = stackalloc byte[KeySize];
        _DeriveKey(key, salt, outputKey);
        using var chacha = new ChaCha20Poly1305(outputKey);

        // Perform decryption
        var plaintext = new byte[ciphertext.Length];
        chacha.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static ReadOnlySpan<byte> Info => "PCL.Core.Utils.Encryption.ChaCha20"u8;
    private static void _DeriveKey(ReadOnlySpan<byte> ikm, ReadOnlySpan<byte> salt, Span<byte> outputKey)
    {
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            outputKey,
            salt,
            Info);
    }

    [SuppressMessage("Performance", "CA1822", Justification = "Instance property is retained for provider API compatibility.")]
    public bool IsSupported => ChaCha20Poly1305.IsSupported;
}

using System;
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
        // Generate random salt, nonce and the tag
        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(nonce);
        RandomNumberGenerator.Fill(tag);

        // Derive key using the salt
        Span<byte> outputKey = stackalloc byte[KeySize];
        _DeriveKey(key, salt, outputKey);
        using var chacha = new ChaCha20Poly1305(outputKey);

        // Prepare output arrays
        var ciphertext = new byte[data.Length];

        // Perform encryption
        chacha.Encrypt(nonce, data, ciphertext, tag);

        // Make the encryption data: salt + nonce + tag + ciphertext
        var result = new byte[SaltSize + NonceSize + ciphertext.Length + TagSize];
        var resultSpan = result.AsSpan();

        salt.CopyTo(resultSpan[..SaltSize]);
        nonce.CopyTo(resultSpan.Slice(SaltSize, NonceSize));
        tag.CopyTo(resultSpan.Slice(SaltSize + NonceSize, TagSize));
        ciphertext.CopyTo(resultSpan.Slice(SaltSize + NonceSize + TagSize, ciphertext.Length));

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

    private static readonly byte[] _Info = "PCL.Core.Utils.Encryption.ChaCha20"u8.ToArray();
    private static void _DeriveKey(ReadOnlySpan<byte> ikm, ReadOnlySpan<byte> salt, Span<byte> outputKey)
    {
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            outputKey,
            salt,
            _Info.AsSpan());
    }

    public bool IsSupported { get => ChaCha20Poly1305.IsSupported; }
}
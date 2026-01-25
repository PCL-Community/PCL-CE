using System;

namespace PCL.Core.Utils.Encryption;

public interface IEncryptionProvider
{
    public byte[] Encrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key);
    public byte[] Decrypt(ReadOnlySpan<byte> data, ReadOnlySpan<byte> key);

    public static bool IsSupported { get; }
}
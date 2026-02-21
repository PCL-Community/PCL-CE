using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Hash;

public class SHA512Provider : IHashProvider
{
    public static SHA512Provider Instance { get; } = new();
    public int Length => 128;

    public byte[] ComputeHash(byte[] input)
    {
        return SHA512.HashData(input);
    }

    public byte[] ComputeHash(ReadOnlySpan<byte> input)
    {
        return SHA512.HashData(input);
    }

    public byte[] ComputeHash(string input, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return ComputeHash(encoding.GetBytes(input));
    }

    public byte[] ComputeHash(Stream input)
    {
        return SHA512.HashData(input);
    }

    public async ValueTask<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default)
    {
        return await SHA512.HashDataAsync(input, cancellationToken).ConfigureAwait(false);
    }
}
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Hash;

public class MurmurHash2Provider : IHashProvider
{
    public static MurmurHash2Provider Instance { get; } = new();
    public int Length => 8;

    public byte[] ComputeHash(ReadOnlySpan<byte> data)
    {
        var filtered = new List<byte>(data.Length);
        foreach (var b in data)
            if (b != 9 && b != 10 && b != 13 && b != 32)
                filtered.Add(b);
        return _ComputeHash(CollectionsMarshal.AsSpan(filtered));
    }

    public byte[] ComputeHash(string input, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        return ComputeHash(encoding.GetBytes(input));
    }

    public byte[] ComputeHash(Stream input)
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return ComputeHash(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    public async ValueTask<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        await input.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ComputeHash(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static byte[] _ComputeHash(ReadOnlySpan<byte> data)
    {
        var h = (uint)(1 ^ data.Length);
        var i = 0;
        var loopTo = data.Length - 4;

        for (; i <= loopTo; i += 4)
        {
            var k = data[i]
                 | ((uint)data[i + 1] << 8)
                 | ((uint)data[i + 2] << 16)
                 | ((uint)data[i + 3] << 24);
            k *= 0x5BD1E995;
            k ^= k >> 24;
            k *= 0x5BD1E995;
            h *= 0x5BD1E995;
            h ^= k;
        }

        switch (data.Length - i)
        {
            case 3:
                h ^= (uint)(data[i] | ((uint)data[i + 1] << 8));
                h ^= (uint)data[i + 2] << 16;
                h *= 0x5BD1E995;
                break;
            case 2:
                h ^= (uint)(data[i] | ((uint)data[i + 1] << 8));
                h *= 0x5BD1E995;
                break;
            case 1:
                h ^= data[i];
                h *= 0x5BD1E995;
                break;
        }

        h ^= h >> 13;
        h *= 0x5BD1E995;
        h ^= h >> 15;

        return BitConverter.GetBytes(h);
    }
}

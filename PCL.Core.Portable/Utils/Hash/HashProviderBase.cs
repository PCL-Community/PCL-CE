// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Text;

namespace PCL.Core.Utils.Hash;

public abstract class HashProviderBase : IHashProvider
{
    private const int StackBufferLimit = 512;

    public abstract int HashSizeInBytes { get; }
    public int Length => HashSizeInBytes * 2;

    public abstract ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default);

    public abstract byte[] ComputeHash(Stream input);
    public abstract byte[] ComputeHash(ReadOnlySpan<byte> input);

    public byte[] ComputeHash(string input, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = GC.AllocateUninitializedArray<byte>(HashSizeInBytes);
        TryComputeHash(input.AsSpan(), result, out _, encoding);
        return result;
    }

    public bool TryComputeHash(
        ReadOnlySpan<char> input,
        Span<byte> destination,
        out int bytesWritten,
        Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        var byteCount = encoding.GetByteCount(input);
        byte[]? rented = null;
        Span<byte> bytes = byteCount <= StackBufferLimit
            ? stackalloc byte[byteCount]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));

        try
        {
            var written = encoding.GetBytes(input, bytes);
            return TryComputeHash(bytes[..written], destination, out bytesWritten);
        }
        finally
        {
            if (rented is not null)
            {
                bytes[..byteCount].Clear();
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    public abstract bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten);
}

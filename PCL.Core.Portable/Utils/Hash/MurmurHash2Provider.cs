// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Buffers.Binary;

namespace PCL.Core.Utils.Hash;

public sealed class MurmurHash2Provider : HashProviderBase
{
    private const int StackBufferLimit = 512;
    private const int StreamBufferSize = 64 * 1024;

    public static MurmurHash2Provider Instance { get; } = new();
    public override int HashSizeInBytes => sizeof(uint);

    public override byte[] ComputeHash(ReadOnlySpan<byte> input)
    {
        var result = GC.AllocateUninitializedArray<byte>(HashSizeInBytes);
        TryComputeHash(input, result, out _);
        return result;
    }

    public override bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (destination.Length < HashSizeInBytes)
        {
            bytesWritten = 0;
            return false;
        }

        var filteredLength = CountSignificantBytes(input);
        if (filteredLength == input.Length)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, ComputeCore(input));
            bytesWritten = HashSizeInBytes;
            return true;
        }

        byte[]? rented = null;
        Span<byte> filtered = filteredLength <= StackBufferLimit
            ? stackalloc byte[filteredLength]
            : (rented = ArrayPool<byte>.Shared.Rent(filteredLength));

        try
        {
            CopySignificantBytes(input, filtered);
            BinaryPrimitives.WriteUInt32LittleEndian(
                destination,
                ComputeCore(filtered[..filteredLength]));
            bytesWritten = HashSizeInBytes;
            return true;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public override byte[] ComputeHash(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.CanSeek)
            return ComputeSeekableStream(input);

        return ComputeBufferedStream(input);
    }

    public override async ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.CanSeek
            ? await ComputeSeekableStreamAsync(input, cancellationToken).ConfigureAwait(false)
            : await ComputeBufferedStreamAsync(input, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] ComputeSeekableStream(Stream input)
    {
        // MurmurHash2 seeds its state with the filtered length, so a seekable stream needs a count pass.
        var origin = input.Position;
        var readBuffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            var significantLength = CountSignificantBytes(input, readBuffer);
            input.Position = origin;

            var state = (uint)(1 ^ significantLength);
            uint tail = 0;
            var tailLength = 0;
            int read;
            while ((read = input.Read(readBuffer)) > 0)
                AppendToHashState(
                    readBuffer.AsSpan(0, read),
                    ref state,
                    ref tail,
                    ref tailLength);

            return CompleteHash(state, tail, tailLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private static async ValueTask<byte[]> ComputeSeekableStreamAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        var origin = input.Position;
        var readBuffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            var significantLength = await CountSignificantBytesAsync(
                    input,
                    readBuffer,
                    cancellationToken)
                .ConfigureAwait(false);
            input.Position = origin;

            var state = (uint)(1 ^ significantLength);
            uint tail = 0;
            var tailLength = 0;
            int read;
            while ((read = await input
                       .ReadAsync(readBuffer, cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                AppendToHashState(
                    readBuffer.AsSpan(0, read),
                    ref state,
                    ref tail,
                    ref tailLength);
            }

            return CompleteHash(state, tail, tailLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private static byte[] ComputeBufferedStream(Stream input)
    {
        var data = ArrayPool<byte>.Shared.Rent(256);
        var readBuffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        var length = 0;
        try
        {
            int read;
            while ((read = input.Read(readBuffer)) > 0)
                AppendSignificantBytes(readBuffer.AsSpan(0, read), ref data, ref length);

            return ComputeHashCore(data.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    private static async ValueTask<byte[]> ComputeBufferedStreamAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        var data = ArrayPool<byte>.Shared.Rent(256);
        var readBuffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        var length = 0;
        try
        {
            int read;
            while ((read = await input
                       .ReadAsync(readBuffer, cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                AppendSignificantBytes(readBuffer.AsSpan(0, read), ref data, ref length);
            }

            return ComputeHashCore(data.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            ArrayPool<byte>.Shared.Return(data);
        }
    }

    private static uint ComputeCore(ReadOnlySpan<byte> data)
    {
        var h = (uint)(1 ^ data.Length);
        var i = 0;
        var loopTo = data.Length - 4;

        for (; i <= loopTo; i += 4)
        {
            var k = BinaryPrimitives.ReadUInt32LittleEndian(data[i..]);
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

        return h;
    }

    private static byte[] ComputeHashCore(ReadOnlySpan<byte> data)
    {
        var result = GC.AllocateUninitializedArray<byte>(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(result, ComputeCore(data));
        return result;
    }

    private static int CountSignificantBytes(ReadOnlySpan<byte> data)
    {
        var count = 0;
        foreach (var value in data)
            if (IsSignificant(value))
                count++;
        return count;
    }

    private static void CopySignificantBytes(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var index = 0;
        foreach (var value in source)
            if (IsSignificant(value))
                destination[index++] = value;
    }

    private static bool IsSignificant(byte value) =>
        value is not (9 or 10 or 13 or 32);

    private static int CountSignificantBytes(Stream input, byte[] buffer)
    {
        long count = 0;
        int read;
        while ((read = input.Read(buffer)) > 0)
            count += CountSignificantBytes(buffer.AsSpan(0, read));
        return checked((int)count);
    }

    private static async ValueTask<int> CountSignificantBytesAsync(
        Stream input,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        long count = 0;
        int read;
        while ((read = await input
                   .ReadAsync(buffer, cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            count += CountSignificantBytes(buffer.AsSpan(0, read));
        }

        return checked((int)count);
    }

    private static void AppendToHashState(
        ReadOnlySpan<byte> source,
        ref uint state,
        ref uint tail,
        ref int tailLength)
    {
        foreach (var value in source)
        {
            if (!IsSignificant(value))
                continue;

            tail |= (uint)value << (tailLength * 8);
            tailLength++;
            if (tailLength != sizeof(uint))
                continue;

            MixBlock(ref state, tail);
            tail = 0;
            tailLength = 0;
        }
    }

    private static byte[] CompleteHash(uint state, uint tail, int tailLength)
    {
        if (tailLength > 0)
        {
            state ^= tail;
            state *= 0x5BD1E995;
        }

        state ^= state >> 13;
        state *= 0x5BD1E995;
        state ^= state >> 15;

        var result = GC.AllocateUninitializedArray<byte>(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(result, state);
        return result;
    }

    private static void MixBlock(ref uint state, uint block)
    {
        block *= 0x5BD1E995;
        block ^= block >> 24;
        block *= 0x5BD1E995;
        state *= 0x5BD1E995;
        state ^= block;
    }

    private static void AppendSignificantBytes(
        ReadOnlySpan<byte> source,
        ref byte[] destination,
        ref int length)
    {
        var additionalLength = CountSignificantBytes(source);
        EnsureCapacity(ref destination, length + additionalLength);
        CopySignificantBytes(source, destination.AsSpan(length, additionalLength));
        length += additionalLength;
    }

    private static void EnsureCapacity(ref byte[] buffer, int requiredLength)
    {
        if (requiredLength <= buffer.Length)
            return;

        var newLength = Math.Max(requiredLength, checked(buffer.Length * 2));
        var replacement = ArrayPool<byte>.Shared.Rent(newLength);
        buffer.CopyTo(replacement, 0);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = replacement;
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class HashProviderTests
{
    [TestMethod]
    public void Sha256TryComputeHashMatchesFrameworkWithoutAllocating()
    {
        var input = Encoding.UTF8.GetBytes("PCL N portable core allocation regression");
        Span<byte> destination = stackalloc byte[SHA256.HashSizeInBytes];

        Assert.IsTrue(SHA256Provider.Instance.TryComputeHash(input, destination, out var written));
        Assert.AreEqual(SHA256.HashSizeInBytes, written);
        CollectionAssert.AreEqual(SHA256.HashData(input), destination.ToArray());

        SHA256Provider.Instance.TryComputeHash(input, destination, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
            SHA256Provider.Instance.TryComputeHash(input, destination, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0L, allocated, "Caller-buffer hashing must remain allocation-free.");
    }

    [TestMethod]
    public void Sha256StringCallerBufferDoesNotAllocate()
    {
        const string input = "PCL N string hashing";
        Span<byte> destination = stackalloc byte[SHA256.HashSizeInBytes];

        SHA256Provider.Instance.TryComputeHash(input, destination, out _);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
            SHA256Provider.Instance.TryComputeHash(input, destination, out _);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(0L, allocated, "Short string hashing must use stack storage.");
    }

    [TestMethod]
    public async Task MurmurHash2FiltersWhitespaceForSpanAndStream()
    {
        var compact = Encoding.UTF8.GetBytes("fabric.mod.json");
        var spaced = Encoding.UTF8.GetBytes(" fabric.\r\nmod.\tjson ");
        var expected = MurmurHash2Provider.Instance.ComputeHash(compact);

        CollectionAssert.AreEqual(expected, MurmurHash2Provider.Instance.ComputeHash(spaced));

        await using var stream = new MemoryStream(spaced);
        var streamed = await MurmurHash2Provider.Instance.ComputeHashAsync(stream);
        CollectionAssert.AreEqual(expected, streamed);
        Assert.AreNotEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(streamed));
    }

    [TestMethod]
    public async Task MurmurHash2StreamMatchesSpanAcrossBufferAndTailBoundaries()
    {
        for (var tailLength = 0; tailLength < sizeof(uint); tailLength++)
        {
            var data = new byte[64 * 1024 + tailLength];
            Random.Shared.NextBytes(data);
            for (var index = 31; index < data.Length; index += 97)
                data[index] = (byte)' ';

            var expected = MurmurHash2Provider.Instance.ComputeHash(data);

            using var syncStream = new MemoryStream(data);
            CollectionAssert.AreEqual(
                expected,
                MurmurHash2Provider.Instance.ComputeHash(syncStream));

            await using var asyncStream = new MemoryStream(data);
            CollectionAssert.AreEqual(
                expected,
                await MurmurHash2Provider.Instance.ComputeHashAsync(asyncStream));

            await using var nonSeekableStream = new NonSeekableReadStream(data);
            CollectionAssert.AreEqual(
                expected,
                await MurmurHash2Provider.Instance.ComputeHashAsync(nonSeekableStream));
        }
    }

    private sealed class NonSeekableReadStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}

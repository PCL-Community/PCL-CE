// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;

namespace PCL.Core.Utils.Hash;

#pragma warning disable CA5350 // Minecraft metadata mandates SHA-1 as a compatibility checksum.
public sealed class SHA1Provider : HashProviderBase
{
    public static SHA1Provider Instance { get; } = new();
    public override int HashSizeInBytes => SHA1.HashSizeInBytes;

    public override byte[] ComputeHash(ReadOnlySpan<byte> input) => SHA1.HashData(input);

    public override byte[] ComputeHash(Stream input) => SHA1.HashData(input);

    public override ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default) =>
        SHA1.HashDataAsync(input, cancellationToken);

    public override bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten) =>
        SHA1.TryHashData(input, destination, out bytesWritten);
}
#pragma warning restore CA5350

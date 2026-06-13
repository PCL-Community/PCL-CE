// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;

namespace PCL.Core.Utils.Hash;

#pragma warning disable CA5351 // Minecraft metadata uses MD5 as a compatibility checksum, never for security.
public sealed class MD5Provider : HashProviderBase
{
    public static MD5Provider Instance { get; } = new();
    public override int HashSizeInBytes => MD5.HashSizeInBytes;

    public override byte[] ComputeHash(ReadOnlySpan<byte> input) => MD5.HashData(input);

    public override byte[] ComputeHash(Stream input) => MD5.HashData(input);

    public override ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default) =>
        MD5.HashDataAsync(input, cancellationToken);

    public override bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten) =>
        MD5.TryHashData(input, destination, out bytesWritten);
}
#pragma warning restore CA5351

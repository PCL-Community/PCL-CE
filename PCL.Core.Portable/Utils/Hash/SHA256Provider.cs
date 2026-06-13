// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;

namespace PCL.Core.Utils.Hash;

public sealed class SHA256Provider : HashProviderBase
{
    public static SHA256Provider Instance { get; } = new();
    public override int HashSizeInBytes => SHA256.HashSizeInBytes;

    public override byte[] ComputeHash(ReadOnlySpan<byte> input) => SHA256.HashData(input);

    public override byte[] ComputeHash(Stream input) => SHA256.HashData(input);

    public override ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default) =>
        SHA256.HashDataAsync(input, cancellationToken);

    public override bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten) =>
        SHA256.TryHashData(input, destination, out bytesWritten);
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;

namespace PCL.Core.Utils.Hash;

public sealed class SHA512Provider : HashProviderBase
{
    public static SHA512Provider Instance { get; } = new();
    public override int HashSizeInBytes => SHA512.HashSizeInBytes;

    public override byte[] ComputeHash(ReadOnlySpan<byte> input) => SHA512.HashData(input);

    public override byte[] ComputeHash(Stream input) => SHA512.HashData(input);

    public override ValueTask<byte[]> ComputeHashAsync(
        Stream input,
        CancellationToken cancellationToken = default) =>
        SHA512.HashDataAsync(input, cancellationToken);

    public override bool TryComputeHash(
        ReadOnlySpan<byte> input,
        Span<byte> destination,
        out int bytesWritten) =>
        SHA512.TryHashData(input, destination, out bytesWritten);
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;

namespace PCL.Core.Utils.Hash;

public interface IHashProvider
{
    ValueTask<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default);
    byte[] ComputeHash(Stream input);
    byte[] ComputeHash(ReadOnlySpan<byte> input);
    byte[] ComputeHash(string input, Encoding? encoding = null);
    bool TryComputeHash(ReadOnlySpan<byte> input, Span<byte> destination, out int bytesWritten);
    bool TryComputeHash(
        ReadOnlySpan<char> input,
        Span<byte> destination,
        out int bytesWritten,
        Encoding? encoding = null);
    int HashSizeInBytes { get; }
    int Length { get; }
}

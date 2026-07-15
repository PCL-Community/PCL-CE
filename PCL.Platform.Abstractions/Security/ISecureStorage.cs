// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Security;

public interface ISecureStorage
{
    ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public enum SecureStorageStatus
{
    Success,
    NotFound,
    Unavailable,
    Failed
}

public sealed record SecureStorageReadResult(SecureStorageStatus Status, byte[]? Value = null, string? Message = null);

public sealed record SecureStorageOperationResult(SecureStorageStatus Status, string? Message = null)
{
    public bool IsSuccess => Status is SecureStorageStatus.Success or SecureStorageStatus.NotFound;
}

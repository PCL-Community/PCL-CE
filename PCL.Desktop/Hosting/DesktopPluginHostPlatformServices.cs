// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting.PluginPlatform;
using PCL.Platform.Abstractions.Security;
using PCL.Platform.Processes;
using PCL.Platform.Security;

namespace PCL.Desktop.Hosting;

internal sealed class DesktopPluginHostSecureStorage(ISecureStorage storage) : IPluginHostSecureStorage
{
    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default) =>
        storage.ReadAsync(key, cancellationToken);

    public ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) =>
        storage.WriteAsync(key, value, cancellationToken);

    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        storage.DeleteAsync(key, cancellationToken);

    public ValueTask<SecureStorageReadResult> UnprotectLegacyWindowsAsync(
        ReadOnlyMemory<byte> encrypted,
        ReadOnlyMemory<byte> entropy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Unavailable));
        try
        {
            return ValueTask.FromResult(new SecureStorageReadResult(
                SecureStorageStatus.Success,
                LegacyWindowsDataProtection.Unprotect(encrypted.ToArray(), entropy.ToArray())));
        }
        catch (Exception exception)
        {
            return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Failed, Message: exception.Message));
        }
    }
}

internal sealed class DesktopPluginHostUriLauncher : IPluginHostUriLauncher
{
    public static DesktopPluginHostUriLauncher Instance { get; } = new();

    public ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        DefaultUriLauncher.OpenAsync(uri, cancellationToken);
}

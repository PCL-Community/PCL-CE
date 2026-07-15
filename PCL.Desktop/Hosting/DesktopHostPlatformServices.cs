// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Application.Hosting.RuntimeExtensions;
using PCL.Platform.Abstractions.Security;
using PCL.Platform.Processes;
using PCL.Platform.Security;

namespace PCL.Desktop.Hosting;

internal sealed class DesktopHostSecureStorage(ISecureStorage storage) : IHostSecureStorage
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

internal sealed class DesktopHostClipboard : IHostClipboard
{
    public static DesktopHostClipboard Instance { get; } = new();

    public async ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TopLevel? topLevel = await ResolveTopLevelAsync().ConfigureAwait(false);
        return topLevel is null ? null : await topLevel.Clipboard!.TryGetTextAsync().ConfigureAwait(false);
    }

    public async ValueTask WriteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        TopLevel? topLevel = await ResolveTopLevelAsync().ConfigureAwait(false);
        if (topLevel?.Clipboard is not null)
            await topLevel.Clipboard.SetTextAsync(text).ConfigureAwait(false);
    }

    private static async Task<TopLevel?> ResolveTopLevelAsync()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return await Dispatcher.UIThread.InvokeAsync(ResolveTopLevelAsyncCore);
        return ResolveTopLevelAsyncCore();
    }

    private static TopLevel? ResolveTopLevelAsyncCore() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime && lifetime.Windows.Count > 0
            ? lifetime.Windows[0]
            : null;
}

internal sealed class DesktopHostUriLauncher : IHostUriLauncher
{
    public static DesktopHostUriLauncher Instance { get; } = new();

    public ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default) =>
        DefaultUriLauncher.OpenAsync(uri, cancellationToken);
}

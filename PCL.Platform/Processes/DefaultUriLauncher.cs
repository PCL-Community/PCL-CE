// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace PCL.Platform.Processes;

public static class DefaultUriLauncher
{
    public static ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        cancellationToken.ThrowIfCancellationRequested();
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ArgumentException("Only absolute HTTP and HTTPS URIs can be opened.", nameof(uri));
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return ValueTask.FromResult(process is not null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or global::System.ComponentModel.Win32Exception)
        {
            return ValueTask.FromResult(false);
        }
    }
}

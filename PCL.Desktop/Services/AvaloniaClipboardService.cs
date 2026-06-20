// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Input.Platform;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

internal sealed class AvaloniaClipboardService : IClipboardService
{
    public Task SetTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return AvaloniaUiTaskRunner.RunAsync(
            async () =>
            {
                IClipboard? clipboard =
                    DesktopWindowProvider.GetRequiredMainWindow().Clipboard;
                if (clipboard is null)
                {
                    throw new InvalidOperationException(
                        "The system clipboard is not available.");
                }

                await clipboard.SetTextAsync(text);
                return true;
            },
            cancellationToken);
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Threading;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

public sealed class AvaloniaUiScheduler : IUiScheduler
{
    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }

    public async Task InvokeAsync(
        Action action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        await Dispatcher.UIThread
            .InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);
    }
}

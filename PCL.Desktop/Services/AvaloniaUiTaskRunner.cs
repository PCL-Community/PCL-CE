// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Threading;

namespace PCL.Desktop.Services;

internal static class AvaloniaUiTaskRunner
{
    public static async Task<T> RunAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        if (Dispatcher.UIThread.CheckAccess())
            return await action().ConfigureAwait(true);

        TaskCompletionSource<T> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration =
            cancellationToken.Register(
                static state =>
                {
                    var (source, token) =
                        ((TaskCompletionSource<T>, CancellationToken))state!;
                    source.TrySetCanceled(token);
                },
                (completion, cancellationToken));

        Dispatcher.UIThread.Post(
            async () =>
            {
                try
                {
                    completion.TrySetResult(
                        await action().ConfigureAwait(true));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            });

        return await completion.Task.ConfigureAwait(false);
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Online;

public static class OnlineUiScheduler
{
    private static Func<Action, Task>? _invokeAsync;

    public static void Configure(Func<Action, Task>? invokeAsync)
    {
        Volatile.Write(ref _invokeAsync, invokeAsync);
    }

    public static Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var invokeAsync = Volatile.Read(ref _invokeAsync);
        if (invokeAsync is not null)
            return invokeAsync(action);

        action();
        return Task.CompletedTask;
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Utils.Threading;

namespace PCL.Core.Test;

[TestClass]
public sealed class ThreadingTests
{
    [TestMethod]
    public async Task CountResetEventReleasesOnlyRequestedWaiters()
    {
        using var signal = new AsyncCountResetEvent();
        var first = signal.WaitAsync();
        var second = signal.WaitAsync();

        signal.Set();
        await first.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(second.IsCompleted);

        signal.Set();
        await second.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task LimitedTaskPoolHonorsConcurrencyLimit()
    {
        using var pool = new LimitedTaskPool(2);
        var running = 0;
        var maximum = 0;

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => pool.Submit(async () =>
            {
                var current = Interlocked.Increment(ref running);
                InterlockedExtensions.Max(ref maximum, current);
                await Task.Delay(10);
                Interlocked.Decrement(ref running);
            }))
            .ToArray();

        await Task.WhenAll(tasks);
        Assert.IsTrue(maximum <= 2, $"Observed {maximum} concurrent tasks.");
    }

    [TestMethod]
    public async Task DualThreadPoolLimitsEachQueueIndependently()
    {
        using var pool = new DualThreadPool(1);
        var ioRunning = 0;
        var cpuRunning = 0;
        var ioMaximum = 0;
        var cpuMaximum = 0;

        var ioTasks = Enumerable.Range(0, 4)
            .Select(_ => pool.QueueIo(async () =>
            {
                InterlockedExtensions.Max(ref ioMaximum, Interlocked.Increment(ref ioRunning));
                await Task.Delay(10);
                Interlocked.Decrement(ref ioRunning);
            }));
        var cpuTasks = Enumerable.Range(0, 4)
            .Select(_ => pool.QueueCpu(async () =>
            {
                InterlockedExtensions.Max(ref cpuMaximum, Interlocked.Increment(ref cpuRunning));
                await Task.Delay(10);
                Interlocked.Decrement(ref cpuRunning);
            }));

        await Task.WhenAll(ioTasks.Concat(cpuTasks));
        Assert.AreEqual(1, ioMaximum);
        Assert.AreEqual(1, cpuMaximum);
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (current < value)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }
}

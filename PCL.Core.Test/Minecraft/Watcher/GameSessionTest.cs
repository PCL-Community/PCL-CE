using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Watcher;

namespace PCL.Core.Test.Minecraft.Watcher;

[TestClass]
public sealed class GameSessionTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EmitsLiveLogAndReachesRunning()
    {
        var process = new FakeGameProcess();
        var progress = new ConcurrentQueue<double>();
        var options = WatcherTestHelpers.CreateOptions(process, reportProgress: progress.Enqueue);
        using var session = new GameSession(process, options);
        var lines = new ConcurrentQueue<GameLogLineEventArgs>();
        session.LogLineReceived += (_, e) => lines.Enqueue(e);

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: Setting user: player");
        process.Emit("[12:00:00] [main/INFO]: LWJGL Version: 3.3.3");
        process.Emit("[12:00:00] [main/INFO]: OpenAL initialized");

        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Running);
        await WatcherTestHelpers.WaitUntilAsync(() => lines.Count == 3);

        Assert.AreEqual(3u, session.Counters.Info);
        Assert.HasCount(3, session.RecentLog);
        Assert.HasCount(3, session.History);
        Assert.Contains(0.95d, progress);
        Assert.IsTrue(lines.All(line => line.Level == GameLogLevel.Info));
    }

    [TestMethod]
    public async Task WindowAppearanceTransitionsToRunningAndSetsTitle()
    {
        var process = new FakeGameProcess();
        var window = new FakeWindowInterop();
        window.EnqueueWindow(42, "Minecraft 1.20.1");
        var options = WatcherTestHelpers.CreateOptions(
            process,
            window,
            maximizeWindow: true);

        using var session = new GameSession(process, options);
        session.Start();

        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Running);
        await WatcherTestHelpers.WaitUntilAsync(() => !window.Titles.IsEmpty);
        await WatcherTestHelpers.WaitUntilAsync(() => !window.MaximizedHandles.IsEmpty);

        Assert.Contains(title => title.StartsWith("Minecraft "), window.Titles);
        Assert.AreEqual(42, window.MaximizedHandles.First());
    }

    [TestMethod]
    public async Task CrashMarkerRaisesCrashedEventWithRecentLog()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process));
        var crashed = new TaskCompletionSource<GameSessionCrashedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Crashed += (_, e) => crashed.TrySetResult(e);

        session.Start();
        process.Emit("[12:00:00] [Render thread/INFO]: Crash report saved to: crash.txt");

        var args = await crashed.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreEqual(GameSessionState.Crashed, session.State);
        Assert.AreEqual(GameCrashReason.CrashLogDetected, args.Reason);
        Assert.IsTrue(session.CanTerminate);
        Assert.Contains(line => line.Contains("Crash report saved to"), args.RecentLog);
    }

    [TestMethod]
    public async Task CrashMarkerIsProcessedBeforeExit()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process));
        using var entered = new ManualResetEventSlim();
        using var blocked = new ManualResetEventSlim();
        var crashed = new TaskCompletionSource<GameSessionCrashedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        session.LogLineReceived += (_, e) =>
        {
            if (!e.Text.Contains("block consumer"))
                return;

            entered.Set();
            blocked.Wait(TestContext.CancellationToken);
        };
        session.Crashed += (_, e) => crashed.TrySetResult(e);

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: block consumer");
        await WatcherTestHelpers.WaitUntilAsync(() => entered.IsSet);
        process.Emit("[12:00:00] [Render thread/INFO]: Crash report saved to: crash.txt");
        process.Complete(1);

        await Task.Delay(TimeSpan.FromMilliseconds(1200), TestContext.CancellationToken);
        blocked.Set();

        var args = await crashed.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreEqual(GameCrashReason.CrashLogDetected, args.Reason);
        Assert.Contains(line => line.Contains("Crash report saved to"), args.RecentLog);
    }

    [TestMethod]
    public async Task ExitDuringLoadingIsStartupFailure()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process));
        var exited = new TaskCompletionSource<GameSessionExitedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var crashed = new TaskCompletionSource<GameSessionCrashedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, e) => exited.TrySetResult(e);
        session.Crashed += (_, e) => crashed.TrySetResult(e);

        session.Start();
        process.Complete(0);

        var exitArgs = await exited.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        var crashArgs = await crashed.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);

        Assert.AreEqual(GameSessionState.Crashed, session.State);
        Assert.AreEqual(GameExitReason.StartupFailure, exitArgs.Reason);
        Assert.AreEqual(GameCrashReason.StartupFailure, crashArgs.Reason);
    }

    [TestMethod]
    public async Task AbnormalExitAfterRunningIsCrash()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(
                process,
                releaseTime: new DateTime(2020, 1, 1)));
        var exited = new TaskCompletionSource<GameSessionExitedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, e) => exited.TrySetResult(e);

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: Setting user: player");
        process.Emit("[12:00:00] [main/INFO]: LWJGL Version: 3.3.3");
        process.Emit("[12:00:00] [main/INFO]: OpenAL initialized");
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Running);

        process.Complete(1);

        var args = await exited.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreEqual(GameSessionState.Crashed, session.State);
        Assert.AreEqual(GameExitReason.AbnormalExit, args.Reason);
    }

    [TestMethod]
    public async Task OldVersionNonZeroExitEndsNormally()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(
                process,
                releaseTime: new DateTime(2010, 1, 1)));
        var exited = new TaskCompletionSource<GameSessionExitedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, e) => exited.TrySetResult(e);

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: Setting user: player");
        process.Emit("[12:00:00] [main/INFO]: LWJGL Version: 3.3.3");
        process.Emit("[12:00:00] [main/INFO]: OpenAL initialized");
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Running);

        process.Complete(1);

        var args = await exited.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreEqual(GameSessionState.Ended, session.State);
        Assert.AreEqual(GameExitReason.Normal, args.Reason);
    }

    [TestMethod]
    public async Task KillTransitionsToCanceled()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process));
        var exited = new TaskCompletionSource<GameSessionExitedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, e) => exited.TrySetResult(e);

        session.Start();
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Loading);
        session.Kill();

        var args = await exited.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreEqual(GameSessionState.Canceled, session.State);
        Assert.AreEqual(GameExitReason.Canceled, args.Reason);
    }

    [TestMethod]
    public async Task RecentLogIsTrimmedToConfiguredCapacity()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process, recentLogCapacity: 3));

        session.Start();
        for (var i = 0; i < 5; i++)
            process.Emit($"[12:00:00] [main/INFO]: line {i}");

        await WatcherTestHelpers.WaitUntilAsync(() => session.RecentLog.Count == 3);
        Assert.HasCount(3, session.RecentLog);
        Assert.Contains("line 2", session.RecentLog[0]);
        Assert.Contains("line 4", session.RecentLog[2]);
        Assert.HasCount(5, session.FullLog);
    }

    [TestMethod]
    public async Task AbortTokenCancelsSessionAndKillsProcess()
    {
        var process = new FakeGameProcess();
        using var cancellation = new CancellationTokenSource();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process, abortToken: cancellation.Token));

        session.Start();
        await cancellation.CancelAsync();

        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Canceled);
        await WatcherTestHelpers.WaitUntilAsync(() => process.KillCount > 0);
        Assert.AreEqual(GameSessionState.Canceled, session.State);
    }

    [TestMethod]
    public async Task RecentLogKeepsLinesAfterTerminalState()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process));

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: Restarting Minecraft with command foo");
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Ended);

        process.Emit("[12:00:00] [main/INFO]: after terminal");
        await WatcherTestHelpers.WaitUntilAsync(() =>
            session.RecentLog.Any(line => line.Contains("after terminal")));
    }

    [TestMethod]
    public async Task LiveLogDisabledStillRecordsRecentLog()
    {
        var process = new FakeGameProcess();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process, captureLiveLog: false));
        var received = 0;
        session.LogLineReceived += (_, _) => Interlocked.Increment(ref received);

        session.Start();
        process.Emit("[12:00:00] [main/INFO]: line");

        await WatcherTestHelpers.WaitUntilAsync(() => session.RecentLog.Count == 1);
        Assert.AreEqual(0, received);
        Assert.IsEmpty(session.FullLog);
        Assert.AreEqual(0u, session.Counters.Info);
    }

    [TestMethod]
    public async Task StackDumpUsesThreeAttempts()
    {
        var process = new FakeGameProcess();
        var runner = new FakeExternalCommandRunner();
        using var session = new GameSession(
            process,
            WatcherTestHelpers.CreateOptions(process, commandRunner: runner));

        session.Start();
        var result = await session.CaptureStackDumpsAsync(TestContext.CancellationToken);

        Assert.HasCount(3, result);
        Assert.IsTrue(result.All(item => item == "dump-output"));
        Assert.AreEqual(3, runner.RunCount);
    }
}
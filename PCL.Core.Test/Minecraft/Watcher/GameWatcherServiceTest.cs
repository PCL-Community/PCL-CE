using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Watcher;

namespace PCL.Core.Test.Minecraft.Watcher;

[TestClass]
public sealed class GameWatcherServiceTest
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        GameWatcherService.ResetForTests();
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameWatcherService.ResetForTests();
    }

    [TestMethod]
    public async Task CrashStopsActiveStateAndDisallowsLauncherExit()
    {
        var process = new FakeGameProcess();
        var stop =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeChanges = 0;

        GameWatcherService.ActiveSessionsChanged += () => Interlocked.Increment(ref activeChanges);
        GameWatcherService.AllSessionsStopped += allowExit => stop.TrySetResult(allowExit);

        var session = GameWatcherService.StartSession(
            process,
            WatcherTestHelpers.CreateOptions(process));

        Assert.IsTrue(GameWatcherService.HasActiveSessions);
        Assert.HasCount(1, GameWatcherService.Sessions);

        process.Emit("[12:00:00] [Render thread/INFO]: Crash report saved to: crash.txt");

        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Crashed);
        var allowLauncherExit = await stop.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);

        Assert.IsFalse(allowLauncherExit);
        Assert.IsFalse(GameWatcherService.HasActiveSessions);
        Assert.IsTrue(GameWatcherService.HasLiveSessions);
        Assert.AreEqual(2, activeChanges);
    }

    [TestMethod]
    public async Task CrashedLiveSessionCanStillBeTerminated()
    {
        var process = new FakeGameProcess();
        var session = GameWatcherService.StartSession(
            process,
            WatcherTestHelpers.CreateOptions(process));

        process.Emit("[12:00:00] [Render thread/INFO]: Crash report saved to: crash.txt");
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Crashed);

        Assert.IsTrue(session.CanTerminate);
        Assert.IsTrue(GameWatcherService.HasLiveSessions);

        GameWatcherService.KillAll();

        await WatcherTestHelpers.WaitUntilAsync(() => process.HasExited);
        await WatcherTestHelpers.WaitUntilAsync(() => !GameWatcherService.HasLiveSessions);
    }

    [TestMethod]
    public async Task NormalExitAllowsLauncherExit()
    {
        var process = new FakeGameProcess();
        var stop =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        GameWatcherService.AllSessionsStopped += allowExit => stop.TrySetResult(allowExit);

        var session = GameWatcherService.StartSession(
            process,
            WatcherTestHelpers.CreateOptions(process));

        process.Emit("[12:00:00] [main/INFO]: Setting user: player");
        process.Emit("[12:00:00] [main/INFO]: LWJGL Version: 3.3.3");
        process.Emit("[12:00:00] [main/INFO]: OpenAL initialized");
        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Running);

        process.Complete(0);

        await WatcherTestHelpers.WaitUntilAsync(() => session.State == GameSessionState.Ended);
        var allowLauncherExit = await stop.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);

        Assert.IsTrue(allowLauncherExit);
        Assert.IsFalse(GameWatcherService.HasActiveSessions);
    }

    [TestMethod]
    public async Task SessionCrashedIsForwarded()
    {
        var process = new FakeGameProcess();
        var forwarded = new TaskCompletionSource<GameSessionCrashedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        GameWatcherService.SessionCrashed += (_, e) => forwarded.TrySetResult(e);

        var session = GameWatcherService.StartSession(
            process,
            WatcherTestHelpers.CreateOptions(process));

        process.Emit("[12:00:00] [Render thread/INFO]: Crash report saved to: crash.txt");

        var args = await forwarded.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.AreSame(session, args.Session);
        Assert.AreEqual(GameCrashReason.CrashLogDetected, args.Reason);
    }

    [TestMethod]
    public async Task KillAllCancelsEverySession()
    {
        var first = new FakeGameProcess();
        var second = new FakeGameProcess();
        var firstSession = GameWatcherService.StartSession(
            first,
            WatcherTestHelpers.CreateOptions(first));
        var secondSession = GameWatcherService.StartSession(
            second,
            WatcherTestHelpers.CreateOptions(second));

        GameWatcherService.KillAll();

        await WatcherTestHelpers.WaitUntilAsync(() =>
            firstSession.State == GameSessionState.Canceled
            && secondSession.State == GameSessionState.Canceled);
        await WatcherTestHelpers.WaitUntilAsync(() => !GameWatcherService.HasActiveSessions);

        Assert.AreEqual(GameSessionState.Canceled, firstSession.State);
        Assert.AreEqual(GameSessionState.Canceled, secondSession.State);
    }

    [TestMethod]
    public async Task StartingNewSessionPrunesFinishedSessions()
    {
        var first = new FakeGameProcess();
        var firstSession = GameWatcherService.StartSession(
            first,
            WatcherTestHelpers.CreateOptions(first));
        first.Emit("[12:00:00] [main/INFO]: Crash report saved to: crash.txt");
        await WatcherTestHelpers.WaitUntilAsync(() =>
            firstSession.State == GameSessionState.Crashed);
        first.Complete(1);
        await WatcherTestHelpers.WaitUntilAsync(() => firstSession.HasExited);

        var second = new FakeGameProcess();
        var secondSession = GameWatcherService.StartSession(
            second,
            WatcherTestHelpers.CreateOptions(second));

        Assert.HasCount(1, GameWatcherService.Sessions);
        Assert.AreSame(secondSession, GameWatcherService.Sessions[0]);
        Assert.IsTrue(first.Disposed);
        Assert.IsTrue(GameWatcherService.HasActiveSessions);
    }

    [TestMethod]
    public async Task StartingNewSessionKeepsCrashedLiveSession()
    {
        var first = new FakeGameProcess();
        var firstSession = GameWatcherService.StartSession(
            first,
            WatcherTestHelpers.CreateOptions(first));
        first.Emit("[12:00:00] [main/INFO]: Crash report saved to: crash.txt");
        await WatcherTestHelpers.WaitUntilAsync(() =>
            firstSession.State == GameSessionState.Crashed);

        Assert.IsFalse(first.HasExited);
        Assert.IsTrue(GameWatcherService.HasLiveSessions);

        var second = new FakeGameProcess();
        GameWatcherService.StartSession(
            second,
            WatcherTestHelpers.CreateOptions(second));

        Assert.HasCount(2, GameWatcherService.Sessions);
        Assert.IsFalse(first.Disposed);
        Assert.IsTrue(GameWatcherService.HasLiveSessions);

        first.Complete(0);
        await WatcherTestHelpers.WaitUntilAsync(() => firstSession.HasExited);

        var third = new FakeGameProcess();
        GameWatcherService.StartSession(
            third,
            WatcherTestHelpers.CreateOptions(third));

        Assert.HasCount(2, GameWatcherService.Sessions);
        Assert.IsFalse(
            GameWatcherService.Sessions.Any(item => ReferenceEquals(item, firstSession)));
        Assert.IsTrue(first.Disposed);
    }
}
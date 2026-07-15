using PCL.Application.Launching;

namespace PCL.Application.Test;

[TestClass]
public sealed class GameSessionRegistryTests
{
    [TestMethod]
    public void Registry_UsesMonotonicSequenceAndSingleTerminalEvent()
    {
        GameSessionRegistry registry = new();
        List<GameLaunchEvent> events = [];
        registry.LaunchEventPublished += events.Add;

        GameSessionSnapshot session = registry.Start("1.21.5", 1234);
        Assert.IsTrue(registry.PublishOutput(session.SessionId, GameProcessOutputChannel.StandardOutput, "ready"));
        Assert.IsTrue(registry.PublishLanAddress(session.SessionId, "127.0.0.1:25565"));
        Assert.IsTrue(registry.Complete(session.SessionId, 1));
        Assert.IsFalse(registry.Complete(session.SessionId, 0));

        Assert.AreEqual(3, events.Count);
        Assert.AreEqual("started", events[0].Kind);
        Assert.AreEqual("lan-detected", events[1].Kind);
        Assert.AreEqual("crashed", events[2].Kind);
        Assert.AreEqual(GameSessionState.Crashed, events[2].Session.State);
        Assert.IsTrue(events[0].Sequence < events[1].Sequence);
        Assert.IsTrue(events[1].Sequence < events[2].Sequence);

        GameProcessOutput output = registry.ReadOutput(session.SessionId, 0).Single();
        Assert.IsTrue(events[0].Sequence < output.Sequence);
        Assert.IsTrue(output.Sequence < events[1].Sequence);
    }

    [TestMethod]
    public void Registry_ClassifiesCleanAndTerminatedExits()
    {
        GameSessionRegistry registry = new();
        GameSessionSnapshot clean = registry.Start("clean", 10);
        GameSessionSnapshot terminated = registry.Start("terminated", 11);

        registry.Complete(clean.SessionId, 0);
        registry.Complete(terminated.SessionId, -1, terminated: true);

        Assert.IsTrue(registry.TryGetSession(clean.SessionId, out GameSessionSnapshot? cleanResult));
        Assert.AreEqual(GameSessionState.Exited, cleanResult!.State);
        Assert.IsTrue(registry.TryGetSession(terminated.SessionId, out GameSessionSnapshot? terminatedResult));
        Assert.AreEqual(GameSessionState.Terminated, terminatedResult!.State);
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.EventBus;
using System;
using System.Threading.Tasks;

namespace PCL.Core.Test.App.EventBus;

[TestClass]
public class EventBusServiceTest
{
    private record MyEvent(Guid Id, string Name, int Value) : EventDataBase(Id, Name);

    [TestMethod]
    public async Task Publish_Calls_Delegate_Handler()
    {
        var channel = "test-delegate-" + Guid.NewGuid();
        Assert.IsTrue(EventBusService.AddChannel(channel));

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var sub = EventBusService.Subscribe<MyEvent>(channel, ev =>
        {
            tcs.TrySetResult(ev.Value == 42);
            return Task.CompletedTask;
        });

        await EventBusService.PublishAsync(channel, new MyEvent(Guid.NewGuid(), "x", 42));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreEqual(tcs.Task, completed, "Handler was not invoked within timeout");
        Assert.IsTrue(await tcs.Task.ConfigureAwait(false));

        EventBusService.RemoveChannel(channel);
    }

    private class HandlerObject : IEventHandler<MyEvent>
    {
        private readonly TaskCompletionSource<MyEvent> _tcs;
        public HandlerObject(TaskCompletionSource<MyEvent> tcs) => _tcs = tcs;
        public void Dispose() { }
        public Task HandleEventAsync(MyEvent eventData)
        {
            _tcs.TrySetResult(eventData);
            return _tcs.Task;
        }
    }

    [TestMethod]
    public async Task Publish_Calls_IEventHandler_Instance()
    {
        var channel = "test-instance-" + Guid.NewGuid();
        Assert.IsTrue(EventBusService.AddChannel(channel));

        var tcs = new TaskCompletionSource<MyEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new HandlerObject(tcs);
        using var sub = EventBusService.Subscribe<MyEvent>(channel, handler);

        await EventBusService.PublishAsync(channel, new MyEvent(Guid.NewGuid(), "y", 7));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        Assert.AreEqual(tcs.Task, completed, "IEventHandler instance was not invoked");
        Assert.AreEqual(7, (await tcs.Task.ConfigureAwait(false)).Value);

        EventBusService.RemoveChannel(channel);
    }

    [TestMethod]
    public async Task Unsubscribe_Prevents_Handler_Call()
    {
        var channel = "test-unsub-" + Guid.NewGuid();
        Assert.IsTrue(EventBusService.AddChannel(channel));

        var called = false;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sub = EventBusService.Subscribe<MyEvent>(channel, ev =>
        {
            called = true;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        });

        sub.Dispose();

        await EventBusService.PublishAsync(channel, new MyEvent(Guid.NewGuid(), "z", 1));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(300));
        Assert.AreNotEqual(tcs.Task, completed, "Handler should not be called after unsubscribe");
        Assert.IsFalse(called, "Handler flag should remain false");

        EventBusService.RemoveChannel(channel);
    }
}

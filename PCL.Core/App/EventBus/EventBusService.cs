using PCL.Core.App.IoC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.EventBus;

[LifecycleService(LifecycleState.BeforeLoading)]
[LifecycleScope("eventbus", "EventBus")]
public sealed partial class EventBusService
{
    private static readonly ConcurrentDictionary<string,
        ConcurrentDictionary<Type, ConcurrentDictionary<Guid, (Func<EventDataBase, Task> Handler, object? Owner)>>> _Channels = [];

    [LifecycleStop]
    private static Task _StopAsync()
    {
        try
        {
            foreach (var channel in _Channels.Values)
            {
                foreach (var handlersByType in channel.Values)
                {
                    foreach (var entry in handlersByType.Values)
                    {
                        if (entry.Owner is IDisposable d)
                        {
                            try { d.Dispose(); } catch { /* ignore */ }
                        }
                    }
                }
            }

            _Channels.Clear();
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    public static Task PublishAsync<TEventData>(string channelName, TEventData data) where TEventData : EventDataBase
        => _CallChannelAsync(channelName, data);

    /// <summary>
    /// 订阅使用 <c>IEventHandler{TEventData}</c> 的对象实例。
    /// 返回 <see cref="IDisposable"/> 用于取消订阅。
    /// </summary>
    public static IDisposable Subscribe<TEventData>(string channel, IEventHandler<TEventData> handler)
        where TEventData : EventDataBase
    {
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentNullException(nameof(channel));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        if (!_Channels.TryGetValue(channel, out var dataHandler))
        {
            Context.Error($"Channel {channel} not found.");
            throw new InvalidOperationException("No channel found for the given channel identification.");
        }

        var dataType = typeof(TEventData);
        var handlers = dataHandler.GetOrAdd(dataType, _ => []);

        var id = Guid.NewGuid();
        handlers.TryAdd(id, (Wrapper, handler));

        return new Subscription(() =>
        {
            handlers.TryRemove(id, out _);
            if (handlers.IsEmpty)
            {
                dataHandler.TryRemove(dataType, out _);
            }
        });

        Task Wrapper(EventDataBase ev) => handler.HandleEventAsync((TEventData)ev);
    }

    /// <summary>
    /// 订阅一个委托（更轻量）
    /// </summary>
    public static IDisposable Subscribe<TEventData>(string channel, Func<TEventData, Task> handler)
        where TEventData : EventDataBase
    {
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentNullException(nameof(channel));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        if (!_Channels.TryGetValue(channel, out var dataHandler))
        {
            Context.Error($"Channel {channel} not found.");
            throw new InvalidOperationException("No channel found for the given channel identification.");
        }

        var dataType = typeof(TEventData);
        var handlers = dataHandler.GetOrAdd(dataType, _ => []);

        var id = Guid.NewGuid();
        handlers.TryAdd(id, (Wrapper, null));

        return new Subscription(() =>
        {
            handlers.TryRemove(id, out _);
            if (handlers.IsEmpty)
            {
                dataHandler.TryRemove(dataType, out _);
            }
        });

        Task Wrapper(EventDataBase ev) => handler((TEventData)ev);
    }

    /// <summary>
    /// 创建 channel（显式）
    /// </summary>
    public static bool AddChannel(string name) => !string.IsNullOrWhiteSpace(name) && _Channels.TryAdd(name, []);

    public static bool RemoveChannel(string name) => _Channels.TryRemove(name, out _);

    private static Task _CallChannelAsync<TEventData>(string channel, TEventData data)
        where TEventData : EventDataBase
    {
        if (!_Channels.TryGetValue(channel, out var eventHandlers))
        {
            Context.Error($"Channel {channel} not found.");
            throw new InvalidOperationException("No channel found for the given channel identification.");
        }

        return _CallEventHandlerAsync(data, eventHandlers);
    }

    private static Task _CallEventHandlerAsync<TEventData>(TEventData data, ConcurrentDictionary<Type, ConcurrentDictionary<Guid, (Func<EventDataBase, Task> Handler, object? Owner)>> dataHandlers)
        where TEventData : EventDataBase
    {
        var eventType = data.GetType();

        var matching = new List<Func<EventDataBase, Task>>();
        foreach (var (registeredType, handlers) in dataHandlers)
        {
            if (registeredType.IsAssignableFrom(eventType))
            {
                foreach (var entry in handlers.Values.ToImmutableArray())
                {
                    matching.Add(entry.Handler);
                }
            }
        }

        if (matching.Count == 0)
        {
            Context.Error($"No handler found for event data type {eventType.Name}");
            throw new InvalidOperationException("No handler found for the given event data type.");
        }

        var tasks = matching.Select(async h =>
        {
            try
            {
                await h(data).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Context.Error($"Event handler threw an exception: {ex}");
            }
        }).ToImmutableArray();

        return Task.WhenAll(tasks);
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;
        public Subscription(Action dispose) => _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
        public void Dispose()
        {
            var d = Interlocked.Exchange(ref _dispose, null);
            d?.Invoke();
        }
    }
}

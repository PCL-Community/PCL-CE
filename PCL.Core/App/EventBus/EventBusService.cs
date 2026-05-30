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
        ConcurrentDictionary<Type, ConcurrentDictionary<Guid, (Func<EventDataBase, Task> Handler, WeakReference<object>? OwnerRef)>>> _Channels = [];

    /// <summary>
    /// 0 = running, 1 = stopping/closed
    /// </summary>
    private static int _isStopping;

    [LifecycleStop]
    private static Task _StopAsync()
    {
        Interlocked.Exchange(ref _isStopping, 1);
        try
        {
            var channelCount = _Channels.Count;
            var handlerCount = _Channels.Values.Sum(c => c.Values.Sum(h => h.Count));
            _Channels.Clear();
            Context.Info($"EventBus stopping: cleared {channelCount} channels and {handlerCount} handlers.");
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            Context.Error($"Exception while stopping EventBus: {exception}");
            return Task.FromException(exception);
        }
    }

    /// <summary>
    /// Publish an event to a channel. All handlers subscribed to this channel with compatible event data type will be invoked.
    /// </summary>
    /// <exception cref="InvalidOperationException">EventBus is stopping</exception>
    public static Task PublishAsync<TEventData>(string channelName, TEventData data) where TEventData : EventDataBase
    {
        if (Volatile.Read(ref _isStopping) != 0) throw new InvalidOperationException("EventBus is stopping");
        return _CallChannelAsync(channelName, data);
    }

    /// <summary>
    /// 订阅使用 <c>IEventHandler{TEventData}</c> 的对象实例。
    /// 返回 <see cref="IDisposable"/> 用于取消订阅。
    /// </summary>
    /// <exception cref="InvalidOperationException">EventBus is stopping</exception>
    /// <exception cref="InvalidOperationException">Failed to create channel</exception>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/></exception>
    public static IDisposable Subscribe<TEventData>(string channel, IEventHandler<TEventData> handler, bool disposeOwnerOnUnsubscribe = false)
        where TEventData : EventDataBase
    {
        if (Volatile.Read(ref _isStopping) != 0) throw new InvalidOperationException("EventBus is stopping");
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentNullException(nameof(channel));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (!_Channels.TryGetValue(channel, out var dataHandler))
        {
            Context.Trace($"Channel {channel} not found.");
            //throw new InvalidOperationException("No channel found for the given channel identification.");

            // create channel if not exist
            var success = AddChannel(channel);
            if (!success)
            {
                throw new InvalidOperationException("Failed to create channel.");
            }
        }

        dataHandler ??= _Channels[channel]; // ensure dataHandler is not null here

        var dataType = typeof(TEventData);
        var handlers = dataHandler.GetOrAdd(dataType, _ => []);

        var ownerRef = new WeakReference<object>(handler);

        var id = Guid.NewGuid();
        handlers.TryAdd(id, (Wrapper, ownerRef));

        return new Subscription(() =>
        {
            handlers.TryRemove(id, out _);
            if (handlers.IsEmpty)
            {
                dataHandler.TryRemove(dataType, out _);
            }

            // disposal responsibility: if this subscription requested owner disposal, try to dispose target if still alive
            if (disposeOwnerOnUnsubscribe &&
                ownerRef.TryGetTarget(out var tgt) &&
                tgt is IDisposable d)
            {
                // check if any remaining subscription in this channel still references the same owner
                var stillReferenced = dataHandler.Values.Any(dict => dict.Values.Any(e => e.OwnerRef is not null && e.OwnerRef.TryGetTarget(out var other) && ReferenceEquals(other, tgt)));

                if (stillReferenced) return;

                try { d.Dispose(); } catch (Exception ex) { Context.Error($"Exception disposing subscription owner: {ex}"); }
            }
        });

        Task Wrapper(EventDataBase ev)
        {
            if (ownerRef.TryGetTarget(out var target) && target is IEventHandler<TEventData> typed)
            {
                return typed.HandleEventAsync((TEventData)ev);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 订阅一个委托
    /// </summary>
    /// <exception cref="InvalidOperationException">EventBus is stopping</exception>
    /// <exception cref="InvalidOperationException">Failed to create channel</exception>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/></exception>
    public static IDisposable Subscribe<TEventData>(string channel, Func<TEventData, Task> handler)
        where TEventData : EventDataBase
    {
        if (Volatile.Read(ref _isStopping) != 0) throw new InvalidOperationException("EventBus is stopping");
        if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentNullException(nameof(channel));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (!_Channels.TryGetValue(channel, out var dataHandler))
        {
            Context.Trace($"Channel {channel} not found.");
            //throw new InvalidOperationException("No channel found for the given channel identification.");

            // create channel if not exist
            var success = AddChannel(channel);
            if (!success)
            {
                throw new InvalidOperationException("Failed to create channel.");
            }

        }

        dataHandler ??= _Channels[channel]; // ensure dataHandler is not null here

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

    /// <summary>
    /// Remove a channel and all its handlers. Use with caution.
    /// </summary>
    /// <param name="name">Channel name.</param>
    /// <returns><see langword="true"/> if the channel was removed; otherwise, <see langword="false"/>.</returns>
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

    private static Task _CallEventHandlerAsync<TEventData>(TEventData data,
        ConcurrentDictionary<Type, ConcurrentDictionary<Guid, (Func<EventDataBase, Task> Handler, WeakReference<object>? OwnerRef)>> dataHandlers)
        where TEventData : EventDataBase
    {
        var eventType = data.GetType();

        var matching = new List<Func<EventDataBase, Task>>();
        foreach (var (registeredType, handlers) in dataHandlers)
        {
            if (registeredType.IsAssignableFrom(eventType))
            {
                foreach (var kv in handlers.ToImmutableArray())
                {
                    var key = kv.Key;
                    var entry = kv.Value;
                    if (entry.OwnerRef is not null)
                    {
                        if (!entry.OwnerRef.TryGetTarget(out var _))
                        {
                            // owner was collected, remove this subscription
                            handlers.TryRemove(key, out _);
                            continue;
                        }
                    }
                    matching.Add(entry.Handler);
                }
            }
        }

        if (matching.Count == 0)
        {
            Context.Trace($"No handler found for event data type {eventType.Name}");
            return Task.CompletedTask;
            // will not throw Exception
            //throw new InvalidOperationException("No handler found for the given event data type.");
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

        /// <exception cref="ArgumentNullException">The dispose action is null.</exception>
        public Subscription(Action dispose) => _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));

        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public void Dispose()
        {
            var d = Interlocked.Exchange(ref _dispose, null);
            d?.Invoke();
        }
    }
}

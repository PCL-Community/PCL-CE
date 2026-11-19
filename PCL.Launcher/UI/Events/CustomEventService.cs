using System.Collections;
using Avalonia;

namespace PCL;

/// <summary>
///     控件自定义事件的集合，供 XAML 的 CustomEventService.Events 附加属性使用。
/// </summary>
public sealed class CustomEventCollection : IEnumerable<CustomEvent>
{
    public List<CustomEvent> Events { get; } = [];

    public IEnumerator<CustomEvent> GetEnumerator()
    {
        return Events.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
///     控件自定义事件附加属性：沿控件树注册事件集合、单次事件类型与参数。
/// </summary>
public sealed class CustomEventService
{
    public static readonly AttachedProperty<CustomEventCollection?> EventsProperty =
        AvaloniaProperty
            .RegisterAttached<CustomEventService, AvaloniaObject, CustomEventCollection?>("Events");

    public static readonly AttachedProperty<EventType> EventTypeProperty =
        AvaloniaProperty.RegisterAttached<CustomEventService, AvaloniaObject, EventType>(
            "EventType");

    public static readonly AttachedProperty<string?> EventDataProperty =
        AvaloniaProperty.RegisterAttached<CustomEventService, AvaloniaObject, string?>("EventData");

    public static void SetEvents(AvaloniaObject target, CustomEventCollection? value)
    {
        target.SetValue(EventsProperty, value);
    }

    public static CustomEventCollection GetEvents(AvaloniaObject target)
    {
        var value = target.GetValue(EventsProperty);
        if (value is not null) return value;
        value = new CustomEventCollection();
        target.SetValue(EventsProperty, value);
        return value;
    }

    public static void SetEventType(AvaloniaObject target, EventType value)
    {
        target.SetValue(EventTypeProperty, value);
    }

    public static EventType GetEventType(AvaloniaObject target)
    {
        return target.GetValue(EventTypeProperty);
    }

    public static void SetEventData(AvaloniaObject target, string? value)
    {
        target.SetValue(EventDataProperty, value);
    }

    public static string? GetEventData(AvaloniaObject target)
    {
        return target.GetValue(EventDataProperty);
    }

    /// <summary>
    ///     收集控件上的事件并在后台执行。控件自身只在交互完成时调用此方法。
    /// </summary>
    public static void Raise(AvaloniaObject control)
    {
        var events = GetEvents(control).ToList();
        var eventType = GetEventType(control);
        if (eventType != EventType.None)
            events.Add(new CustomEvent(eventType, GetEventData(control) ?? string.Empty));

        if (events.Count == 0) return;

        _ = Task.Run(() =>
        {
            foreach (var customEvent in events)
                try
                {
                    customEvent.Raise();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[UI] 自定义事件执行失败：{customEvent.Type}，{ex.Message}");
                }
        });
    }
}
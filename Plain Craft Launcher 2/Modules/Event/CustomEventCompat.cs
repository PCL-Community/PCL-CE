using System.Collections;
using System.Windows;

namespace PCL;

/// <summary>旧版 XAML 集合包装。</summary>
[System.Windows.Markup.ContentProperty("Events")]
public sealed class CustomEventCollection : IEnumerable<CustomEvent>
{
    public List<CustomEvent> Events { get; } = [];
    public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>旧版 WPF 附加属性服务，供现有内部 XAML 兼容。</summary>
public static class CustomEventService
{
    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.RegisterAttached("Events", typeof(CustomEventCollection), typeof(CustomEventService));
    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(EventType), typeof(CustomEventService), new(EventType.None));
    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(CustomEventService));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEvents(DependencyObject d, CustomEventCollection? v) => d.SetValue(EventsProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static CustomEventCollection GetEvents(DependencyObject d)
    {
        if (d.GetValue(EventsProperty) is not CustomEventCollection c)
            d.SetValue(EventsProperty, c = new());
        return c;
    }
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventType(DependencyObject d, EventType v) => d.SetValue(EventTypeProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static EventType GetEventType(DependencyObject d) => (EventType)d.GetValue(EventTypeProperty);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventData(DependencyObject d, string? v) => d.SetValue(EventDataProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static string? GetEventData(DependencyObject d) => (string?)d.GetValue(EventDataProperty);
}

/// <summary>旧版事件类；通过 <see cref="LegacyEventCompat"/> 处理中文值，委托到 <see cref="EventHandlers"/>。</summary>
public sealed class CustomEvent
{
    public EventType Type { get; init; } = EventType.None;
    public string? Data { get; init; }

    public CustomEvent() { }
    public CustomEvent(EventType type, string? data) => (Type, Data) = (type, data);

    public void Raise()
    {
        if (Type is EventType.None) return;
        EventHandlers.Raise(Type, Data);
    }

    public static void Raise(EventType type, string? data) => new CustomEvent(type, data).Raise();
    public static string GetCustomVariable(string name, string defaultValue = "") => 
        Core.App.States.CustomVariables.TryGetValue(name, out var v) ? v : defaultValue;
}

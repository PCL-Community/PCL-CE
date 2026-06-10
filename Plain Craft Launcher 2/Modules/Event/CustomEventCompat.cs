using System.Collections;
using System.Windows;
using System.Windows.Markup;

namespace PCL;

/// <summary>旧版 XAML 集合包装，供现有内部代码兼容。</summary>
[ContentProperty("Events")]
public class CustomEventCollection : IEnumerable<CustomEvent>
{
    private readonly List<CustomEvent> _events = [];
    public List<CustomEvent> Events => _events;
    public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>旧版 WPF 附加属性服务，供现有内部 XAML 兼容。</summary>
public static class CustomEventService
{
    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.RegisterAttached("Events", typeof(CustomEventCollection), typeof(CustomEventService), new(null));
    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(CustomEvent.EventType), typeof(CustomEventService), new(CustomEvent.EventType.None));
    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(CustomEventService), new(null));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEvents(DependencyObject d, CustomEventCollection? v) => d.SetValue(EventsProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static CustomEventCollection GetEvents(DependencyObject d)
    {
        if (d.GetValue(EventsProperty) is null) d.SetValue(EventsProperty, new CustomEventCollection());
        return (CustomEventCollection)d.GetValue(EventsProperty);
    }
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventType(DependencyObject d, CustomEvent.EventType v) => d.SetValue(EventTypeProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static CustomEvent.EventType GetEventType(DependencyObject d) => (CustomEvent.EventType)d.GetValue(EventTypeProperty);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventData(DependencyObject d, string? v) => d.SetValue(EventDataProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static string? GetEventData(DependencyObject d) => (string?)d.GetValue(EventDataProperty);
}

/// <summary>旧版事件类，委托到 <see cref="EventHandlers"/>。中文名映射在 <see cref="LegacyEventCompat.NameMap"/> 中维护。</summary>
public class CustomEvent
{
    public enum EventType
    {
        None = 0,
        OpenUrl, LaunchGame, CopyText, RefreshHome, ShowDialog, ShowHint, InvokeFunction,
    }

    public EventType Type { get; set; } = EventType.None;
    public string? Data { get; set; }

    public CustomEvent() { }
    public CustomEvent(EventType type, string? data) { Type = type; Data = data; }

    public void Raise()
    {
        if (Type == EventType.None) return;
        ModBase.Log($"[Event] 旧版事件：{Type}, {Data}");
        EventHandlers.Raise((PCL.EventType)Type, Data);
    }

    public static void Raise(EventType type, string? data) => new CustomEvent(type, data).Raise();
    public static string GetCustomVariable(string name, string defaultValue = "") =>
        PCL.Core.App.States.CustomVariables.TryGetValue(name, out var v) ? v : defaultValue;
}

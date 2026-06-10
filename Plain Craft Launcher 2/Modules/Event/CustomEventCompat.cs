using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PCL;

/// <summary>旧版 XAML 集合包装。</summary>
[System.Windows.Markup.ContentProperty("Events")]
public sealed class CustomEventCollection : IList<CustomEvent>
{
    public List<CustomEvent> Events { get; } = [];
    public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => Events.Count;
    public bool IsReadOnly => false;
    public void Add(CustomEvent item) => Events.Add(item);
    public void Clear() => Events.Clear();
    public bool Contains(CustomEvent item) => Events.Contains(item);
    public void CopyTo(CustomEvent[] array, int arrayIndex) => Events.CopyTo(array, arrayIndex);
    public bool Remove(CustomEvent item) => Events.Remove(item);
    public int IndexOf(CustomEvent item) => Events.IndexOf(item);
    public void Insert(int index, CustomEvent item) => Events.Insert(index, item);
    public void RemoveAt(int index) => Events.RemoveAt(index);
    public CustomEvent this[int index]
    {
        get => Events[index];
        set => Events[index] = value;
    }
}

/// <summary>旧版 WPF 附加属性服务，供现有内部 XAML 兼容。</summary>
public static class CustomEventService
{
    private static readonly ConditionalWeakTable<DependencyObject, CustomEventCollection> EventsStore = [];

    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(EventType), typeof(CustomEventService), new(EventType.None));
    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(CustomEventService));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEvents(DependencyObject d, CustomEventCollection? v)
    {
        if (v is not null)
            EventsStore.AddOrUpdate(d, v);
        else
            EventsStore.Remove(d);
    }
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static CustomEventCollection GetEvents(DependencyObject d)
    {
        return EventsStore.GetValue(d, _ => new());
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

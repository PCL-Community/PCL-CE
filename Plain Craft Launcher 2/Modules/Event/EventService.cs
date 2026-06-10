using System.Collections;
using System.Windows;

namespace PCL;

/// <summary>XAML 中声明多个 <see cref="EventAction"/> 的集合。</summary>
[System.Windows.Markup.ContentProperty("Events")]
public sealed class EventActionCollection : IEnumerable<EventAction>
{
    public List<EventAction> Events { get; } = [];
    public IEnumerator<EventAction> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>WPF 附加属性：EventType、EventData、Events。</summary>
public static class EventService
{
    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.RegisterAttached("Events", typeof(EventActionCollection), typeof(EventService));
    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(EventType), typeof(EventService), new(EventType.None));
    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(EventService));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEvents(DependencyObject d, EventActionCollection? v) => d.SetValue(EventsProperty, v);
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static EventActionCollection GetEvents(DependencyObject d)
    {
        if (d.GetValue(EventsProperty) is not EventActionCollection c)
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

using System.Collections;
using System.Windows;
using System.Windows.Markup;

namespace PCL;

/// <summary>
///     XAML 中声明多个 <see cref="EventAction"/> 的集合。
/// </summary>
[ContentProperty("Events")]
public class EventActionCollection : IEnumerable<EventAction>
{
    private readonly List<EventAction> _events = [];

    public List<EventAction> Events => _events;
    public IEnumerator<EventAction> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
///     为 WPF 控件提供事件相关附加属性。
/// </summary>
public static class EventService
{
    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.RegisterAttached("Events", typeof(EventActionCollection), typeof(EventService),
            new PropertyMetadata(null));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEvents(DependencyObject d, EventActionCollection? value) => d.SetValue(EventsProperty, value);

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static EventActionCollection GetEvents(DependencyObject d)
    {
        if (d.GetValue(EventsProperty) is null)
            d.SetValue(EventsProperty, new EventActionCollection());
        return (EventActionCollection)d.GetValue(EventsProperty);
    }

    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(EventType), typeof(EventService),
            new PropertyMetadata(EventType.None));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventType(DependencyObject d, EventType value) => d.SetValue(EventTypeProperty, value);

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static EventType GetEventType(DependencyObject d) => (EventType)d.GetValue(EventTypeProperty);

    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(EventService),
            new PropertyMetadata(null));

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static void SetEventData(DependencyObject d, string? value) => d.SetValue(EventDataProperty, value);

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static string? GetEventData(DependencyObject d) => (string?)d.GetValue(EventDataProperty);
}

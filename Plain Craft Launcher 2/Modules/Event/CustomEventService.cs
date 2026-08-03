using System.Windows;

namespace PCL
{
    public static class CustomEventService
    {
        public static readonly DependencyProperty EventsProperty =
            DependencyProperty.RegisterAttached(
                "Events",
                typeof(CustomEventCollection),
                typeof(CustomEventService),
                new PropertyMetadata(null));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEvents(DependencyObject d, CustomEventCollection value) =>
            d.SetValue(EventsProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static CustomEventCollection GetEvents(DependencyObject d)
        {
            if (d.GetValue(EventsProperty) is null)
                d.SetValue(EventsProperty, new CustomEventCollection());
            return (CustomEventCollection)d.GetValue(EventsProperty);
        }

        public static readonly DependencyProperty EventTypeProperty =
            DependencyProperty.RegisterAttached(
                "EventType",
                typeof(EventType),
                typeof(CustomEventService),
                new PropertyMetadata(EventType.None));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEventType(DependencyObject d, EventType value) =>
            d.SetValue(EventTypeProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static EventType GetEventType(DependencyObject d) =>
            (EventType)d.GetValue(EventTypeProperty);

        public static readonly DependencyProperty EventDataProperty =
            DependencyProperty.RegisterAttached(
                "EventData",
                typeof(string),
                typeof(CustomEventService),
                new PropertyMetadata(null));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEventData(DependencyObject d, string value) =>
            d.SetValue(EventDataProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static string GetEventData(DependencyObject d) =>
            (string)d.GetValue(EventDataProperty);
    }
}

using System.Windows;

namespace PCL;

public static partial class Launcher
{
    public static void RaiseCustomEvent(DependencyObject control)
    {
        var events = CustomEventService.GetEvents(control).ToList();
        var eventType = CustomEventService.GetEventType(control);
        if (eventType != EventType.None)
            events.Add(new CustomEvent(eventType, CustomEventService.GetEventData(control)));

        if (events.Count == 0) return;

        Common.RunInNewThread(
            () =>
            {
                foreach (var e in events)
                    e.Raise();
            },
            $"执行自定义事件 {Common.GetUuid()}");
    }
}

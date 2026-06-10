namespace PCL;

/// <summary>
///     表示一个用户触发的事件动作。
/// </summary>
public class EventAction
{
    public EventType Type { get; set; } = EventType.None;
    public string? Data { get; set; }

    public EventAction() { }

    public EventAction(EventType type, string? data) { Type = type; Data = data; }

    /// <summary>执行事件。</summary>
    public void Raise() => EventHandlers.Raise(Type, Data);

    /// <summary>静态便捷重载。</summary>
    public static void Raise(EventType type, string? data) => EventHandlers.Raise(type, data);
}

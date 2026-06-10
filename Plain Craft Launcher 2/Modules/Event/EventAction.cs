namespace PCL;

/// <summary>表示一个事件动作。</summary>
public sealed class EventAction
{
    public EventType Type { get; init; } = EventType.None;
    public string? Data { get; init; }

    public EventAction() { }
    public EventAction(EventType type, string? data) => (Type, Data) = (type, data);
}

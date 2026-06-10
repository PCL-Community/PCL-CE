using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;
using PCL.Core.App.Localization;

namespace PCL;

// ── 向后兼容层：保持旧版 CustomEvent / CustomEventService / CustomEventCollection 可用 ──

[ContentProperty("Events")]
public class CustomEventCollection : IEnumerable<CustomEvent>
{
    private readonly List<CustomEvent> _events = [];
    public List<CustomEvent> Events => _events;
    public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

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

public class CustomEvent
{
    /// <summary>
    ///     旧版 EventType 枚举，英文值在前（0-6），中文值在后（100+）。
    ///     内部 XAML 中 local:CustomEventService.EventType="打开网页" 依赖 WPF 枚举解析该中文值。
    ///     AnnouncementService 使用 Enum.TryParse 解析远程按钮命令，同时支持中英文。
    /// </summary>
    public enum EventType
    {
        None = 0,
        OpenUrl, LaunchGame, CopyText, RefreshHome, ShowDialog, ShowHint, InvokeFunction,
        打开网页 = 100, 打开文件, 执行命令, 启动游戏, 复制文本, 刷新主页, 刷新页面,
        今日人品, 清理垃圾, 弹出窗口, 弹出提示, 切换页面, 导入整合包, 安装整合包,
        下载文件, 修改设置, 写入设置, 修改变量, 写入变量,
    }

    private static readonly Dictionary<EventType, PCL.EventType> Map = new()
    {
        [EventType.OpenUrl] = PCL.EventType.OpenUrl,       [EventType.打开网页] = PCL.EventType.OpenUrl,
        [EventType.LaunchGame] = PCL.EventType.LaunchGame,  [EventType.启动游戏] = PCL.EventType.LaunchGame,
        [EventType.CopyText] = PCL.EventType.CopyText,      [EventType.复制文本] = PCL.EventType.CopyText,
        [EventType.RefreshHome] = PCL.EventType.RefreshHome, [EventType.刷新主页] = PCL.EventType.RefreshHome,
        [EventType.ShowDialog] = PCL.EventType.ShowDialog,  [EventType.弹出窗口] = PCL.EventType.ShowDialog,
        [EventType.ShowHint] = PCL.EventType.ShowHint,      [EventType.弹出提示] = PCL.EventType.ShowHint,
        [EventType.InvokeFunction] = PCL.EventType.InvokeFunction,
        [EventType.刷新页面] = PCL.EventType.RefreshHome,
    };

    public EventType Type { get; set; } = EventType.None;
    public string? Data { get; set; }

    public CustomEvent() { }
    public CustomEvent(EventType type, string? data) { Type = type; Data = data; }

    public void Raise()
    {
        if (Type == EventType.None) return;
        ModBase.Log($"[Event] 旧版事件：{Type}, {Data}");
        if (Map.TryGetValue(Type, out var t))
            EventHandlers.Raise(t, Data);
        else
            ModMain.Hint(Lang.Text("Event.Hint.LegacyTypeRemoved", Type.ToString()), ModMain.HintType.Critical);
    }

    public static void Raise(EventType type, string? data) => new CustomEvent(type, data).Raise();
    public static string GetCustomVariable(string name, string defaultValue = "") =>
        PCL.Core.App.States.CustomVariables.TryGetValue(name, out var v) ? v : defaultValue;
}

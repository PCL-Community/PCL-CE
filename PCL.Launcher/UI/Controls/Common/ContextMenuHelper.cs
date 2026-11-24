using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PCL.UI.Controls;

/// <summary>
///     统一 Menu 键、Shift+F10 与触摸长按的上下文菜单入口。
///     右键仍由 Avalonia 的 ContextMenu 属性处理，避免重复弹出。
/// </summary>
public static class ContextMenuHelper
{
    public static void Attach(Control target, Func<ContextMenu?> factory)
    {
        target.AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                if (e.Key != Key.Apps
                    && e is not { Key: Key.F10, KeyModifiers: KeyModifiers.Shift })
                    return;
                var menu = factory();
                if (menu is null) return;
                menu.Open(target);
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);

        target.AddHandler(
            InputElement.HoldingEvent,
            (_, e) =>
            {
                if (e.HoldingState != HoldingState.Started) return;
                var menu = factory();
                if (menu is null) return;
                menu.Open(target);
                e.Handled = true;
            },
            RoutingStrategies.Tunnel);
    }
}
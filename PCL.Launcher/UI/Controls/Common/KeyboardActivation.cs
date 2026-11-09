using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PCL.UI.Controls;

/// <summary>为自定义可点击控件提供 Enter/Space 键盘激活。</summary>
public static class KeyboardActivation
{
    public static void Attach(
        Control control,
        Action action,
        Func<bool>? canInvoke = null,
        bool focusable = true)
    {
        Attach(control, _ => action(), canInvoke, focusable);
    }

    public static void Attach(
        Control control,
        Action<KeyModifiers> action,
        Func<bool>? canInvoke = null,
        bool focusable = true)
    {
        if (focusable)
            control.Focusable = true;
        control.AddHandler(
            InputElement.KeyDownEvent,
            (_, e) =>
            {
                if (e.Handled || e.Key is not (Key.Enter or Key.Space))
                    return;
                if (canInvoke?.Invoke() == false)
                    return;
                e.Handled = true;
                action(e.KeyModifiers);
            },
            RoutingStrategies.Bubble);
    }
}
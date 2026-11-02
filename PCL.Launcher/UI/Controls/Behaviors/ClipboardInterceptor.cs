using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace PCL.UI.Controls.Behaviors;

/// <summary>
///     为 TextBox 提供带异常保护的复制/剪切/粘贴路径；外部程序占用剪贴板时不抛出未处理异常。
/// </summary>
public sealed class ClipboardInterceptor
{
    public static readonly AttachedProperty<bool> EnableSafeClipboardProperty =
        AvaloniaProperty.RegisterAttached<ClipboardInterceptor, TextBox, bool>(
            "EnableSafeClipboard");

    private static readonly ConditionalWeakTable<TextBox, State> States = new();

    public static void SetEnableSafeClipboard(TextBox textBox, bool value)
    {
        textBox.SetValue(EnableSafeClipboardProperty, value);
    }

    public static bool GetEnableSafeClipboard(TextBox textBox)
    {
        return textBox.GetValue(EnableSafeClipboardProperty);
    }

    static ClipboardInterceptor()
    {
        EnableSafeClipboardProperty.Changed.AddClassHandler<TextBox>((textBox, args) =>
        {
            if (args.NewValue is true)
                Attach(textBox);
            else
                Detach(textBox);
        });
    }

    private static void Attach(TextBox textBox)
    {
        if (States.TryGetValue(textBox, out _)) return;
        var state = new State();
        States.Add(textBox, state);
        textBox.CopyingToClipboard += state.OnCopying;
        textBox.CuttingToClipboard += state.OnCutting;
        textBox.PastingFromClipboard += state.OnPasting;
    }

    private static void Detach(TextBox textBox)
    {
        if (!States.TryGetValue(textBox, out var state)) return;
        textBox.CopyingToClipboard -= state.OnCopying;
        textBox.CuttingToClipboard -= state.OnCutting;
        textBox.PastingFromClipboard -= state.OnPasting;
        States.Remove(textBox);
    }

    private static IClipboard? GetClipboard(TextBox textBox)
    {
        return TopLevel.GetTopLevel(textBox)?.Clipboard;
    }

    private sealed class State
    {
        public async void OnCopying(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox { SelectionStart: var start, SelectionEnd: var end } textBox
                || start == end)
                return;

            var clipboard = GetClipboard(textBox);
            if (clipboard is null) return;
            try
            {
                await clipboard.SetTextAsync(textBox.SelectedText);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[UI] 复制失败：{ex.Message}");
            }
        }

        public async void OnCutting(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.SelectionStart == textBox.SelectionEnd)
                return;

            var clipboard = GetClipboard(textBox);
            if (clipboard is null) return;
            try
            {
                await clipboard.SetTextAsync(textBox.SelectedText);
                textBox.SelectedText = string.Empty;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[UI] 剪切失败：{ex.Message}");
            }
        }

        public async void OnPasting(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            var clipboard = GetClipboard(textBox);
            if (clipboard is null) return;
            try
            {
                var text = await clipboard.TryGetTextAsync();
                if (string.IsNullOrEmpty(text)) return;
                textBox.SelectedText = text;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[UI] 粘贴失败：{ex.Message}");
            }
        }
    }
}
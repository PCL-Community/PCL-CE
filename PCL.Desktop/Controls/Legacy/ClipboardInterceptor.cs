// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.
//
// Implements the PCL clipboard safety hook for text input controls. The WPF
// version intercepted Copy/Cut/Paste commands to avoid OpenClipboard failures;
// this Avalonia port keeps the same attached property surface and routes the
// shortcuts through Avalonia's cross-platform clipboard abstraction.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace PCL.Desktop.Controls.Legacy;

public sealed class ClipboardInterceptor
{
    public static readonly AttachedProperty<bool> EnableSafeClipboardProperty =
        AvaloniaProperty.RegisterAttached<ClipboardInterceptor, InputElement, bool>(
            "EnableSafeClipboard",
            false);

    private static readonly AttachedProperty<bool> IsHookedProperty =
        AvaloniaProperty.RegisterAttached<ClipboardInterceptor, InputElement, bool>(
            "IsHooked",
            false);

    static ClipboardInterceptor()
    {
        EnableSafeClipboardProperty.Changed.AddClassHandler<InputElement>(EnableSafeClipboardChanged);
    }

    private ClipboardInterceptor()
    {
    }

    public static bool GetEnableSafeClipboard(InputElement element) =>
        element.GetValue(EnableSafeClipboardProperty);

    public static void SetEnableSafeClipboard(InputElement element, bool value) =>
        element.SetValue(EnableSafeClipboardProperty, value);

    private static void EnableSafeClipboardChanged(InputElement element, AvaloniaPropertyChangedEventArgs e)
    {
        bool enabled = e.NewValue is true;
        bool hooked = element.GetValue(IsHookedProperty);
        if (enabled && !hooked)
        {
            element.KeyDown += ElementKeyDown;
            element.SetValue(IsHookedProperty, true);
        }
        else if (!enabled && hooked)
        {
            element.KeyDown -= ElementKeyDown;
            element.SetValue(IsHookedProperty, false);
        }
    }

    private static async void ElementKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox ||
            e.Handled ||
            !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.C:
                e.Handled = await CopyAsync(textBox).ConfigureAwait(true);
                break;
            case Key.X:
                e.Handled = await CutAsync(textBox).ConfigureAwait(true);
                break;
            case Key.V:
                e.Handled = await PasteAsync(textBox).ConfigureAwait(true);
                break;
        }
    }

    private static async Task<bool> CopyAsync(TextBox textBox)
    {
        string selectedText = textBox.SelectedText;
        if (string.IsNullOrEmpty(selectedText))
            return false;

        IClipboard? clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
        if (clipboard is null)
            return false;

        await clipboard.SetTextAsync(selectedText).ConfigureAwait(true);
        return true;
    }

    private static async Task<bool> CutAsync(TextBox textBox)
    {
        if (textBox.IsReadOnly || string.IsNullOrEmpty(textBox.SelectedText))
            return false;

        if (!await CopyAsync(textBox).ConfigureAwait(true))
            return false;

        ReplaceSelection(textBox, string.Empty);
        return true;
    }

    private static async Task<bool> PasteAsync(TextBox textBox)
    {
        if (textBox.IsReadOnly)
            return false;

        IClipboard? clipboard = TopLevel.GetTopLevel(textBox)?.Clipboard;
        if (clipboard is null)
            return false;

        string? text = await clipboard.TryGetTextAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(text))
            return false;

        ReplaceSelection(textBox, text);
        return true;
    }

    private static void ReplaceSelection(TextBox textBox, string replacement)
    {
        string text = textBox.Text ?? string.Empty;
        int start = Math.Clamp(Math.Min(textBox.SelectionStart, textBox.SelectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(textBox.SelectionStart, textBox.SelectionEnd), 0, text.Length);

        if (replacement.Length > 0 && textBox.MaxLength > 0)
        {
            int allowed = Math.Max(0, textBox.MaxLength - (text.Length - (end - start)));
            if (replacement.Length > allowed)
                replacement = replacement[..allowed];
        }

        textBox.Text = string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(end));
        textBox.CaretIndex = start + replacement.Length;
        textBox.SelectionStart = textBox.CaretIndex;
        textBox.SelectionEnd = textBox.CaretIndex;
    }
}

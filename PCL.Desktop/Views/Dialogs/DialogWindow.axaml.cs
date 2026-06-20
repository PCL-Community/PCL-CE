// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Views.Dialogs;

public enum DialogMode
{
    Message,
    Confirm,
    Prompt
}

public sealed record DialogResult(
    bool Accepted,
    string? Value);

public sealed partial class DialogWindow : Window
{
    private readonly DialogMode _mode;
    private readonly TextBox _promptText;

    public DialogWindow()
        : this(string.Empty, string.Empty, DialogMode.Message)
    {
    }

    public DialogWindow(
        string title,
        string message,
        DialogMode mode,
        string? defaultValue = null)
    {
        _mode = mode;
        AvaloniaXamlLoader.Load(this);
        Title = title;
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        _promptText = this.FindControl<TextBox>("PromptText")!;
        _promptText.Text = defaultValue;
        _promptText.IsVisible = mode == DialogMode.Prompt;
        this.FindControl<Button>("CancelButton")!.IsVisible =
            mode != DialogMode.Message;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_mode == DialogMode.Prompt)
        {
            _promptText.Focus();
            _promptText.SelectAll();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape:
                Close(new DialogResult(false, null));
                e.Handled = true;
                break;
            case Key.Enter:
                Accept();
                e.Handled = true;
                break;
        }
    }

    private void AcceptButton_OnClick(
        object? sender,
        RoutedEventArgs e) =>
        Accept();

    private void CancelButton_OnClick(
        object? sender,
        RoutedEventArgs e) =>
        Close(new DialogResult(false, null));

    private void Accept() =>
        Close(
            new DialogResult(
                true,
                _mode == DialogMode.Prompt ? _promptText.Text : null));
}

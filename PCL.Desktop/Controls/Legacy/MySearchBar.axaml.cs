// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1711
public sealed partial class MySearchBar : MyCard
{
    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MySearchBar, string>(nameof(HintText), string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MySearchBar, string>(nameof(Text), string.Empty);

    private readonly MyTextBox? _textBox;
    private readonly MyIconButton? _clearButton;
    private bool _updatingText;

    public MySearchBar()
    {
        AvaloniaXamlLoader.Load(this);
        _textBox = this.FindControl<MyTextBox>("TextBox");
        _clearButton = this.FindControl<MyIconButton>("BtnClear");

        this.GetObservable(HintTextProperty).Subscribe(hint =>
        {
            if (_textBox is not null)
                _textBox.HintText = hint;
        });
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_textBox is null || _textBox.Text == text)
                return;

            _updatingText = true;
            _textBox.Text = text;
            _updatingText = false;
            UpdateClearButtonState(animate: false);
        });
        UpdateClearButtonState(animate: false);
    }

    public event TextChangedEventHandler? TextChanged;

    public string HintText
    {
        get => GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private void Text_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_textBox is null)
            return;

        if (!_updatingText)
            SetCurrentValue(TextProperty, _textBox.Text ?? string.Empty);

        UpdateClearButtonState(animate: true);
        TextChanged?.Invoke(sender ?? this, e);
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        if (_textBox is not null)
            _textBox.Text = string.Empty;
        _textBox?.Focus();
    }

    private void UpdateClearButtonState(bool animate)
    {
        if (_clearButton is null)
            return;

        bool hasText = !string.IsNullOrEmpty(_textBox?.Text);
        _clearButton.IsHitTestVisible = hasText;
        AnimateClearButton(hasText ? 1d : 0d, animate);
    }

    private void AnimateClearButton(double targetOpacity, bool animate)
    {
        if (_clearButton is null)
            return;

        if (!animate)
        {
            ModAnimation.AniStop($"MySearchBar ClearBtn {GetHashCode()}");
            _clearButton.Opacity = targetOpacity;
            return;
        }

        ModAnimation.AniStart(
            ModAnimation.AaOpacity(_clearButton, targetOpacity - _clearButton.Opacity, 90),
            $"MySearchBar ClearBtn {GetHashCode()}");
    }
}
#pragma warning restore CA1711

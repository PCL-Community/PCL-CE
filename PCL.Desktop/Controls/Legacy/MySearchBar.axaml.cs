// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public sealed partial class MySearchBar : MyCard
{
    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MySearchBar, string>(nameof(HintText), string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MySearchBar, string>(nameof(Text), string.Empty);

    private readonly MyTextBox? _textBox;
    private readonly MyIconButton? _clearButton;
    private readonly Stopwatch _clearAnimationClock = new();
    private DispatcherTimer? _clearAnimationTimer;
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

    public event EventHandler? TextChanged;

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
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        Text = string.Empty;
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

        _clearAnimationTimer?.Stop();
        if (!animate)
        {
            _clearButton.Opacity = targetOpacity;
            return;
        }

        double startOpacity = _clearButton.Opacity;
        _clearAnimationClock.Restart();
        _clearAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _clearAnimationTimer.Tick += (_, _) =>
        {
            double progress = Math.Clamp(_clearAnimationClock.Elapsed.TotalMilliseconds / 90d, 0d, 1d);
            _clearButton.Opacity = startOpacity + (targetOpacity - startOpacity) * progress;
            if (progress < 1d)
                return;

            _clearButton.Opacity = targetOpacity;
            _clearAnimationTimer?.Stop();
            _clearAnimationTimer = null;
        };
        _clearAnimationTimer.Start();
    }
}

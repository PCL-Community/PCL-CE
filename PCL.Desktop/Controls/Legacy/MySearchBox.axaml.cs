// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1711
public partial class MySearchBox : MyCard
{
    public delegate void SearchEventHandler(object sender, EventArgs e);

    public delegate void TextChangedEventHandler(object sender, EventArgs e);

    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<MySearchBox, string>(nameof(HintText), string.Empty);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MySearchBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<bool> SearchButtonVisibilityProperty =
        AvaloniaProperty.Register<MySearchBox, bool>(nameof(SearchButtonVisibility));

    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MySearchBox, object?>(nameof(ToolTip));

    private readonly MyTextBox? _textBox;
    private readonly MyIconButton? _clearButton;
    private readonly MyButton? _searchButton;
    private bool _updatingText;

    public MySearchBox()
    {
        AvaloniaXamlLoader.Load(this);
        _textBox = this.FindControl<MyTextBox>("TextBox");
        _clearButton = this.FindControl<MyIconButton>("BtnClear");
        _searchButton = this.FindControl<MyButton>("BtnSearch");

        if (_textBox is not null)
            _textBox.KeyUp += MySearchBox_KeyUp;

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
        this.GetObservable(SearchButtonVisibilityProperty).Subscribe(ApplySearchButtonVisibility);
        this.GetObservable(ToolTipProperty).Subscribe(tip => Avalonia.Controls.ToolTip.SetTip(this, tip));
        AttachedToVisualTree += (_, _) =>
        {
            _textBox?.Focus();
            UpdateClearButtonState(animate: false);
        };
    }

    public event TextChangedEventHandler? TextChanged;

    public event SearchEventHandler? Search;

    public int Uuid { get; } = Random.Shared.Next();

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

    public bool SearchButtonVisibility
    {
        get => GetValue(SearchButtonVisibilityProperty);
        set => SetValue(SearchButtonVisibilityProperty, value);
    }

    public bool SearchButtonVisible
    {
        get => SearchButtonVisibility;
        set => SearchButtonVisibility = value;
    }

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
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

    private void BtnSearch_Click(object? sender, EventArgs e)
    {
        Search?.Invoke(sender ?? this, e);
    }

    private void MySearchBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        Search?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ApplySearchButtonVisibility(bool isVisible)
    {
        if (_clearButton is not null)
            _clearButton.Margin = new Thickness(0d, 0d, isVisible ? 70d : 10d, 0d);
        if (_searchButton is not null)
            _searchButton.IsVisible = isVisible;
    }

    private void UpdateClearButtonState(bool animate)
    {
        if (_clearButton is null)
            return;

        bool hasText = !string.IsNullOrEmpty(_textBox?.Text);
        _clearButton.IsHitTestVisible = hasText;
        if (!animate)
        {
            ModAnimation.AniStop("MySearchBox ClearBtn " + Uuid);
            _clearButton.Opacity = hasText ? 1d : 0d;
            return;
        }

        ModAnimation.AniStart(
            ModAnimation.AaOpacity(_clearButton, hasText ? 1d - _clearButton.Opacity : -_clearButton.Opacity, 90),
            "MySearchBox ClearBtn " + Uuid);
    }
}
#pragma warning restore CA1711

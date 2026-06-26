// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public enum MyHintTheme
{
    Red,
    Yellow,
    Blue
}

public partial class MyHint : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyHint, string>(nameof(Text), string.Empty);

    public new static readonly StyledProperty<MyHintTheme> ThemeProperty =
        AvaloniaProperty.Register<MyHint, MyHintTheme>(nameof(Theme));

    public static readonly StyledProperty<bool> CanCloseProperty =
        AvaloniaProperty.Register<MyHint, bool>(nameof(CanClose));

    private readonly TextBlock? _label;
    private readonly MyIconButton? _closeButton;

    public MyHint()
    {
        AvaloniaXamlLoader.Load(this);
        _label = this.FindControl<TextBlock>("LabText");
        _closeButton = this.FindControl<MyIconButton>("BtnClose");
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(ThemeProperty).Subscribe(_ => RefreshTheme());
        this.GetObservable(CanCloseProperty).Subscribe(value =>
        {
            if (_closeButton is not null)
                _closeButton.IsVisible = value;
        });
        RefreshTheme();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public new MyHintTheme Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    private void BtnClose_Click(object? sender, EventArgs e) => IsVisible = false;

    private void RefreshTheme()
    {
        var color = Theme switch
        {
            MyHintTheme.Yellow => Color.Parse("#f39c12"),
            MyHintTheme.Blue => Color.Parse("#1370f3"),
            _ => Color.Parse("#ff4444")
        };
        BorderBrush = new SolidColorBrush(color);
        Background = new SolidColorBrush(Color.FromArgb(0x10, color.R, color.G, color.B));
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public enum MyButtonColorType
{
    Normal,
    Highlight,
    Red,
    Gray
}

public partial class MyButton : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<MyButtonColorType> ColorTypeProperty =
        AvaloniaProperty.Register<MyButton, MyButtonColorType>(nameof(ColorType));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyButton, object?>(nameof(CommandParameter));

    private readonly Border? _foregroundBorder;
    private readonly TextBlock? _label;
    private bool _isPressed;

    public MyButton()
    {
        AvaloniaXamlLoader.Load(this);
        _foregroundBorder = this.FindControl<Border>("PanFore");
        _label = this.FindControl<TextBlock>("LabText");

        PointerEntered += (_, _) => RefreshVisual();
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshVisual();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(ColorTypeProperty).Subscribe(_ => RefreshVisual());
        RefreshVisual();
    }

    public event EventHandler? Click;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public MyButtonColorType ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        RefreshVisual();
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        RefreshVisual();
        var parameter = CommandParameter;
        if (Command?.CanExecute(parameter) == true)
            Command.Execute(parameter);
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void RefreshVisual()
    {
        if (_foregroundBorder is null || _label is null)
            return;

        var accent = ColorType switch
        {
            MyButtonColorType.Normal => Color.Parse("#343d4a"),
            MyButtonColorType.Red => Color.Parse("#ce2111"),
            MyButtonColorType.Gray => Color.Parse("#737373"),
            _ => Color.Parse("#1370f3")
        };
        var alpha = _isPressed ? 0x24 : IsPointerOver ? 0x18 : 0x00;
        _foregroundBorder.BorderBrush = new SolidColorBrush(accent);
        _foregroundBorder.Background = new SolidColorBrush(Color.FromArgb((byte)alpha, accent.R, accent.G, accent.B));
        _label.Foreground = new SolidColorBrush(accent);
    }
}

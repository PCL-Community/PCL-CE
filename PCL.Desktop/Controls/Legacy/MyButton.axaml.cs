// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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
    public enum ColorState
    {
        Normal,
        Highlight,
        Red,
        Gray
    }

    private const int AnimationColorIn = 100;
    private const int AnimationColorOut = 200;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ColorState> ColorTypeProperty =
        AvaloniaProperty.Register<MyButton, ColorState>(nameof(ColorType));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MyButton, object?>(nameof(ToolTip));

    public new static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MyButton, Thickness>(nameof(Padding), new Thickness());

    public static readonly StyledProperty<Thickness> TextPaddingProperty =
        AvaloniaProperty.Register<MyButton, Thickness>(nameof(TextPadding), new Thickness());

    private readonly Border? _foregroundBorder;
    private readonly TextBlock? _label;
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private bool _isPressed;

    public MyButton()
    {
        AvaloniaXamlLoader.Load(this);
        _foregroundBorder = this.FindControl<Border>("PanFore");
        _label = this.FindControl<TextBlock>("LabText");
        Cursor = new Cursor(StandardCursorType.Hand);

        PointerEntered += (_, _) =>
        {
            RefreshColor();
            ButtonMouseEnter();
        };
        PointerExited += (_, _) =>
        {
            RefreshColor();
            ButtonMouseLeave();
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        this.GetObservable(TextProperty).Subscribe(text =>
        {
            if (_label is not null)
                _label.Text = text;
        });
        this.GetObservable(ColorTypeProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(ToolTipProperty).Subscribe(tip => Avalonia.Controls.ToolTip.SetTip(this, tip));
        this.GetObservable(PaddingProperty).Subscribe(padding =>
        {
            if (_foregroundBorder is not null)
                _foregroundBorder.Padding = padding;
        });
        this.GetObservable(TextPaddingProperty).Subscribe(padding =>
        {
            if (_label is not null)
                _label.Padding = padding;
        });
        RefreshColor();
    }

    public event EventHandler? Click;

    public event EventHandler<PointerReleasedEventArgs>? ClickReleased;

    public InlineCollection Inlines =>
        _label?.Inlines ?? throw new InvalidOperationException("MyButton text block is not initialized.");

    public ITransform? RealRenderTransform
    {
        get => _foregroundBorder?.RenderTransform;
        set
        {
            if (_foregroundBorder is not null)
                _foregroundBorder.RenderTransform = value;
        }
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ColorState ColorType
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

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public new Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Thickness TextPadding
    {
        get => GetValue(TextPaddingProperty);
        set => SetValue(TextPaddingProperty, value);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || _foregroundBorder is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        Focus();
        ModAnimation.AniStart(
            new List<ModAnimation.AniData>
            {
                ModAnimation.AaScaleTransform(
                    _foregroundBorder,
                    0.955d - GetForegroundScale(),
                    80,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                ModAnimation.AaScaleTransform(_foregroundBorder, -0.01d, 700, ease: new ModAnimation.AniEaseOutFluent())
            },
            $"MyButton Scale {_uuid}");
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed || _foregroundBorder is null)
            return;

        _isPressed = false;
        var parameter = CommandParameter;
        if (Command?.CanExecute(parameter) == true)
            Command.Execute(parameter);
        ClickReleased?.Invoke(this, e);
        Click?.Invoke(this, EventArgs.Empty);
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _foregroundBorder,
                1d - GetForegroundScale(),
                300,
                10,
                new ModAnimation.AniEaseOutFluent()),
            $"MyButton Scale {_uuid}");
        e.Handled = true;
    }

    private void RefreshColor()
    {
        if (_foregroundBorder is null || _label is null)
            return;

        string resourceKey = IsEnabled ? GetBorderBrushResourceKey() : "ColorBrushGray4";
        ControlVisualHelpers.AnimateColorOrSetResource(
            _foregroundBorder,
            Border.BorderBrushProperty,
            resourceKey,
            IsPointerOver ? AnimationColorIn : AnimationColorOut,
            $"MyButton Color {_uuid}",
            ControlVisualHelpers.ShouldAnimate(this));
        Cursor = IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    private string GetBorderBrushResourceKey()
    {
        if (ColorType == ColorState.Gray)
            return "ColorBrushGray2";

        return ColorType switch
        {
            ColorState.Normal => IsPointerOver ? "ColorBrush3" : "ColorBrush1",
            ColorState.Highlight => IsPointerOver ? "ColorBrush3" : "ColorBrush2",
            ColorState.Red => IsPointerOver ? "ColorBrushRedLight" : "ColorBrushRedDark",
            _ => "ColorBrush1"
        };
    }

    private void ButtonMouseEnter()
    {
        if (!IsEnabled || _foregroundBorder is null)
            return;

        ControlVisualHelpers.AnimateColorOrSetResource(
            _foregroundBorder,
            Border.BackgroundProperty,
            ColorType == ColorState.Red ? "ColorBrushRedBack" : "ColorBrush7",
            AnimationColorIn,
            $"MyButton Background {_uuid}",
            ControlVisualHelpers.ShouldAnimate(this));
    }

    private void ButtonMouseLeave()
    {
        if (_foregroundBorder is null)
            return;

        ControlVisualHelpers.AnimateColorOrSetResource(
            _foregroundBorder,
            Border.BackgroundProperty,
            "ColorBrushHalfWhite",
            AnimationColorOut,
            $"MyButton Background {_uuid}",
            ControlVisualHelpers.ShouldAnimate(this));
        if (!_isPressed)
            return;

        _isPressed = false;
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(
                _foregroundBorder,
                1d - GetForegroundScale(),
                800,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
            $"MyButton Scale {_uuid}");
    }

    private double GetForegroundScale() =>
        _foregroundBorder?.RenderTransform is ScaleTransform scale ? scale.ScaleX : 1d;
}

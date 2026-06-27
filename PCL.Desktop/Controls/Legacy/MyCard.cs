// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public class MyCard : AnimatedBackgroundGrid
{
    private const double DropShadowIdleOpacity = 0.07d;
    private const double DropShadowHoverOpacity = 0.4d;

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<bool> CanSwapProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(CanSwap));

    public static readonly StyledProperty<bool> IsSwappedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsSwapped));

    public static readonly StyledProperty<bool> SwapLogoRightProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(SwapLogoRight));

    public static readonly StyledProperty<bool> HasMouseAnimationProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(HasMouseAnimation), true);

    public static readonly StyledProperty<bool> UseAnimationProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(UseAnimation), true);

    private readonly BlurBorder _mainBorder;
    private readonly Grid _mainGrid;
    private TextBlock? _mainTextBlock;
    private PathShape? _mainSwap;
    private Control? _swapControl;
    private bool _isInitialized;
    private bool _isApplyingSwap;
    private bool _isSwapMouseDown;
    private bool _isCustomMouseDown;

    public MyCard()
        : base(BlurBorder.BackgroundProperty)
    {
        MainChrome = new MyDropShadow
        {
            Margin = new Thickness(-3d, -3d, -3d, -4d),
            ShadowRadius = 3d,
            Opacity = DropShadowIdleOpacity,
            CornerRadius = new CornerRadius(8d),
            IsHitTestVisible = false
        };
        Children.Insert(0, MainChrome);

        _mainBorder = new BlurBorder
        {
            CornerRadius = new CornerRadius(8d),
            IsHitTestVisible = false
        };
        Children.Insert(1, _mainBorder);

        _mainGrid = new Grid
        {
            IsHitTestVisible = false
        };
        Children.Add(_mainGrid);

        AttachedToVisualTree += (_, _) => Init();
        PointerEntered += (_, _) => RefreshHoverVisual(true);
        PointerExited += (_, _) =>
        {
            RefreshHoverVisual(false);
            _isSwapMouseDown = false;
        };
        PointerPressed += MyCard_PointerPressed;
        PointerReleased += MyCard_PointerReleased;

        this.GetObservable(TitleProperty).Subscribe(title =>
        {
            if (_mainTextBlock is not null)
                _mainTextBlock.Text = title;
        });
        this.GetObservable(CanSwapProperty).Subscribe(_ =>
        {
            if (_isInitialized)
                EnsureSwapChrome();
        });
        this.GetObservable(IsSwappedProperty).Subscribe(value =>
        {
            if (!_isApplyingSwap)
                ApplySwapped(value);
        });
        this.GetObservable(SwapLogoRightProperty).Subscribe(_ => ApplySwapArrow());
    }

    public MyDropShadow MainChrome { get; }

    public Control? BorderChild
    {
        get => _mainBorder.Child;
        set => _mainBorder.Child = value;
    }

    public TextBlock MainTextBlock
    {
        get
        {
            Init();
            return _mainTextBlock ?? throw new InvalidOperationException("MyCard title block was not initialized.");
        }
        set
        {
            if (_mainTextBlock is not null)
                _mainGrid.Children.Remove(_mainTextBlock);

            _mainTextBlock = value;
            _mainTextBlock.Text = Title;
            _mainGrid.Children.Add(_mainTextBlock);
        }
    }

    public PathShape MainSwap
    {
        get
        {
            Init();
            return _mainSwap ?? throw new InvalidOperationException("MyCard swap indicator was not initialized.");
        }
        set
        {
            if (_mainSwap is not null)
                _mainGrid.Children.Remove(_mainSwap);

            _mainSwap = value;
            _mainGrid.Children.Add(_mainSwap);
            ApplySwapArrow();
        }
    }

    public InlineCollection Inlines => MainTextBlock.Inlines!;

    public CornerRadius CornerRadius
    {
        get => MainChrome.CornerRadius;
        set
        {
            MainChrome.CornerRadius = value;
            _mainBorder.CornerRadius = value;
        }
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool CanSwap
    {
        get => GetValue(CanSwapProperty);
        set => SetValue(CanSwapProperty, value);
    }

    public bool IsSwapped
    {
        get => GetValue(IsSwappedProperty);
        set => SetValue(IsSwappedProperty, value);
    }

    [Obsolete("请使用 IsSwapped 属性，IsSwaped 存在拼写错误")]
    public bool IsSwaped
    {
        get => IsSwapped;
        set => IsSwapped = value;
    }

    public bool SwapLogoRight
    {
        get => GetValue(SwapLogoRightProperty);
        set => SetValue(SwapLogoRightProperty, value);
    }

    public bool HasMouseAnimation
    {
        get => GetValue(HasMouseAnimationProperty);
        set => SetValue(HasMouseAnimationProperty, value);
    }

    public bool UseAnimation
    {
        get => GetValue(UseAnimationProperty);
        set => SetValue(UseAnimationProperty, value);
    }

    public Control? SwapControl
    {
        get => _swapControl;
        set
        {
            if (ReferenceEquals(_swapControl, value))
                return;

            _swapControl = value;
            if (_isInitialized)
            {
                EnsureSwapChrome();
                ApplySwapped(IsSwapped);
            }
        }
    }

    public Action<StackPanel>? InstallMethod { get; set; }

    public const int SwapedHeight = 40;

    public event PreviewSwapEventHandler? PreviewSwap;
    public event SwapEventHandler? Swap;
    public event EventHandler? Click;

#pragma warning disable CA1711
    public delegate void PreviewSwapEventHandler(object sender, CardRouteEventArgs e);
    public delegate void SwapEventHandler(object sender, CardRouteEventArgs e);
#pragma warning restore CA1711

    public void StackInstall()
    {
        if (SwapControl is not StackPanel stack)
            return;

        StackInstall(ref stack, InstallMethod);
        _swapControl = stack;
        TriggerForceResize();
    }

    public static void StackInstall(ref StackPanel stack, Action<StackPanel>? installMethod)
    {
        if (stack.Tag is null)
            return;

        installMethod?.Invoke(stack);
        stack.Children.Add(new Control { Height = 18d });
        stack.Tag = null;
    }

    public void TriggerForceResize()
    {
        Height = IsSwapped ? SwapedHeight : double.NaN;
        if (SwapControl is not null)
            SwapControl.IsVisible = !IsSwapped;
    }

    private void Init()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        BackgroundBrush = FindBrush("ColorBrushTransparentBackground", "#d2fbfbfb");
        _mainBorder.Background = BackgroundBrush;
        MainChrome.Color = FindColor("ColorObject1", "#343d4a");

        if (_mainTextBlock is null)
        {
            _mainTextBlock = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Thickness(15d, 12d, 0d, 0d),
                FontWeight = FontWeight.Bold,
                FontSize = 13d,
                Foreground = FindBrush("ColorBrush1", "#343d4a"),
                Text = Title,
                IsHitTestVisible = false
            };
            _mainGrid.Children.Add(_mainTextBlock);
        }

        EnsureSwapChrome();
        ApplySwapped(IsSwapped);
        RefreshHoverVisual(IsPointerOver);
    }

    private void EnsureSwapChrome()
    {
        if (!CanSwap && SwapControl is null)
            return;

        if (SwapControl is null && Children.Count > 3 && Children[3] is Control control)
            SwapControl = control;

        if (_mainSwap is not null)
            return;

        _mainSwap = new PathShape
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Stretch = Stretch.Uniform,
            Height = 6d,
            Width = 10d,
            Margin = new Thickness(0d, 17d, 16d, 0d),
            Data = Geometry.Parse("M2,4 l-2,2 10,10 10,-10 -2,-2 -8,8 -8,-8 z"),
            RenderTransform = new RotateTransform(180d),
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            Fill = FindBrush("ColorBrush1", "#343d4a"),
            IsHitTestVisible = false
        };
        _mainGrid.Children.Add(_mainSwap);
        ApplySwapArrow();
    }

    private void ApplySwapped(bool value)
    {
        if (SwapControl is null)
            return;

        if (!value && SwapControl is StackPanel stack)
        {
            StackInstall(ref stack, InstallMethod);
            _swapControl = stack;
        }

        SwapControl.IsVisible = !value;
        Height = value ? SwapedHeight : double.NaN;
        ApplySwapArrow();
    }

    private void ApplySwapArrow()
    {
        if (_mainSwap?.RenderTransform is RotateTransform rotate)
            rotate.Angle = IsSwapped ? (SwapLogoRight ? 270d : 0d) : 180d;
    }

    private void RefreshHoverVisual(bool isHover)
    {
        if (!HasMouseAnimation)
            return;

        var textBrush = FindBrush(isHover ? "ColorBrush2" : "ColorBrush1", isHover ? "#0b5bcb" : "#343d4a");
        if (_mainTextBlock is not null)
            _mainTextBlock.Foreground = textBrush;
        if (_mainSwap is not null)
            _mainSwap.Fill = textBrush;

        MainChrome.Color = FindColor(isHover ? "ColorObject4" : "ColorObject1", isHover ? "#4890f5" : "#343d4a");
        MainChrome.Opacity = isHover ? DropShadowHoverOpacity : DropShadowIdleOpacity;
    }

    private void MyCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        double y = e.GetPosition(this).Y;
        if (!IsSwapped && (y > SwapedHeight - 6d || (Math.Abs(y) < 0.001d && !IsPointerOver)))
            return;

        _isCustomMouseDown = true;
        if (SwapControl is null)
            return;

        if (!IsSwapped && y > SwapedHeight - 6d)
            return;

        _isSwapMouseDown = true;
        e.Handled = true;
    }

    private void MyCard_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isCustomMouseDown)
            return;

        _isCustomMouseDown = false;
        Click?.Invoke(this, EventArgs.Empty);

        if (!_isSwapMouseDown)
            return;

        _isSwapMouseDown = false;
        double y = e.GetPosition(this).Y;
        if (!IsSwapped && (SwapControl is null || y > SwapedHeight - 6d || (Math.Abs(y) < 0.001d && !IsPointerOver)))
            return;

        CardRouteEventArgs routeArgs = new(raiseByMouse: true);
        PreviewSwap?.Invoke(this, routeArgs);
        if (routeArgs.Handled)
            return;

        _isApplyingSwap = true;
        try
        {
            IsSwapped = !IsSwapped;
        }
        finally
        {
            _isApplyingSwap = false;
        }

        ApplySwapped(IsSwapped);
        Swap?.Invoke(this, routeArgs);
        e.Handled = true;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        if (this.TryGetResource(key, null, out object? resource) && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallback));
    }

    private Color FindColor(string key, string fallback)
    {
        if (this.TryGetResource(key, null, out object? resource))
        {
            if (resource is Color color)
                return color;
            if (resource is SolidColorBrush brush)
                return brush.Color;
        }

        return Color.Parse(fallback);
    }
}

#pragma warning disable CA1708
public sealed class CardRouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool Handled { get; set; }

    public bool handled
    {
        get => Handled;
        set => Handled = value;
    }

    public bool RaiseByMouse { get; } = raiseByMouse;

    public bool raiseByMouse => RaiseByMouse;
}
#pragma warning restore CA1708

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
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private TextBlock? _mainTextBlock;
    private PathShape? _mainSwap;
    private Control? _swapControl;
    private bool _isInitialized;
    private bool _isApplyingSwap;
    private bool _isSwapMouseDown;
    private bool _isCustomMouseDown;
    private bool _isHeightAnimating;
    private double _actualUsedHeight;

    public MyCard()
        : base(BlurBorder.BackgroundProperty)
    {
        Background = Brushes.Transparent;
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
        SizeChanged += MyCard_SizeChanged;

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

    protected override Control AnimatableElement => _mainBorder;

    protected override IBrush? AnimatableBrush
    {
        get => _mainBorder.Background;
        set => _mainBorder.Background = value;
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
    public delegate void PreviewSwapEventHandler(object sender, RouteEventArgs e);
    public delegate void SwapEventHandler(object sender, RouteEventArgs e);
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
        ModAnimation.AniStop($"MyCard Height {_uuid}");
        _isHeightAnimating = false;
        if (SwapControl is not null)
            SwapControl.IsVisible = !IsSwapped;
    }

    private void Init()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        SetBackgroundBrushDirect(FindBrush("ColorBrushTransparentBackground", "#d2fbfbfb"));
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
        ApplySwapped(IsSwapped, animate: false);
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
        ApplySwapArrow(animate: false);
    }

    private void ApplySwapped(bool value) =>
        ApplySwapped(value, ControlVisualHelpers.ShouldAnimate(this) && UseAnimation && Bounds.Height > 0d);

    private void ApplySwapped(bool value, bool animate)
    {
        if (SwapControl is null)
            return;

        if (!value && SwapControl is StackPanel stack)
        {
            StackInstall(ref stack, InstallMethod);
            _swapControl = stack;
        }

        SwapControl.IsVisible = animate || !value;
        Height = value ? SwapedHeight : double.NaN;
        ModAnimation.AniStop($"MyCard Height {_uuid}");
        _isHeightAnimating = false;
        if (!animate)
            SwapControl.IsVisible = !value;
        ApplySwapArrow(animate);
    }

    private void ApplySwapArrow(bool animate = false)
    {
        if (_mainSwap?.RenderTransform is not RotateTransform rotate)
            return;

        double targetAngle = IsSwapped ? (SwapLogoRight ? 270d : 0d) : 180d;
        if (animate && ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                ModAnimation.AaRotateTransform(
                    _mainSwap,
                    targetAngle - rotate.Angle,
                    250,
                    ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                $"MyCard Swap {_uuid}",
                true);
            return;
        }

        ModAnimation.AniStop($"MyCard Swap {_uuid}");
        rotate.Angle = targetAngle;
    }

    private void MyCard_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!UseAnimation)
            return;

        double deltaHeight = (IsSwapped ? SwapedHeight : e.NewSize.Height) - e.PreviousSize.Height;
        if (e.PreviousSize.Height == 0d ||
            _isHeightAnimating ||
            Math.Abs(deltaHeight) < 1d ||
            e.NewSize.Height == 0d)
        {
            return;
        }

        StartHeightAnimation(deltaHeight, e.PreviousSize.Height);
    }

    private void StartHeightAnimation(double delta, double previousHeight)
    {
        if (_isHeightAnimating)
            return;

        List<ModAnimation.AniData> animations = [];
        double absDelta = Math.Abs(delta);
        if (absDelta <= 800d)
        {
            animations.Add(ModAnimation.AaHeight(
                this,
                delta,
                150,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)));
        }
        else
        {
            int easeLength;
            int easeTime;
            int initSpeed;
            if (delta < 0d && absDelta > 5000d * 0.1d)
            {
                easeLength = 200;
                easeTime = 150;
                initSpeed = (int)Math.Round((absDelta - easeLength) / 0.1d);
            }
            else if (delta > 0d && absDelta > 5000d * 0.6d)
            {
                initSpeed = 5000;
                easeLength = (int)Math.Round(absDelta - initSpeed * 0.3d);
                easeTime = 400;
            }
            else
            {
                easeLength = 150;
                easeTime = 200;
                initSpeed = 4000;
            }

            animations.Add(ModAnimation.AaHeight(
                this,
                (absDelta - easeLength) * Math.Sign(delta),
                (int)Math.Round((absDelta - easeLength) / initSpeed * 1000d)));
            animations.Add(ModAnimation.AaHeight(
                this,
                easeLength * Math.Sign(delta),
                easeTime,
                ease: new ModAnimation.AniEaseOutFluentWithInitial(initSpeed, easeTime / 1000d, easeLength),
                after: true));
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            Height = _actualUsedHeight;
            if (IsSwapped && SwapControl is not null)
                SwapControl.IsVisible = false;
            _isHeightAnimating = false;
        }, after: true));

        _actualUsedHeight = IsSwapped ? SwapedHeight : Height;
        Height = previousHeight;
        _isHeightAnimating = true;
        ModAnimation.AniStart(animations, $"MyCard Height {_uuid}");
    }

    private void RefreshHoverVisual(bool isHover)
    {
        if (!HasMouseAnimation)
            return;

        string foregroundKey = isHover ? "ColorBrush2" : "ColorBrush1";
        string shadowKey = isHover ? "ColorObject4" : "ColorObject1";
        int duration = 90;
        if (!ControlVisualHelpers.ShouldAnimate(this) || IsBackgroundAnimating)
        {
            IBrush textBrush = FindBrush(foregroundKey, isHover ? "#0b5bcb" : "#343d4a");
            if (_mainTextBlock is not null)
                _mainTextBlock.Foreground = textBrush;
            if (_mainSwap is not null)
                _mainSwap.Fill = textBrush;
            MainChrome.Color = FindColor(shadowKey, isHover ? "#4890f5" : "#343d4a");
            MainChrome.Opacity = isHover ? DropShadowHoverOpacity : DropShadowIdleOpacity;
            return;
        }

        List<ModAnimation.AniData> animations = [];
        if (_mainTextBlock is not null)
            animations.Add(ModAnimation.AaColor(_mainTextBlock, TextBlock.ForegroundProperty, foregroundKey, duration));
        if (_mainSwap is not null)
            animations.Add(ModAnimation.AaColor(_mainSwap, Shape.FillProperty, foregroundKey, duration));
        animations.Add(ModAnimation.AaColor(MainChrome, MyDropShadow.ColorProperty, shadowKey, duration));
        animations.Add(ModAnimation.AaOpacity(
            MainChrome,
            (isHover ? DropShadowHoverOpacity : DropShadowIdleOpacity) - MainChrome.Opacity,
            duration));

        ModAnimation.AniStart(animations, $"MyCard Mouse {GetHashCode()}");
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

        RouteEventArgs routeArgs = new(raiseByMouse: true);
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
        return LegacyResourceResolver.Brush(this, key, fallback);
    }

    private Color FindColor(string key, string fallback)
    {
        return LegacyResourceResolver.Color(this, key, fallback);
    }
}

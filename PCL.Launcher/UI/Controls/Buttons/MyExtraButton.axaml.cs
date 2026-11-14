using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 侧边栏圆形附加按钮：显示时以缩放与高度动画展开，按下/释放带弹性回弹，
///     悬停与禁用切换底色；<see cref="Progress" /> 用环形区域表示下载进度，
///     <see cref="Ribble" /> 发出一圈扩散提示。
/// </summary>
public partial class MyExtraButton : Grid
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs? e);

    public delegate void RightClickEventHandler(object? sender, PointerReleasedEventArgs e);

    public delegate bool ShowCheckDelegate();

    // 悬停/离开的配色渐变时长（毫秒）。
    private const int AnimationColorIn = 120;
    private const int AnimationColorOut = 150;

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyExtraButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyExtraButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<ICommand?> RightClickCommandProperty =
        AvaloniaProperty.Register<MyExtraButton, ICommand?>(nameof(RightClickCommand));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<MyExtraButton, double>(nameof(Progress));

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyExtraButton, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyExtraButton, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyExtraButton, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<bool> ShowProperty =
        AvaloniaProperty.Register<MyExtraButton, bool>(nameof(Show));

    private static int _uuidSeed;

    private static int _ribbleSeed;

    /// <summary>动画键使用的唯一编号。</summary>
    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private readonly RectangleGeometry _rectProgress = new(new Rect(0d, 5d, 40d, 35d));
    private bool _isCommandRunning;
    private bool _isLeftMouseHeld;
    private bool _isRightClickCommandRunning;
    private bool _isRightMouseHeld;

    public ShowCheckDelegate? showCheck;

    public MyExtraButton()
    {
        InitializeComponent();

        Tooltip.Enable();

        PanProgress.Clip = _rectProgress;
        PanClick.PointerPressed += _OnPointerPressed;
        PanClick.PointerReleased += _OnPointerReleased;
        PanClick.PointerEntered += (_, _) => RefreshColor();
        PanClick.PointerExited += _OnPointerExited;
        KeyboardActivation.Attach(this, PerformClick);
        Loaded += (_, _) => RefreshColor();
        ActualThemeVariantChanged += (_, _) => RefreshColor();
        Unloaded += (_, _) => _StopAnimations();
        ApplyLogoScale();
    }

    /// <summary>是否允许右键点击。</summary>
    public bool CanRightClick { get; set; }

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

    public ICommand? RightClickCommand
    {
        get => GetValue(RightClickCommandProperty);
        set => SetValue(RightClickCommandProperty, value);
    }

    /// <summary>下载进度（0~1），大于 0 时显示环形进度底色。</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>旧版 Path 图标数据（SVG path 语法）。</summary>
    public string Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value ?? string.Empty);
    }

    /// <summary>SVG 图标名称（<c>pack/name</c>），设置后优先于 Logo。</summary>
    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value ?? string.Empty);
    }

    /// <summary>未使用 SVG 时的图标缩放（SVG 图标按 1.0 绘制）。</summary>
    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    /// <summary>是否显示；通过缩放与高度动画切换。</summary>
    public bool Show
    {
        get => GetValue(ShowProperty);
        set => SetValue(ShowProperty, value);
    }

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1d : LogoScale;

    private string ScaleKey => ButtonAnimation.Name("MyExtraButton Scale", Uuid);

    private string MainScaleKey => ButtonAnimation.Name("MyExtraButton MainScale", Uuid);

    private string ColorKey => ButtonAnimation.Name("MyExtraButton Color", Uuid);

    public event ClickEventHandler? Click;

    public event RightClickEventHandler? RightClick;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ProgressProperty)
            _ApplyProgress(change.GetNewValue<double>());
        else if (change.Property == LogoProperty)
            _ApplyLogo(change.NewValue as string);
        else if (change.Property == SvgIconProperty)
            _ApplySvgIcon(change.NewValue as string);
        else if (change.Property == LogoScaleProperty)
            ApplyLogoScale();
        else if (change.Property == ShowProperty)
            _ApplyShow(change.GetNewValue<bool>());
        else if (change.Property == IsEnabledProperty)
            RefreshColor();
    }

    /// <summary>按 <see cref="showCheck" /> 刷新显示状态。</summary>
    public void ShowRefresh()
    {
        if (showCheck is not null)
            Show = showCheck();
    }

    /// <summary>按当前悬停/禁用状态刷新底色。</summary>
    public void RefreshColor()
    {
        if (!IsLoaded)
            return;

        var key = !IsEnabled
            ? "ColorBrushGray4"
            : IsPointerOver
                ? "ColorBrush4"
                : "ColorBrush3";

        ButtonAnimation.ApplyColor(
            PanColor,
            Border.BackgroundProperty,
            key,
            null,
            !IsEnabled || IsPointerOver ? AnimationColorIn : AnimationColorOut,
            true,
            ColorKey);
    }

    /// <summary>
    ///     发出一圈波浪效果提示：插入圆形 Border，放大并淡出后移除。
    ///     动画关闭时不产生视觉效果。
    /// </summary>
    public void Ribble()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Ribble);
            return;
        }

        if (!ButtonAnimation.CanAnimate(this))
            return;

        var color = ButtonAnimation.ThemeColor(this, "ColorBrush5");
        if (color is null)
            return;

        var shape = new Border
        {
            CornerRadius = new CornerRadius(1000d),
            BorderThickness = new Thickness(0.001d),
            Opacity = 0.5d,
            IsHitTestVisible = false,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new ScaleTransform(1d, 1d),
            Background = new SolidColorBrush((Color)color)
        };

        PanScale.Children.Insert(0, shape);
        AnimationService.Run(
            new List<IAnimation>
            {
                new ScaleTransformAnimation(
                    shape,
                    13d,
                    1000,
                    ease: new FluentEaseInOut(
                        EasePower.Strong,
                        0.3d)),
                new OpacityAnimation(shape, -0.5d, 1000),
                new ActionAnimation(() => PanScale.Children.Remove(shape), true)
            },
            $"MyExtraButton Ribble {Interlocked.Increment(ref _ribbleSeed)}");
    }

    private void _ApplyProgress(double value)
    {
        if (value < 0.0001d)
        {
            PanProgress.IsVisible = false;
            return;
        }

        PanProgress.IsVisible = true;
        _rectProgress.Rect = new Rect(0d, 40d * (1d - value), 40d, 40d * value);
    }

    private void _ApplyLogo(string? value)
    {
        try
        {
            Path.Data = string.IsNullOrWhiteSpace(value) ? null : Geometry.Parse(value);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MyExtraButton", $"解析 Logo 路径失败：{value}");
            Path.Data = null;
        }

        _ApplyIconVisibility();
    }

    private void _ApplySvgIcon(string? value)
    {
        ShapeSvgIcon.Icon = value ?? string.Empty;
        _ApplyIconVisibility();
        ApplyLogoScale();
    }

    private void _ApplyIconVisibility()
    {
        Path.IsVisible = !IsUsingSvgIcon;
        ShapeSvgIcon.IsVisible = IsUsingSvgIcon;
    }

    private void ApplyLogoScale()
    {
        IconHost.RenderTransform = new ScaleTransform(EffectiveLogoScale, EffectiveLogoScale);
    }

    private double CurrentHeight()
    {
        return double.IsNaN(Height) ? Bounds.Height : Height;
    }

    /// <summary>
    ///     显示/隐藏过渡：显示时先缩放再回弹并展开高度，隐藏时缩到 0、高度收拢后折叠。
    /// </summary>
    private void _ApplyShow(bool value)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _ApplyShow(value));
            return;
        }

        IsHitTestVisible = value;

        if (value)
        {
            IsVisible = true;

            if (!ButtonAnimation.CanAnimate(this))
            {
                ButtonAnimation.SetScale(this, 1d);
                Height = 50d;
                return;
            }

            AnimationService.Run(
                new List<IAnimation>
                {
                    new ScaleTransformAnimation(
                        this,
                        0.3d - ButtonAnimation.CurrentScale(this),
                        500,
                        60,
                        new FluentEaseOut(EasePower.Weak)),
                    new ScaleTransformAnimation(
                        this,
                        0.7d,
                        500,
                        60,
                        new BackEaseWithPowerOut(EasePower.Weak)),
                    new HeightAnimation(
                        this,
                        50d - CurrentHeight(),
                        200,
                        ease: new FluentEaseOut(EasePower.Weak))
                },
                MainScaleKey);
            return;
        }

        if (!ButtonAnimation.CanAnimate(this))
        {
            ButtonAnimation.SetScale(this, 0d);
            Height = 0d;
            IsVisible = false;
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new ScaleTransformAnimation(
                    this,
                    -ButtonAnimation.CurrentScale(this),
                    100,
                    ease: new FluentEaseIn(EasePower.Weak)),
                new HeightAnimation(
                    this,
                    -CurrentHeight(),
                    400,
                    100,
                    new FluentEaseOut()),
                new ActionAnimation(
                    () =>
                    {
                        Height = 0d;
                        IsVisible = false;
                    },
                    true)
            },
            MainScaleKey);
    }

    /// <summary>按下时的缩放回弹。</summary>
    private void _StartScaleAnimation(
        double targetScale,
        double reboundScale,
        int reboundDuration = 60)
    {
        if (!ButtonAnimation.CanAnimate(this))
        {
            ButtonAnimation.SetScale(PanScale, targetScale + reboundScale);
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new ScaleTransformAnimation(
                    PanScale,
                    targetScale - ButtonAnimation.CurrentScale(PanScale),
                    800,
                    ease: new FluentEaseOut(EasePower.Strong)),
                new ScaleTransformAnimation(
                    PanScale,
                    reboundScale,
                    reboundDuration,
                    ease: new FluentEaseOut())
            },
            ScaleKey);
    }

    /// <summary>释放后回弹到原尺寸。</summary>
    private void _RefreshScaleAfterRelease()
    {
        if (!ButtonAnimation.CanAnimate(this))
        {
            ButtonAnimation.SetScale(PanScale, 1d);
            return;
        }

        AnimationService.Run(
            new ScaleTransformAnimation(
                PanScale,
                1d - ButtonAnimation.CurrentScale(PanScale),
                300,
                ease: new BackEaseWithPowerOut()),
            ScaleKey);
    }

    private void _ResetHeldState()
    {
        _isLeftMouseHeld = false;
        _isRightMouseHeld = false;
        if (ButtonAnimation.CanAnimate(this))
            AnimationService.Run(
                new ScaleTransformAnimation(
                    PanScale,
                    1d - ButtonAnimation.CurrentScale(PanScale),
                    500,
                    ease: new FluentEaseOut()),
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanScale, 1d);

        RefreshColor();
    }

    private void _StopAnimations()
    {
        AnimationService.CancelAnimationByName(ScaleKey);
        AnimationService.CancelAnimationByName(MainScaleKey);
        AnimationService.CancelAnimationByName(ColorKey);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            if (!CanRightClick || _isLeftMouseHeld || _isRightMouseHeld)
                return;
            _StartScaleAnimation(0.85d, -0.05d);
            _isRightMouseHeld = true;
            Focus();
            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        if (!_isLeftMouseHeld && !_isRightMouseHeld)
            _StartScaleAnimation(0.85d, -0.05d);
        _isLeftMouseHeld = true;
        Focus();
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var tipText = ToolTip.GetTip(this) as string;

        if (e.InitialPressMouseButton == MouseButton.Left && _isLeftMouseHeld)
        {
            _isLeftMouseHeld = false;
            PerformClick();
            e.Handled = true;
            if (!_isRightMouseHeld)
                _RefreshScaleAfterRelease();
            return;
        }

        if (e.InitialPressMouseButton != MouseButton.Right || !_isRightMouseHeld)
            return;

        if (!CanRightClick)
            return;

        LogWrapper.Debug(
            $"[Control] 右键按下附加按钮{(string.IsNullOrEmpty(tipText) ? "" : $"：{tipText}")}");
        _isRightMouseHeld = false;
        RightClick?.Invoke(this, e);
        e.Handled = true;
        if (!_isLeftMouseHeld)
            _RefreshScaleAfterRelease();
        RefreshColor();
        ExecuteRightClickCommand();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _ResetHeldState();
    }

    /// <summary>执行点击语义；鼠标释放、键盘 Enter/Space 与自动化调用共用。</summary>
    public void PerformClick()
    {
        if (!IsEnabled) return;
        var tipText = ToolTip.GetTip(this) as string;
        LogWrapper.Debug(
            $"[Control] 按下附加按钮{(string.IsNullOrEmpty(tipText) ? "" : $"：{tipText}")}");
        Click?.Invoke(this, null!);
        RefreshColor();
        CustomEventService.Raise(this);
        ExecuteCommand();
    }

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }

    private void ExecuteRightClickCommand()
    {
        var command = RightClickCommand;
        if (_isRightClickCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isRightClickCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isRightClickCommandRunning = false;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PclButtonAutomationPeer(
            this,
            PerformClick,
            () => ToolTip.GetTip(this) as string);
    }
}
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 圆形图标按钮：内层 <c>PanBack</c> 承载图标与缩放，图标可以是旧版 Path
///     数据（<see cref="Logo" />）或 SVG 图标（<see cref="SvgIcon" />）。
///     图标填充与 White 主题的悬停底色由 <c>PCL.Animations</c> 插值，其余主题的
///     底色保持透明；Custom 主题按 <see cref="Foreground" /> 计算透明度。
///     从父容器移除时由 <see cref="AniDispose" /> 先缩放收起再移除。
/// </summary>
public partial class MyIconButton : Border
{
    public delegate void ClickEventHandler(object? sender, EventArgs e);

    public enum Themes
    {
        Color,
        White,
        Black,
        Red,
        Custom
    }

    private const int AnimationColorIn = 120;
    private const int AnimationColorOut = 150;

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyIconButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyIconButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyIconButton, IBrush?>(nameof(Foreground));

    private static int _uuidSeed;

    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private bool _isCommandRunning;
    private bool _isMouseDown;
    private double _logoScale = 1d;
    private Themes _theme = Themes.Color;

    public MyIconButton()
    {
        InitializeComponent();

        _ApplyThemeClasses();
        RefreshAnim(false);

        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerEntered += (_, _) => RefreshAnim();
        PointerExited += _OnPointerExited;
        KeyboardActivation.Attach(this, PerformClick);

        Loaded += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(this)))
            {
                var tooltip = ToolTip.GetTip(this) as string;
                AutomationProperties.SetName(
                    this,
                    !string.IsNullOrWhiteSpace(tooltip)
                        ? tooltip
                        : !string.IsNullOrWhiteSpace(SvgIcon)
                            ? SvgIcon
                            : Name ?? string.Empty);
            }

            RefreshAnim(false);
        };
        ActualThemeVariantChanged += (_, _) => RefreshAnim(false);
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(ScaleKey);
            PCL.Animations.AniStop(FillKey);
            PCL.Animations.AniStop(BackgroundKey);
            PCL.Animations.AniStop(DisposeKey);
        };
    }

    /// <summary>旧版 Path 图标数据（SVG path 语法）。</summary>
    public string Logo
    {
        get => Path.Data?.ToString() ?? string.Empty;
        set
        {
            try
            {
                Path.Data = string.IsNullOrWhiteSpace(value) ? null : Geometry.Parse(value);
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "MyIconButton", "解析 Logo 路径失败：" + value);
                Path.Data = null;
            }

            _ApplyIconVisibility();
        }
    }

    /// <summary>SVG 图标名称（<c>pack/name</c>），设置后优先于 Logo。</summary>
    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            ShapeSvgIcon.Icon = field;
            _ApplyIconVisibility();
            ApplyLogoScale();
            RefreshAnim();
        }
    } = string.Empty;

    /// <summary>未使用 SVG 时的图标缩放（SVG 图标按 1.0 绘制）。</summary>
    public double LogoScale
    {
        get => _logoScale;
        set
        {
            _logoScale = value;
            ApplyLogoScale();
        }
    }

    /// <summary>配色主题。</summary>
    public new Themes Theme
    {
        get => _theme;
        set
        {
            if (_theme == value)
                return;
            _theme = value;
            _ApplyThemeClasses();
            RefreshAnim();
        }
    }

    /// <summary>Custom 主题下的图标基色。</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
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

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1d : LogoScale;

    private string ScaleKey => ButtonAnimation.Name("MyIconButton Scale", Uuid);

    private string FillKey => ButtonAnimation.Name("MyIconButton Fill", Uuid);

    private string BackgroundKey => ButtonAnimation.Name("MyIconButton Background", Uuid);

    private string DisposeKey => ButtonAnimation.Name("MyIconButton Dispose", Uuid);

    public event ClickEventHandler? Click;

    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        if (double.IsNaN(Width)
            && !double.IsNaN(Height)
            && Height > 0d
            && !double.IsInfinity(Height))
            return new Size(Height, Height);
        return measured;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ForegroundProperty)
            RefreshAnim();
    }

    private void _ApplyIconVisibility()
    {
        Path.IsVisible = !IsUsingSvgIcon;
        ShapeSvgIcon.IsVisible = IsUsingSvgIcon;
    }

    private void _ApplyThemeClasses()
    {
        Classes.Set("theme-color", _theme == Themes.Color);
        Classes.Set("theme-white", _theme == Themes.White);
        Classes.Set("theme-black", _theme == Themes.Black);
        Classes.Set("theme-red", _theme == Themes.Red);
        Classes.Set("theme-custom", _theme == Themes.Custom);
    }

    private void ApplyLogoScale()
    {
        IconHost.RenderTransform = new ScaleTransform(EffectiveLogoScale, EffectiveLogoScale);
    }

    /// <summary>
    ///     刷新当前主题下的图标颜色与 White 主题的悬停底色。
    ///     非 Custom 主题在主题色之间做 120/150ms 的颜色过渡，Custom 主题按 Foreground 计算透明度；
    ///     <paramref name="animate" /> 为 false（加载、主题切换）时直接赋终值。
    /// </summary>
    public void RefreshAnim(bool animate = true)
    {
        try
        {
            var time = IsPointerOver ? AnimationColorIn : AnimationColorOut;
            RefreshBackground(animate, time);

            if (_theme == Themes.Custom)
            {
                var baseColor = Foreground is ISolidColorBrush brush ? brush.Color : Colors.Gray;
                var alpha = IsPointerOver ? 255d : 160d;
                ButtonAnimation.ApplyColor(
                    Path,
                    Shape.FillProperty,
                    string.Empty,
                    new MyColor(alpha, baseColor),
                    time,
                    animate,
                    FillKey);
                return;
            }

            var (key, literal) = _theme switch
            {
                Themes.White => (string.Empty, new MyColor(234d, 242d, 254d)),
                Themes.Black => (string.Empty, ThemeService.IsDarkMode
                    ? new MyColor(IsPointerOver ? 230d : 160d, 255d, 255d, 255d)
                    : new MyColor(IsPointerOver ? 230d : 160d, 0d, 0d, 0d)),
                Themes.Red => IsPointerOver
                    ? ("ColorBrushRedLight", null)
                    : ("ColorBrushRedLight", AlphaThemeColor("ColorBrushRedLight", 160d)),
                _ => IsPointerOver
                    ? ("ColorBrush2", null)
                    : ("ColorBrush4", null)
            };

            ButtonAnimation.ApplyColor(
                Path,
                Shape.FillProperty,
                key,
                literal,
                time,
                animate,
                FillKey);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MyIconButton", "刷新图标按钮颜色出错");
        }
    }

    private MyColor? AlphaThemeColor(string key, double alpha)
    {
        var color = ButtonAnimation.ThemeColor(this, key);
        return color is null ? null : new MyColor(alpha, (Color)color);
    }

    /// <summary>
    ///     White 主题在悬停时给内层 <c>PanBack</c> 铺半透明白底，离开时淡出到透明；
    ///     其余主题始终保持透明底色。
    /// </summary>
    private void RefreshBackground(bool animate, int time)
    {
        if (_theme == Themes.White && IsPointerOver)
        {
            ButtonAnimation.ApplyColor(
                PanBack,
                BackgroundProperty,
                string.Empty,
                new MyColor(50d, 255d, 255d, 255d),
                time,
                animate,
                BackgroundKey);
            return;
        }

        ButtonAnimation.ApplyColor(
            PanBack,
            BackgroundProperty,
            string.Empty,
            new MyColor(0d, 255d, 255d, 255d),
            time,
            animate,
            BackgroundKey);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
        Focus();
        // 按下时内层缩小到 0.8。
        if (ButtonAnimation.CanAnimate(this))
            PCL.Animations.AniStart(
                PCL.Animations.AaScaleTransform(
                    PanBack,
                    0.8d - ButtonAnimation.CurrentScale(PanBack),
                    ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong)),
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanBack, 0.8d);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        // 释放时先弹到 1.05，再回落到 1。
        if (ButtonAnimation.CanAnimate(this))
            PCL.Animations.AniStart(
                new List<PCL.Animations.AniData>
                {
                    PCL.Animations.AaScaleTransform(
                        PanBack,
                        1.05d - ButtonAnimation.CurrentScale(PanBack),
                        250,
                        ease: new PCL.Animations.AniEaseOutBack(PCL.Animations.AniEasePower.Weak)),
                    PCL.Animations.AaScaleTransform(
                        PanBack,
                        -0.05d,
                        250,
                        ease: new PCL.Animations.AniEaseOutFluent(
                            PCL.Animations.AniEasePower.Strong))
                },
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanBack, 1d);

        PerformClick();
        e.Handled = true;
    }

    /// <summary>执行点击语义；鼠标释放、键盘 Enter/Space 与自动化调用共用。</summary>
    public void PerformClick()
    {
        if (!IsEnabled) return;
        LogWrapper.Debug("[Control] 按下图标按钮" + (string.IsNullOrEmpty(Name) ? "" : "：" + Name));
        Click?.Invoke(this, EventArgs.Empty);
        CustomEventService.Raise(this);
        RefreshAnim();
        ExecuteCommand();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        // 离开时内层回到原尺寸。
        if (ButtonAnimation.CanAnimate(this))
            PCL.Animations.AniStart(
                PCL.Animations.AaScaleTransform(
                    PanBack,
                    1d - ButtonAnimation.CurrentScale(PanBack),
                    250,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanBack, 1d);

        RefreshAnim();
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

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PclButtonAutomationPeer(
            this,
            PerformClick,
            () => ToolTip.GetTip(this) as string ?? SvgIcon);
    }

    #region 收起与移除

    private bool _isDisposing;

    /// <summary>
    ///     收起按钮并从父容器移除：整体缩放增量 −1.5（200 ms，<c>AniEaseInFluent</c>）后移除或隐藏，不做淡出。
    ///     已不可交互、动画关闭或控件未加载时立即收尾。
    /// </summary>
    /// <param name="removeFromChildren">为 <see langword="true" /> 时从父容器移除，否则仅隐藏。</param>
    /// <param name="callBack">收尾后在 UI 线程执行的回调，参数为本按钮。</param>
    public void AniDispose(bool removeFromChildren = true, Action<MyIconButton>? callBack = null)
    {
        if (_isDisposing || !IsHitTestVisible)
            return;
        _isDisposing = true;
        IsHitTestVisible = false;

        if (PCL.Animations.AniSpeed <= 0d
            || !IsLoaded
            || TopLevel.GetTopLevel(this) is null
            || PCL.Animations.AniExitsAnyControl())
        {
            Finish();
            return;
        }

        if (RenderTransform is not ScaleTransform transform)
        {
            RenderTransformOrigin = RelativePoint.Center;
            transform = new ScaleTransform(1d, 1d);
            RenderTransform = transform;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScaleTransform(
                    this,
                    -1.5d - transform.ScaleX,
                    200,
                    ease: new PCL.Animations.AniEaseInFluent()),
                PCL.Animations.AaCode(Finish, after: true)
            },
            DisposeKey);
        return;

        void Finish()
        {
            if (removeFromChildren)
            {
                if (!PCL.Animations.TryRemoveFromParent(this))
                    IsVisible = false;
            }
            else
            {
                IsVisible = false;
            }

            callBack?.Invoke(this);
        }
    }

    #endregion
}
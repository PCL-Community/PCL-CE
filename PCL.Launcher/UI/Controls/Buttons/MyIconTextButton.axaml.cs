using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Core.Logging;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 带图标的胶囊按钮：左侧图标（<see cref="Logo" /> 或 <see cref="SvgIcon" />）
///     与右侧文本共用一套前景色，悬停/按下/禁用时与背景一起做颜色插值。
///     文本支持 <see cref="Inlines" /> 富文本；<see cref="ColorType" /> 决定常态前景色。
/// </summary>
public partial class MyIconTextButton : Border
{
    public delegate void ChangeEventHandler(object? sender, bool raiseByMouse);

    public delegate void CheckEventHandler(object? sender, bool raiseByMouse);

    public delegate void ClickEventHandler(object? sender, RouteEventArgs e);

    public enum ColorState
    {
        Black,
        Highlight
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyIconTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ColorState> ColorTypeProperty =
        AvaloniaProperty.Register<MyIconTextButton, ColorState>(nameof(ColorType));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyIconTextButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyIconTextButton, object?>(nameof(CommandParameter));

    private static int _uuidSeed;

    /// <summary>动画键使用的唯一编号。</summary>
    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private bool _hasLegacyLogo;
    private bool _isCommandRunning;
    private bool _isMouseDown;
    private double _logoScale = 1d;

    public MyIconTextButton()
    {
        InitializeComponent();

        LabText.Text = Text;
        _ApplyColorClasses();
        RefreshLogoHostVisibility();

        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerEntered += (_, _) => RefreshColor(true);
        PointerExited += _OnPointerExited;
        KeyboardActivation.Attach(this, modifiers => PerformClick(modifiers));

        Loaded += (_, _) => RefreshColor(false);
        ActualThemeVariantChanged += (_, _) => RefreshColor(false);
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(BackgroundKey);
            PCL.Animations.AniStop(ForegroundKey);
            PCL.Animations.AniStop(IconKey);
        };
    }

    /// <summary>最近一次点击携带的修饰键；键盘和自动化调用为 <see cref="KeyModifiers.None" />。</summary>
    public KeyModifiers LastClickKeyModifiers { get; private set; }

    /// <summary>旧版 Path 图标数据（SVG path 语法）。</summary>
    public string Logo
    {
        get => ShapeLogo.Data?.ToString() ?? string.Empty;
        set
        {
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
            try
            {
                ShapeLogo.Data = _hasLegacyLogo ? Geometry.Parse(value) : null;
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "MyIconTextButton", "解析 Logo 路径失败：" + value);
                ShapeLogo.Data = null;
                _hasLegacyLogo = false;
            }

            ShapeLogo.IsVisible = !IsUsingSvgIcon;
            ShapeSvgIcon.IsVisible = IsUsingSvgIcon;
            RefreshLogoHostVisibility();
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
            ShapeLogo.IsVisible = !IsUsingSvgIcon;
            ShapeSvgIcon.IsVisible = IsUsingSvgIcon;
            ApplyLogoScale();
            RefreshLogoHostVisibility();
            RefreshColor(false);
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

    public InlineCollection Inlines => LabText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>颜色类别：Black（默认文字色）/ Highlight（强调色）。</summary>
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

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1d : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    private string BackgroundKey => ButtonAnimation.Name("MyIconTextButton Background", Uuid);

    private string ForegroundKey => ButtonAnimation.Name("MyIconTextButton Foreground", Uuid);

    private string IconKey => ButtonAnimation.Name("MyIconTextButton Icon", Uuid);

    public event CheckEventHandler? Check;

    public event ChangeEventHandler? Change;

    public event ClickEventHandler? Click;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            LabText.Text = change.NewValue as string ?? string.Empty;
        }
        else if (change.Property == ColorTypeProperty)
        {
            _ApplyColorClasses();
            RefreshColor(false);
        }
        else if (change.Property == IsEnabledProperty)
        {
            RefreshColor(false);
        }
    }

    /// <summary>
    ///     按悬停/按下/禁用状态刷新背景、文字与图标颜色。
    ///     文字与图标共享同一前景色：常态取 <see cref="ColorType" /> 对应的文本色，
    ///     悬停转为强调色，禁用转为次要文本色。
    /// </summary>
    private void RefreshColor(bool animate)
    {
        if (!IsLoaded)
            return;

        var foreground = !IsEnabled
            ? "ColorBrushGray5"
            : IsPointerOver || ColorType == ColorState.Highlight
                ? "ColorBrush3"
                : "ColorBrush1";

        var background = !IsEnabled
            ? "ColorBrushTransparent"
            : _isMouseDown
                ? "ColorBrush6"
                : IsPointerOver
                    ? "ColorBrushBg1"
                    : "ColorBrushSemiTransparent";

        var time = _isMouseDown ? 70 : IsPointerOver ? 100 : 150;

        ButtonAnimation.ApplyColor(
            this,
            BackgroundProperty,
            background ?? string.Empty,
            background is null ? new MyColor(Colors.Transparent) : null,
            time,
            animate,
            BackgroundKey);

        ButtonAnimation.ApplyColor(
            LabText,
            TextBlock.ForegroundProperty,
            foreground,
            null,
            time,
            animate,
            ForegroundKey);

        ButtonAnimation.ApplyColor(
            ShapeLogo,
            Shape.FillProperty,
            foreground,
            null,
            time,
            animate,
            IconKey);
    }

    private void _ApplyColorClasses()
    {
        var highlight = ColorType == ColorState.Highlight;
        Classes.Set("color-black", !highlight);
        Classes.Set("color-highlight", highlight);
    }

    private void ApplyLogoScale()
    {
        LogoHost.RenderTransform = new ScaleTransform(EffectiveLogoScale, EffectiveLogoScale);
    }

    private void RefreshLogoHostVisibility()
    {
        if (HasAnyIcon)
        {
            LogoHost.IsVisible = true;
            LogoHost.Width = 16d;
            LogoHost.Height = 16d;
            LogoHost.Margin = new Thickness(12d, 0d, 0d, 0d);
            LabText.Margin = new Thickness(7d, 0d, 12d, 1d);
        }
        else
        {
            LogoHost.IsVisible = false;
            LogoHost.Width = 0d;
            LogoHost.Height = 16d;
            LogoHost.Margin = default;
            LabText.Margin = new Thickness(12d, 0d, 12d, 1d);
        }
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isMouseDown = true;
        Classes.Set("pressed", true);
        RefreshColor(true);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isMouseDown)
            return;

        _isMouseDown = false;
        Classes.Set("pressed", false);
        RefreshColor(true);

        PerformClick(e.KeyModifiers);
        e.Handled = true;
    }

    /// <summary>执行点击语义；鼠标释放、键盘 Enter/Space 与自动化调用共用。</summary>
    public void PerformClick()
    {
        PerformClick(KeyModifiers.None);
    }

    /// <summary>执行点击语义，并记录本次点击的修饰键。</summary>
    public void PerformClick(KeyModifiers modifiers)
    {
        if (!IsEnabled) return;
        LastClickKeyModifiers = modifiers;
        LogWrapper.Debug("[Control] 按下带图标按钮：" + Text);
        Click?.Invoke(this, new RouteEventArgs(true));
        CustomEventService.Raise(this);
        ExecuteCommand();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        Classes.Set("pressed", false);
        RefreshColor(true);
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
        return new PclButtonAutomationPeer(this, PerformClick, () => Text);
    }
}
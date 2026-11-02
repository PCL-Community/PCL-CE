using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     PCL 侧边栏文字按钮：图标可选，显示/隐藏以透明度与缩放过渡，
///     按下/释放带弹性回弹，悬停与禁用切换底色。
/// </summary>
public partial class MyExtraTextButton : Grid
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    private const int AnimationColorIn = 120;
    private const int AnimationColorOut = 150;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyExtraTextButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyExtraTextButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyExtraTextButton, object?>(nameof(CommandParameter));

    /// <summary>动画键使用的唯一编号。</summary>
    public readonly int Uuid = Interlocked.Increment(ref _uuidSeed);

    private static int _uuidSeed;
    private bool _isCommandRunning;
    private bool _isLeftMouseHeld;
    private bool _show;

    public MyExtraTextButton()
    {
        InitializeComponent();

        LabText.Text = Text;
        SizeChanged += (_, _) => _SyncCornerRadius();
        PanClick.PointerPressed += _OnPointerPressed;
        PanClick.PointerReleased += _OnPointerReleased;
        PanClick.PointerEntered += (_, _) => RefreshColor();
        PanClick.PointerExited += _OnPointerExited;
        Loaded += (_, _) => RefreshColor(false);
        ActualThemeVariantChanged += (_, _) => RefreshColor(false);
        Unloaded += (_, _) =>
        {
            PCL.Animations.AniStop(ScaleKey);
            PCL.Animations.AniStop(MainScaleKey);
            PCL.Animations.AniStop(ColorKey);
        };
        RefreshIconHostVisibility();
        _SyncCornerRadius();
    }

    public event ClickEventHandler? Click;

    /// <summary>旧版 Path 图标数据（SVG path 语法）。</summary>
    public string Logo
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            try
            {
                Path.Data = string.IsNullOrWhiteSpace(field) ? null : Geometry.Parse(field);
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "MyExtraTextButton", "解析 Logo 路径失败：" + field);
                Path.Data = null;
            }

            _ApplyIconVisibility();
            RefreshIconHostVisibility();
        }
    } = string.Empty;

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
            RefreshIconHostVisibility();
        }
    } = string.Empty;

    /// <summary>未使用 SVG 时的图标缩放（SVG 图标按 1.0 绘制）。</summary>
    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    /// <summary>是否显示；通过透明度与缩放动画切换。</summary>
    public bool Show
    {
        get => _show;
        set
        {
            if (_show == value)
                return;
            _show = value;
            _ApplyShow(value);
        }
    }

    public InlineCollection Inlines => LabText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set
        {
            if (value is null)
                return;
            SetValue(TextProperty, value);
        }
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

    private bool HasAnyIcon => IsUsingSvgIcon || !string.IsNullOrWhiteSpace(Logo);

    private string ScaleKey => ButtonAnimation.Name("MyExtraTextButton Scale", Uuid);

    private string MainScaleKey => ButtonAnimation.Name("MyExtraTextButton MainScale", Uuid);

    private string ColorKey => ButtonAnimation.Name("MyExtraTextButton Color", Uuid);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
            LabText.Text = change.NewValue as string ?? string.Empty;
        else if (change.Property == IsEnabledProperty)
            RefreshColor(false);
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

    private void RefreshIconHostVisibility()
    {
        if (HasAnyIcon)
        {
            IconHost.IsVisible = true;
            IconHost.Width = 16d;
            IconHost.Margin = new Thickness(2d, 12d, 0d, 12d);
            LabText.Margin = new Thickness(12d, 0d, 0d, 0.8d);
        }
        else
        {
            IconHost.IsVisible = false;
            IconHost.Width = 0d;
            IconHost.Margin = new Thickness(0d, 12d, 0d, 12d);
            LabText.Margin = new Thickness(0d, 0d, 0d, 0.8d);
        }
    }

    private void _SyncCornerRadius()
    {
        var radius = new CornerRadius(Math.Max(0d, Bounds.Height * 0.4d));
        PanClick.CornerRadius = radius;
        PanColor.CornerRadius = radius;
    }

    /// <summary>
    ///     显示/隐藏过渡：显示时透明度与缩放一起回弹，隐藏时反向收起后折叠。
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
                Opacity = 1d;
                ButtonAnimation.SetScale(this, 1d);
                return;
            }

            Opacity = 0d;
            PCL.Animations.AniStart(
                new List<PCL.Animations.AniData>
                {
                    PCL.Animations.AaOpacity(this, 1d, 80, 50),
                    PCL.Animations.AaScaleTransform(
                        this,
                        0.15d - ButtonAnimation.CurrentScale(this),
                        400,
                        50,
                        new PCL.Animations.AniEaseOutBack()),
                    PCL.Animations.AaScaleTransform(
                        this,
                        0.85d,
                        160,
                        50,
                        new PCL.Animations.AniEaseOutFluent())
                },
                MainScaleKey);
            return;
        }

        if (!ButtonAnimation.CanAnimate(this))
        {
            Opacity = 0d;
            ButtonAnimation.SetScale(this, 0d);
            IsVisible = false;
            return;
        }

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaOpacity(this, -Opacity, 50, 50),
                PCL.Animations.AaScaleTransform(
                    this,
                    -ButtonAnimation.CurrentScale(this),
                    100,
                    ease: new PCL.Animations.AniEaseInFluent(PCL.Animations.AniEasePower.Weak)),
                PCL.Animations.AaCode(
                    () =>
                    {
                        Opacity = 0d;
                        IsVisible = false;
                    },
                    after: true)
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

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScaleTransform(
                    PanScale,
                    targetScale - ButtonAnimation.CurrentScale(PanScale),
                    800,
                    ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong)),
                PCL.Animations.AaScaleTransform(
                    PanScale,
                    reboundScale,
                    reboundDuration,
                    ease: new PCL.Animations.AniEaseOutFluent())
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

        PCL.Animations.AniStart(
            PCL.Animations.AaScaleTransform(
                PanScale,
                1d - ButtonAnimation.CurrentScale(PanScale),
                300,
                ease: new PCL.Animations.AniEaseOutBack()),
            ScaleKey);
    }

    /// <summary>按当前悬停/禁用状态刷新底色。</summary>
    public void RefreshColor(bool animate = true)
    {
        if (!IsLoaded)
            return;

        var key = !IsEnabled
            ? "PageBrushDebug"
            : IsPointerOver
                ? "PageBrushAccentHover"
                : "PageBrushAccent";

        ButtonAnimation.ApplyColor(
            PanColor,
            Border.BackgroundProperty,
            key,
            null,
            !IsEnabled || IsPointerOver ? AnimationColorIn : AnimationColorOut,
            animate,
            ColorKey);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!_isLeftMouseHeld)
            _StartScaleAnimation(0.85d, -0.05d);
        _isLeftMouseHeld = true;
        Focus();
        RefreshColor();
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || !_isLeftMouseHeld)
            return;

        LogWrapper.Debug("[Control] 按下附加图标按钮：" + Text);
        _isLeftMouseHeld = false;
        Click?.Invoke(this, e);
        e.Handled = true;
        CustomEventService.Raise(this);
        _RefreshScaleAfterRelease();
        RefreshColor();
        ExecuteCommand();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _isLeftMouseHeld = false;
        if (ButtonAnimation.CanAnimate(this))
            PCL.Animations.AniStart(
                PCL.Animations.AaScaleTransform(
                    PanScale,
                    1d - ButtonAnimation.CurrentScale(PanScale),
                    500,
                    ease: new PCL.Animations.AniEaseOutFluent()),
                ScaleKey);
        else
            ButtonAnimation.SetScale(PanScale, 1d);

        RefreshColor();
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
}
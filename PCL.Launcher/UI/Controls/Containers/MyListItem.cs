using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Core.App;
using PCL.UI.Controls.Common;
using Path = Avalonia.Controls.Shapes.Path;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     列表项：标题 + 副文本 + 标签 + 图标，可切换为单选、复选或纯点击三种交互。
///     外层只承接页面转场，内层承载悬停/按下缩放，避免两套 RenderTransform 互相覆盖。
///     颜色、背景、按钮和选中条动画均由状态机统一驱动。
/// </summary>
public partial class MyListItem : Grid, IMyRadio
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs? e);

    private const int AnimationTimeIn = 120;
    private const int AnimationTimeOut = 180;

    public readonly int Uuid = LauncherCommon.GetUuid();

    private readonly ColumnDefinition _columnCheck;
    private readonly ColumnDefinition _columnLogo;
    private readonly ColumnDefinition _columnPaddingLeft;
    private readonly ColumnDefinition _columnPaddingRight;
    private bool _isCommandRunning;
    private bool _isExecutingCheckCommand;
    private bool _isHoverAnimationEnabled = true;
    private bool _isMouseDown;
    private bool _isSettingChecked;
    private bool _isSyncingGroup;
    private bool _isUserInteraction;
    private bool _showButtonsOnKeyboardFocus;

    private string? _stateLast;

    public MyListItem()
    {
        InitializeComponent();

        _columnCheck = PanBackElement.ColumnDefinitions[0];
        _columnPaddingLeft = PanBackElement.ColumnDefinitions[1];
        _columnLogo = PanBackElement.ColumnDefinitions[2];
        _columnPaddingRight = PanBackElement.ColumnDefinitions[5];

        ContainerTheme.BindDynamic(this, ForegroundProperty, "ColorBrush1");
        LabTitleElement.Bind(
            TextBlock.ForegroundProperty,
            new Binding(nameof(Foreground))
            {
                Source = this
            });
        LabTitleElement.Bind(
            TextBlock.FontSizeProperty,
            new Binding(nameof(FontSize))
            {
                Source = this
            });
        ContainerTheme.BindDynamic(LabInfoElement, TextBlock.ForegroundProperty, "ColorBrushGray2");
        FocusAdorner = null;

        SizeChanged += (_, _) => OnSizeChanged();
        PointerReleased += Button_MouseUp;
        PointerPressed += Button_MouseDown;
        PointerExited += Button_MouseLeave;
        PointerCaptureLost += (_, _) => ResetPointerState();
        PointerEntered += (_, _) => RefreshColor(this, EventArgs.Empty);
        GotFocus += (_, e) => OnFocusChanged(e.NavigationMethod);
        LostFocus += (_, _) => OnFocusLost();
        KeyboardActivation.Attach(
            this,
            modifiers => PerformClick(modifiers),
            null,
            false);
        Loaded += (_, _) => MyListItem_Loaded();
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    #region 菜单与事件

    public override string ToString()
    {
        return Title;
    }

    #endregion

    #region 事件与属性

    public event IMyRadio.CheckEventHandler? Check;
    public event IMyRadio.ChangedEventHandler? Changed;
    public event ClickEventHandler? Click;

    /// <summary>最近一次点击携带的修饰键；键盘和自动化调用为 <see cref="KeyModifiers.None" />。</summary>
    public KeyModifiers LastClickKeyModifiers { get; private set; }

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(FontSize), 14d);

    public static readonly StyledProperty<string> InfoProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Info), string.Empty);

    public static readonly StyledProperty<string> LogoProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(Logo), string.Empty);

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyListItem, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<CornerRadius> LogoCornerRadiusProperty =
        AvaloniaProperty.Register<MyListItem, CornerRadius>(
            nameof(LogoCornerRadius),
            new CornerRadius(-1d));

    public static readonly StyledProperty<CheckType> TypeProperty =
        AvaloniaProperty.Register<MyListItem, CheckType>(nameof(Type));

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyListItem, bool>(
            nameof(Checked),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsScaleAnimationEnabledProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(IsScaleAnimationEnabled), true);

    public static readonly StyledProperty<int> PaddingLeftProperty =
        AvaloniaProperty.Register<MyListItem, int>(nameof(PaddingLeft));

    public static readonly StyledProperty<int> MinPaddingRightProperty =
        AvaloniaProperty.Register<MyListItem, int>(nameof(MinPaddingRight), 4);

    public static readonly StyledProperty<object?> TagsProperty =
        AvaloniaProperty.Register<MyListItem, object?>(nameof(Tags));

    public static readonly StyledProperty<IEnumerable<MyIconButton>?> ButtonsProperty =
        AvaloniaProperty.Register<MyListItem, IEnumerable<MyIconButton>?>(nameof(Buttons));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<MyListItem, IBrush?>(nameof(Foreground), Brushes.Black);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyListItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyListItem, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyListItem, ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyListItem, object?>(nameof(CheckCommandParameter));

    static MyListItem()
    {
        TitleProperty.Changed.AddClassHandler<MyListItem>((item, e) => item.OnTitleChanged(e));
        InfoProperty.Changed.AddClassHandler<MyListItem>((item, e) => item.OnInfoChanged(e));
        LogoProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
            item.UpdateLogo(e.NewValue as string));
        SvgIconProperty.Changed.AddClassHandler<MyListItem>((item, _) =>
            item.UpdateLogo(item.Logo));
        ButtonsProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
            item.SetButtons(e.NewValue as IEnumerable<MyIconButton>));
        TypeProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
            item.ApplyType(e.NewValue is CheckType type ? type : CheckType.None));
        LogoScaleProperty.Changed.AddClassHandler<MyListItem>((item, _) => item.ApplyLogoScale());
        LogoCornerRadiusProperty.Changed.AddClassHandler<MyListItem>((item, _) =>
            item.RefreshLogoCornerRadius());
        IsScaleAnimationEnabledProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
            item.ApplyScaleAnimationEnabled(e.NewValue is true));
        PaddingLeftProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
            item.ApplyPaddingLeft(e.NewValue is int value ? value : 0));
        MinPaddingRightProperty.Changed.AddClassHandler<MyListItem>((item, _) =>
            item.OnMinPaddingRightChanged());
        TagsProperty.Changed.AddClassHandler<MyListItem>((item, e) => item.SetTags(e.NewValue));
    }

    /// <summary>标题；换行会被移除。</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, (value ?? string.Empty).Replace("\r", "").Replace("\n", ""));
    }

    /// <summary>字号。</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>副文本。</summary>
    public string Info
    {
        get => GetValue(InfoProperty);
        set
        {
            if (Info == value)
                return;
            SetValue(InfoProperty, (value ?? string.Empty).Replace("\r", "").Replace("\n", ""));
        }
    }

    /// <summary>图片地址（http / 本地位图）或矢量图 Data。</summary>
    public string Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    /// <summary>lucide 图标名（如 lucide/x）；优先于 Logo。</summary>
    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    /// <summary>图标圆角；仅对位图 Logo 生效，任一角小于 0 时关闭。</summary>
    public CornerRadius LogoCornerRadius
    {
        get => GetValue(LogoCornerRadiusProperty);
        set => SetValue(LogoCornerRadiusProperty, value);
    }

    /// <summary>是否启用缩放动画；影响 RectBack 与 PanBack 的 Transform。</summary>
    public bool IsScaleAnimationEnabled
    {
        get => GetValue(IsScaleAnimationEnabledProperty);
        set => SetValue(IsScaleAnimationEnabledProperty, value);
    }

    /// <summary>左侧缩进。</summary>
    public int PaddingLeft
    {
        get => GetValue(PaddingLeftProperty);
        set => SetValue(PaddingLeftProperty, value);
    }

    /// <summary>右边距的最小值。存在右侧按钮时，悬停会扩展到 5 + 按钮数 * 25。</summary>
    public int MinPaddingRight
    {
        get => GetValue(MinPaddingRightProperty);
        set => SetValue(MinPaddingRightProperty, value);
    }

    /// <summary>右侧按钮集合。</summary>
    public IEnumerable<MyIconButton>? Buttons
    {
        get => GetValue(ButtonsProperty);
        set => SetValue(ButtonsProperty, value);
    }

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

    public ICommand? CheckCommand
    {
        get => GetValue(CheckCommandProperty);
        set => SetValue(CheckCommandProperty, value);
    }

    public object? CheckCommandParameter
    {
        get => GetValue(CheckCommandParameterProperty);
        set => SetValue(CheckCommandParameterProperty, value);
    }

    public enum CheckType
    {
        None,
        Clickable,
        RadioBox,
        CheckBox
    }

    public CheckType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false, value != Checked);
    }

    /// <summary>标签：String（以 | 分隔）或字符串集合，内容按本地化资源键解析。</summary>
    public object? Tags
    {
        get => GetValue(TagsProperty);
        set => SetValue(TagsProperty, value);
    }

    /// <summary>直接写入的标签文本；不做本地化，语言切换不影响这些标签。</summary>
    public IEnumerable<string>? PlainTags
    {
        set => TagPanelHelper.FillPlain(PanTagsElement, value);
    }

    private void SetTags(object? value)
    {
        var list = value switch
        {
            string str => [.. str.Split('|')],
            IEnumerable<string> strings => [.. strings],
            _ => new List<string>()
        };
        TagPanelHelper.FillLocalized(PanTagsElement, list);
    }

    /// <summary>是否启用鼠标悬停动画；逐项进入动画期间由 MyPageLeft 临时关闭。</summary>
    public bool HoverAnimationEnabled
    {
        get => _isHoverAnimationEnabled;
        set
        {
            if (_isHoverAnimationEnabled == value)
                return;
            _isHoverAnimationEnabled = value;
            _stateLast = null;
            RefreshColor(this, EventArgs.Empty);
        }
    }

    #endregion

    #region 视觉部件

    public Grid PanBack => PanBackElement;

    public Border RectBack => RectBackElement;

    public Border RectCheck => RectCheckElement;

    public TextBlock LabTitle => LabTitleElement;

    public StackPanel PanTags => PanTagsElement;

    public TextBlock LabInfo => LabInfoElement;

    /// <summary>标题内联集合；仅供初始化完成后的代码调用。</summary>
    public InlineCollection Inlines => LabTitleElement.Inlines!;

    public Control? buttonStack;
    public Control? pathLogo;

    #endregion

    #region 生命周期

    private void MyListItem_Loaded()
    {
        _stateLast = null;
        ApplyType(Type);
        UpdateBackChrome();
        UpdateInteractivity();
        _columnPaddingRight.Width = new GridLength(MinPaddingRight);
        ApplyCheckedForeground();
        RefreshColor(this, EventArgs.Empty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateBackChrome();
        ApplyCheckedForeground();
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        AnimationService.CancelAnimationByName("ListItem Color " + Uuid);
        AnimationService.CancelAnimationByName("MyListItem Checked " + Uuid);
        _isMouseDown = false;
        _stateLast = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyCheckedForeground();
        if (RectBackElement.Opacity <= 0d)
            BindRectBackIdle();
        else
            ApplyColorStateInstant(_stateLast ?? "Idle");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CheckedProperty && !_isSettingChecked)
        {
            EnsureSingleChecked();
            RefreshCheckedVisual(change.NewValue is true, true);
        }
        else if (change.Property == IsEnabledProperty
                 || change.Property == IsVisibleProperty
                 || change.Property == IsHitTestVisibleProperty
                 || change.Property == CommandProperty
                 || change.Property == CheckCommandProperty)
        {
            UpdateInteractivity();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return Type switch
        {
            CheckType.RadioBox => new PclToggleAutomationPeer(
                this,
                () => Checked,
                () => PerformClick(),
                () => Title,
                AutomationControlType.RadioButton),
            CheckType.CheckBox => new PclToggleAutomationPeer(
                this,
                () => Checked,
                () => PerformClick(),
                () => Title),
            _ => new PclButtonAutomationPeer(
                this,
                () => PerformClick(),
                () => Title,
                AutomationControlType.ListItem)
        };
    }

    #endregion

    #region 颜色与尺寸

    private double EffectiveHeight
    {
        get
        {
            if (Bounds.Height > 0d)
                return Bounds.Height;
            return double.IsNaN(Height) ? 0d : Height;
        }
    }

    private bool IsCompact => EffectiveHeight > 0d && EffectiveHeight < 40d;

    /// <summary>悬停/按下/离开时的颜色、背景缩放与右侧按钮过渡。</summary>
    public void RefreshColor(object? sender, EventArgs e)
    {
        string stateNew;
        int time;
        if (_isMouseDown && !(Type == CheckType.RadioBox && Checked))
        {
            stateNew = "MouseDown";
            time = AnimationTimeIn;
        }
        else if (IsPointerOver && _isHoverAnimationEnabled)
        {
            stateNew = "MouseOver";
            time = AnimationTimeIn;
        }
        else
        {
            stateNew = "Idle";
            time = AnimationTimeOut;
        }

        if ((_stateLast ?? string.Empty) == stateNew)
            return;
        _stateLast = stateNew;

        if (!ContainerAnimation.CanAnimate(this))
        {
            ApplyColorStateInstant(stateNew);
            return;
        }

        var animationKey = "ListItem Color " + Uuid;
        var animations = new List<IAnimation>();
        var showButtons = ShouldShowButtons(stateNew);
        AddButtonAnimations(animations, showButtons, time);

        var back = RectBackElement;
        if (stateNew is "MouseOver" or "MouseDown")
        {
            animations.Add(
                new OpacityAnimation(
                    back,
                    1d - back.Opacity,
                    time,
                    ease: new FluentEaseOut()));
            animations.Add(
                new ColorAnimation(
                    back,
                    Border.BackgroundProperty,
                    stateNew == "MouseDown" ? "ColorBrush6" : "ColorBrushBg1",
                    time));

            if (IsScaleAnimationEnabled)
            {
                var backScale = ContainerAnimation.EnsureScale(back, 0.8d, 0.8d);
                var selfScale = ContainerAnimation.EnsureScale(PanBackElement);
                animations.Add(
                    new ScaleTransformAnimation(
                        back,
                        1d - backScale.ScaleX,
                        (int)Math.Round(time * 1.6d),
                        ease: new FluentEaseOut()));
                animations.Add(
                    new ScaleTransformAnimation(
                        PanBackElement,
                        (_isMouseDown ? 0.98d : 1d) - selfScale.ScaleX,
                        (int)Math.Round(time * (_isMouseDown ? 0.9d : 1.2d)),
                        ease: new FluentEaseOut()));
            }
        }
        else
        {
            animations.Add(new OpacityAnimation(back, -back.Opacity, time));
            animations.Add(
                new ColorAnimation(back, Border.BackgroundProperty, "ColorBrush7", time));

            if (IsScaleAnimationEnabled)
            {
                var backScale = ContainerAnimation.EnsureScale(back, 0.8d, 0.8d);
                var selfScale = ContainerAnimation.EnsureScale(PanBackElement);
                animations.Add(
                    new ScaleTransformAnimation(
                        PanBackElement,
                        1d - selfScale.ScaleX,
                        time * 3,
                        ease: new FluentEaseOut()));
                animations.Add(
                    new ScaleTransformAnimation(
                        back,
                        0.996d - backScale.ScaleX,
                        time,
                        ease: new FluentEaseOut()));
                animations.Add(new ScaleTransformAnimation(back, -0.246d, 1, after: true));
            }
        }

        AnimationService.Run(animations, animationKey);
    }

    /// <summary>无动画路径：直接写入悬停/按下/离开的终态。</summary>
    private void ApplyColorStateInstant(string stateNew)
    {
        var showButtons = ShouldShowButtons(stateNew);
        if (buttonStack is not null)
        {
            buttonStack.Opacity = showButtons ? 1d : 0d;
            buttonStack.IsHitTestVisible = showButtons;
            _columnPaddingRight.Width = new GridLength(
                showButtons
                    ? Math.Max(MinPaddingRight, 5d + (Buttons?.Count() ?? 0) * 25d)
                    : MinPaddingRight);
            SetButtonTabStops(showButtons);
        }

        var back = RectBackElement;
        back.Opacity = stateNew == "Idle" ? 0d : 1d;
        switch (stateNew)
        {
            case "MouseDown":
                ContainerTheme.BindDynamic(back, Border.BackgroundProperty, "ColorBrush6");
                break;
            case "MouseOver":
                ContainerTheme.BindDynamic(back, Border.BackgroundProperty, "ColorBrushBg1");
                break;
            default:
                BindRectBackIdle();
                break;
        }

        if (IsScaleAnimationEnabled)
        {
            back.RenderTransformOrigin = RelativePoint.Center;
            back.RenderTransform = new ScaleTransform(
                stateNew == "Idle" ? 0.75d : 1d,
                stateNew == "Idle" ? 0.75d : 1d);
            PanBackElement.RenderTransformOrigin = RelativePoint.Center;
            PanBackElement.RenderTransform = new ScaleTransform(
                stateNew == "MouseDown" ? 0.98d : 1d,
                stateNew == "MouseDown" ? 0.98d : 1d);
        }
        else
        {
            back.RenderTransform = null;
            PanBackElement.RenderTransform = null;
        }
    }

    private bool ShouldShowButtons(string stateNew)
    {
        return stateNew is "MouseOver" or "MouseDown"
            || (_showButtonsOnKeyboardFocus && IsKeyboardFocusWithin);
    }

    private void AddButtonAnimations(List<IAnimation> animations, bool show, int time)
    {
        if (buttonStack is null)
            return;

        buttonStack.IsHitTestVisible = show;
        SetButtonTabStops(show);
        var targetPadding = show
            ? Math.Max(MinPaddingRight, 5d + (Buttons?.Count() ?? 0) * 25d)
            : MinPaddingRight;
        animations.Add(
            new OpacityAnimation(
                buttonStack,
                (show ? 1d : 0d) - buttonStack.Opacity,
                (int)Math.Round(time * (show ? 0.7d : 0.4d)),
                show ? (int)Math.Round(time * 0.3d) : 0));
        animations.Add(
            new DoubleAnimation(
                delta => _columnPaddingRight.Width =
                    new GridLength(Math.Max(0d, _columnPaddingRight.Width.Value + delta)),
                targetPadding - _columnPaddingRight.Width.Value,
                (int)Math.Round(time * (show ? 0.3d : 0.4d)),
                show ? (int)Math.Round(time * 0.7d) : 0));
    }

    private void OnFocusChanged(NavigationMethod navigationMethod)
    {
        _showButtonsOnKeyboardFocus =
            navigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private void OnFocusLost()
    {
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private void OnSizeChanged()
    {
        var hasLogo = IsUsingSvgIcon || !string.IsNullOrWhiteSpace(Logo);
        _columnCheck.Width = new GridLength(
            Type is CheckType.None or CheckType.Clickable
                ? IsCompact
                    ? 4d
                    : 2d
                : 6d);
        _columnLogo.Width = new GridLength((hasLogo ? 34d : 0d) + (IsCompact ? 0d : 4d));

        pathLogo?.Margin = IsBitmapLogo
            ? new Thickness(4d, 5d, 3d, 5d)
            : new Thickness(
                IsCompact ? 6d : 8d,
                8d,
                IsCompact ? 4d : 6d,
                8d);

        LabTitleElement.Margin = new Thickness(4d, 0d, 0d, IsCompact ? 0d : 2d);
        UpdateBackChrome();
        if (Checked)
            ApplyCheckedForeground();
    }

    private void UpdateBackChrome()
    {
        var radius = IsScaleAnimationEnabled || EffectiveHeight > 40d ? 6d : 0d;
        RectBackElement.CornerRadius = new CornerRadius(radius);
        RectBackElement.RenderTransformOrigin = RelativePoint.Center;
        PanBackElement.RenderTransformOrigin = RelativePoint.Center;

        if (IsScaleAnimationEnabled)
        {
            if (RectBackElement.RenderTransform is not ScaleTransform)
                RectBackElement.RenderTransform = new ScaleTransform(0.75d, 0.75d);
            if (PanBackElement.RenderTransform is not ScaleTransform)
                PanBackElement.RenderTransform = new ScaleTransform(1d, 1d);
        }
        else
        {
            RectBackElement.RenderTransform = null;
            PanBackElement.RenderTransform = null;
        }
    }

    private void BindRectBackIdle()
    {
        ContainerTheme.BindDynamic(RectBackElement, Border.BackgroundProperty, "ColorBrush7");
        ContainerTheme.BindDynamic(RectBackElement, Border.BorderBrushProperty, "ColorBrush6");
    }

    #endregion

    #region 属性应用

    private void OnTitleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        LabTitleElement.Text =
            (e.NewValue as string ?? string.Empty).Replace("\r", "").Replace("\n", "");
    }

    private void OnInfoChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var value = e.NewValue as string;
        LabInfoElement.Text = value ?? string.Empty;
        LabInfoElement.IsVisible = !string.IsNullOrEmpty(value);
    }

    private void ApplyPaddingLeft(int value)
    {
        _columnPaddingLeft.Width = new GridLength(value);
    }

    private void ApplyScaleAnimationEnabled(bool value)
    {
        UpdateBackChrome();
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private void OnMinPaddingRightChanged()
    {
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    #endregion

    #region Logo

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private void UpdateLogo(string? logo)
    {
        if (pathLogo is not null)
            PanBackElement.Children.Remove(pathLogo);
        pathLogo = null;

        if (IsUsingSvgIcon)
        {
            var icon = new SvgIconControl
            {
                IsHitTestVisible = false,
                Icon = SvgIcon,
                Stretch = Stretch.Uniform,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new ScaleTransform(1d, 1d),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            icon.Bind(
                SvgIconControl.IconBrushProperty,
                new Binding(nameof(Foreground))
                {
                    Source = this
                });
            pathLogo = icon;
        }
        else if (!string.IsNullOrEmpty(logo))
        {
            if (logo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                pathLogo = new MyImage
                {
                    IsHitTestVisible = false,
                    Source = logo,
                    CornerRadius = LogoCornerRadius,
                    Stretch = Stretch.Uniform,
                    RenderTransformOrigin = RelativePoint.Center,
                    RenderTransform = new ScaleTransform(LogoScale, LogoScale),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            else if (IsBitmapPath(logo))
                pathLogo = new ListItemLogoImage
                {
                    IsHitTestVisible = false,
                    Source = logo,
                    CornerRadius = LogoCornerRadius,
                    UseNearestNeighbor = IsAvatarCachePath(logo),
                    RenderTransformOrigin = RelativePoint.Center,
                    RenderTransform = new ScaleTransform(LogoScale, LogoScale),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            else
                try
                {
                    var path = new Path
                    {
                        IsHitTestVisible = false,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Stretch = Stretch.Uniform,
                        Data = Geometry.Parse(logo),
                        RenderTransformOrigin = RelativePoint.Center,
                        RenderTransform = new ScaleTransform(LogoScale, LogoScale)
                    };
                    path.Bind(
                        Shape.FillProperty,
                        new Binding(nameof(Foreground))
                        {
                            Source = this
                        });
                    pathLogo = path;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "解析列表项 Logo 失败：" + logo);
                    pathLogo = null;
                }
        }

        if (pathLogo is not null)
        {
            SetColumn(pathLogo, 2);
            SetRowSpan(pathLogo, 4);
            OnSizeChanged();
            PanBackElement.Children.Add(pathLogo);
        }

        var hasLogo = IsUsingSvgIcon || !string.IsNullOrWhiteSpace(logo);
        _columnLogo.Width = new GridLength((hasLogo ? 34d : 0d) + (IsCompact ? 0d : 4d));
    }

    private static bool IsBitmapPath(string logo)
    {
        return logo.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || logo.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || logo.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsBitmapLogo => !IsUsingSvgIcon && IsBitmapPath(Logo);

    private static bool IsAvatarCachePath(string path)
    {
        var temp = Paths.Temp;
        if (string.IsNullOrEmpty(temp))
            return false;

        var normalized = path.Replace('\\', '/');
        var headCache = System.IO.Path.Combine(temp, "Cache", "Skin", "Head").Replace('\\', '/');
        var capeCache = System.IO.Path.Combine(temp, "Cache", "Cape").Replace('\\', '/');
        return normalized.Contains(headCache, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(capeCache, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyLogoScale()
    {
        if (pathLogo is null) return;
        var scale = IsUsingSvgIcon ? 1d : LogoScale;
        pathLogo.RenderTransform = new ScaleTransform(scale, scale);
        pathLogo.RenderTransformOrigin = RelativePoint.Center;
    }

    private bool IsLogoCornerRadiusEnabled()
    {
        return LogoCornerRadius is
            { TopLeft: >= 0, TopRight: >= 0, BottomLeft: >= 0, BottomRight: >= 0 };
    }

    private void RefreshLogoCornerRadius()
    {
        if (pathLogo is MyImage image && IsLogoCornerRadiusEnabled())
            image.CornerRadius = LogoCornerRadius;
    }

    #endregion

    #region 勾选

    /// <summary>手动设置 Checked 属性。</summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
    public void SetChecked(bool value, bool user, bool anime)
    {
        try
        {
            var changedEventArgs = new RouteEventArgs(user);
            var rawValue = Checked;
            _isUserInteraction = user;
            if (Type == CheckType.RadioBox)
            {
                if (IsInitialized && value != Checked)
                {
                    SetCheckedValue(value);
                    Changed?.Invoke(this, changedEventArgs);
                    if (changedEventArgs.handled)
                    {
                        RevertChecked(rawValue);
                        return;
                    }
                }

                SetCheckedValue(value);
            }
            else
            {
                if (value == Checked)
                    return;
                SetCheckedValue(value);
                if (IsInitialized)
                {
                    Changed?.Invoke(this, changedEventArgs);
                    if (changedEventArgs.handled)
                    {
                        RevertChecked(rawValue);
                        return;
                    }
                }
            }

            if (value)
            {
                var checkEventArgs = new RouteEventArgs(user);
                Check?.Invoke(this, checkEventArgs);
                if (checkEventArgs.handled)
                {
                    RevertChecked(rawValue);
                    return;
                }
            }

            if (Type == CheckType.RadioBox)
            {
                if (Parent is null)
                    return;
                EnsureSingleChecked();
            }

            RefreshCheckedVisual(value, anime);

            if (_isUserInteraction && Checked != rawValue)
                ExecuteCheckCommand();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "设置 Checked 失败");
        }
        finally
        {
            _isUserInteraction = false;
        }
    }

    private void RevertChecked(bool rawValue)
    {
        SetCheckedValue(rawValue);
        RefreshCheckedVisual(rawValue, false);
    }

    private void SetCheckedValue(bool value)
    {
        _isSettingChecked = true;
        try
        {
            SetValue(CheckedProperty, value);
        }
        finally
        {
            _isSettingChecked = false;
        }
    }

    private void EnsureSingleChecked()
    {
        if (Type != CheckType.RadioBox || _isSyncingGroup)
            return;
        if (Parent is not Panel panel)
            return;

        var radioBoxList = panel
            .Children
            .OfType<MyListItem>()
            .Where(item => item.Type == CheckType.RadioBox && item.IsVisible)
            .ToList();
        if (radioBoxList.Count == 0)
            return;

        var checkedCount = radioBoxList.Count(item => item.Checked);
        _isSyncingGroup = true;
        try
        {
            switch (checkedCount)
            {
                case 0:
                {
                    radioBoxList[0].Checked = true;
                    break;
                }

                case > 1 when Checked:
                {
                    foreach (var control in radioBoxList.Where(control =>
                                 control.Checked && !ReferenceEquals(control, this)))
                        control.Checked = false;
                    break;
                }

                case > 1:
                {
                    var firstChecked = false;
                    foreach (var control in radioBoxList.Where(control => control.Checked))
                        if (firstChecked)
                            control.Checked = false;
                        else
                            firstChecked = true;
                    break;
                }
            }
        }
        finally
        {
            _isSyncingGroup = false;
        }
    }

    /// <summary>选中/取消时的左侧竖条弹性缩放与前景色过渡。</summary>
    private void RefreshCheckedVisual(bool value, bool anime)
    {
        const double customHeight = 20d;
        var animationKey = "MyListItem Checked " + Uuid;
        var check = RectCheckElement;

        if (Type is CheckType.None or CheckType.Clickable)
        {
            check.IsVisible = false;
            check.Height = 0d;
            check.Opacity = 0d;
            ApplyCheckedForeground();
            return;
        }

        check.IsVisible = true;
        if (anime && ContainerAnimation.CanAnimate(this))
        {
            var animations = new List<IAnimation>();
            if (value)
            {
                check.Height = customHeight;
                check.VerticalAlignment = VerticalAlignment.Center;
                check.Margin = new Thickness(-1d, 0d, 0d, 0d);
                check.Opacity = 1d;
                check.RenderTransformOrigin = RelativePoint.Center;
                var scale = new ScaleTransform(1d, 0d);
                check.RenderTransform = scale;

                animations.Add(
                    new DoubleAnimation(
                        delta => scale.ScaleY = Math.Max(0d, scale.ScaleY + delta),
                        1d - scale.ScaleY,
                        300,
                        ease: new BackEaseWithPowerOut(EasePower.Weak)));
                animations.Add(
                    new ColorAnimation(
                        this,
                        ForegroundProperty,
                        CheckedForegroundKey,
                        200));
            }
            else
            {
                var scale = ContainerAnimation.EnsureScale(check);
                animations.Add(
                    new DoubleAnimation(
                        delta => scale.ScaleY = Math.Max(0d, scale.ScaleY + delta),
                        -scale.ScaleY,
                        120,
                        ease: new FluentEaseIn(EasePower.Weak)));
                animations.Add(new OpacityAnimation(check, -check.Opacity, 70, 40));
                animations.Add(
                    new ColorAnimation(this, ForegroundProperty, "ColorBrush1", 120));
            }

            animations.Add(new ActionAnimation(ApplyCheckedForeground, true));
            AnimationService.Run(animations, animationKey);
            return;
        }

        AnimationService.CancelAnimationByName(animationKey);
        check.Height = value ? customHeight : 0d;
        check.Margin = new Thickness(-1d, 0d, 0d, 0d);
        check.Opacity = value ? 1d : 0d;
        check.VerticalAlignment = VerticalAlignment.Center;
        check.RenderTransform = null;
        ApplyCheckedForeground();
    }

    private string CheckedForegroundKey => IsCompact ? "ColorBrush3" : "ColorBrush2";

    private void ApplyCheckedForeground()
    {
        ContainerTheme.BindDynamic(
            this,
            ForegroundProperty,
            Checked && Type is CheckType.RadioBox or CheckType.CheckBox
                ? CheckedForegroundKey
                : "ColorBrush1");
    }

    private void ExecuteCheckCommand()
    {
        var command = CheckCommand;
        if (_isExecutingCheckCommand
            || command is null
            || !command.CanExecute(CheckCommandParameter))
            return;
        _isExecutingCheckCommand = true;
        try
        {
            command.Execute(CheckCommandParameter);
        }
        finally
        {
            _isExecutingCheckCommand = false;
        }
    }

    #endregion

    #region 类型与按钮

    private void ApplyType(CheckType value)
    {
        _columnCheck.Width = new GridLength(
            value is CheckType.None or CheckType.Clickable
                ? IsCompact
                    ? 4d
                    : 2d
                : 6d);

        var isCheck = value is CheckType.RadioBox or CheckType.CheckBox;
        RectCheckElement.IsVisible = isCheck;
        if (!isCheck)
        {
            if (Checked)
                SetCheckedValue(false);
            RectCheckElement.Height = 0d;
            RectCheckElement.Opacity = 0d;
            ApplyCheckedForeground();
        }
        else if (Checked)
        {
            RefreshCheckedVisual(true, false);
        }
        else
        {
            RectCheckElement.Height = 0d;
            RectCheckElement.Opacity = 0d;
        }

        UpdateInteractivity();
    }

    private void SetButtons(IEnumerable<MyIconButton>? value)
    {
        if (buttonStack is not null)
        {
            PanBackElement.Children.Remove(buttonStack);
            buttonStack = null;
        }

        if (value is null)
        {
            ResetButtonPadding();
            return;
        }

        var buttons = value.ToList();
        switch (buttons.Count)
        {
            case 0:
            {
                ResetButtonPadding();
                return;
            }

            case 1:
            {
                var button = buttons[0];
                ConfigureButton(button, true);
                button.Opacity = 0d;
                button.IsHitTestVisible = false;
                button.IsTabStop = false;
                SetColumnSpan(button, 10);
                SetRowSpan(button, 10);
                PanBackElement.Children.Add(button);
                buttonStack = button;
                break;
            }

            default:
            {
                var stack = new StackPanel
                {
                    Opacity = 0d,
                    IsHitTestVisible = false,
                    Margin = new Thickness(0d, 0d, 5d, 0d),
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                SetColumnSpan(stack, 10);
                SetRowSpan(stack, 10);
                foreach (var button in buttons)
                {
                    ConfigureButton(button, false);
                    button.IsTabStop = false;
                    stack.Children.Add(button);
                }

                PanBackElement.Children.Add(stack);
                buttonStack = stack;
                break;
            }
        }

        _columnPaddingRight.Width = new GridLength(MinPaddingRight);
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private static void ConfigureButton(MyIconButton button, bool withRightMargin)
    {
        if (double.IsNaN(button.Height))
            button.Height = 25d;
        if (double.IsNaN(button.Width))
            button.Width = 25d;
        button.Margin = withRightMargin
            ? new Thickness(0d, 0d, 5d, 0d)
            : default;
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Center;
    }

    private void ResetButtonPadding()
    {
        _columnPaddingRight.Width = new GridLength(MinPaddingRight);
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private void SetButtonTabStops(bool enabled)
    {
        if (Buttons is null)
            return;
        foreach (var button in Buttons)
            button.IsTabStop = enabled;
    }

    #endregion

    #region 点击

    private void Button_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isMouseDown)
            return;
        if (IsInteractiveChild(e.Source))
        {
            ResetPointerState();
            return;
        }

        _isMouseDown = false;
        buttonStack?.IsHitTestVisible = true;
        PerformClick(e.KeyModifiers, e);
        e.Handled = true;
    }

    /// <summary>执行点击语义；鼠标释放、键盘 Enter/Space 与自动化调用共用。</summary>
    public void PerformClick(
        KeyModifiers modifiers = KeyModifiers.None,
        PointerReleasedEventArgs? pointerArgs = null)
    {
        if (!IsEnabled)
            return;
        LastClickKeyModifiers = modifiers;
        Click?.Invoke(this, pointerArgs);
        ExecuteCommand();
        if (HasCustomEvent)
            CustomEventService.Raise(this);

        switch (Type)
        {
            case CheckType.Clickable:
                LauncherCommon.Log("[Control] 按下单击列表项：" + Title);
                break;

            case CheckType.RadioBox:
                LauncherCommon.Log("[Control] 按下单选列表项：" + Title);
                if (!Checked)
                    SetChecked(true, true, true);
                break;

            case CheckType.CheckBox:
                LauncherCommon.Log("[Control] 按下复选列表项（" + !Checked + "）：" + Title);
                SetChecked(!Checked, true, true);
                break;

            case CheckType.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        RefreshColor(this, EventArgs.Empty);
    }

    private void Button_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (!IsInteractiveItem || IsInteractiveChild(e.Source))
            return;

        _isMouseDown = true;
        _showButtonsOnKeyboardFocus = false;
        buttonStack?.IsHitTestVisible = false;
        RefreshColor(this, EventArgs.Empty);
    }

    private void Button_MouseLeave(object? sender, PointerEventArgs e)
    {
        ResetPointerState();
    }

    private void ResetPointerState()
    {
        _isMouseDown = false;
        buttonStack?.IsHitTestVisible = true;
        _stateLast = null;
        RefreshColor(this, EventArgs.Empty);
    }

    private bool IsInteractiveChild(object? source)
    {
        if (source is not Visual visual)
            return false;

        for (var current = visual;
             current is not null && !ReferenceEquals(current, this);
             current = current.GetVisualParent())
            if (current is MyIconButton || ReferenceEquals(current, buttonStack))
                return true;

        return false;
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

    #endregion

    #region 交互能力

    private bool IsInteractiveItem =>
        Type is CheckType.RadioBox or CheckType.CheckBox or CheckType.Clickable
        || Click is not null
        || Command is not null
        || CheckCommand is not null
        || HasCustomEvent;

    private void UpdateInteractivity()
    {
        var interactive = IsEnabled && IsVisible && IsHitTestVisible && IsInteractiveItem;

        Focusable = interactive;
        IsTabStop = interactive;
    }

    /// <summary>控件上是否配置了自定义事件（附加事件类型或事件集合）。</summary>
    private bool HasCustomEvent =>
        CustomEventService.GetEventType(this) != EventType.None
        || GetValue(CustomEventService.EventsProperty) is { Events.Count: > 0 };

    #endregion
}
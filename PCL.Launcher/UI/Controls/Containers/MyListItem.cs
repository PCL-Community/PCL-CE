using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using PCL.Core.App;
using PCL.UI.Controls.Common;
using static PCL.Animations;
using Path = Avalonia.Controls.Shapes.Path;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     列表项：标题 + 副文本 + 标签 + 图标，可切换为单选、复选或纯点击三种交互。
///     悬停/按下颜色、背景缩放与勾选条缩放由 <c>PCL.Animations</c> 驱动；
///     标签支持本地化资源键与现成文本两种来源，右侧按钮在悬停时淡入并让出右边距。
/// </summary>
public class MyListItem : Grid, IMyRadio
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    public delegate void LogoClickEventHandler(object? sender, PointerReleasedEventArgs e);

    private string? _stateLast;

    /// <summary>是否启用鼠标悬停动画；逐项进入动画期间由 MyPageLeft/MyPageRight 临时关闭。</summary>
    public bool isMouseOverAnimationEnabled = true;

    #region 构造

    public MyListItem()
    {
        ContainerTheme.AttachStyles(this);

        Height = 42d;
        ContainerTheme.BindDynamic(this, BackgroundProperty, "ColorBrushSemiTransparent");
        RenderTransformOrigin = RelativePoint.Center;
        ContainerTheme.BindDynamic(this, ForegroundProperty, "ColorBrush1");

        ColumnDefinitions = new ColumnDefinitions("2,0,4,Auto,*,4");
        RowDefinitions = new RowDefinitions("*,Auto,Auto,*");
        _columnCheck = ColumnDefinitions[0];
        _columnPaddingLeft = ColumnDefinitions[1];
        _columnLogo = ColumnDefinitions[2];
        _columnPaddingRight = ColumnDefinitions[5];

        LabTitle = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            IsHitTestVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 14d,
            Margin = new Thickness(4d, 0d, 0d, 0d)
        };
        LabTitle.Classes.Add("ListItemTitle");
        LabTitle.Bind(
            TextBlock.ForegroundProperty,
            new Binding(nameof(Foreground))
            {
                Source = this
            });
        LabTitle.Bind(
            TextBlock.FontSizeProperty,
            new Binding(nameof(FontSize))
            {
                Source = this
            });
        SetRow(LabTitle, 1);
        SetColumn(LabTitle, 3);
        SetColumnSpan(LabTitle, 2);
        Children.Add(LabTitle);

        SizeChanged += (_, _) => OnSizeChanged();
        PointerReleased += Button_MouseUp;
        PointerPressed += Button_MouseDown;
        PointerExited += Button_MouseLeave;
        PointerEntered += (_, _) => RefreshColor(this, EventArgs.Empty);
        Loaded += (_, _) => MyListItem_Loaded();
    }

    #endregion

    public object? tag { get; set; }

    public event IMyRadio.CheckEventHandler? Check;
    public event IMyRadio.ChangedEventHandler? Changed;

    public event ClickEventHandler? Click;
    public event LogoClickEventHandler? LogoClick;

    #region 标签

    private void SetTags(object? value)
    {
        var list = value switch
        {
            string str => [.. str.Split('|')],
            IEnumerable<string> strings => [.. strings],
            _ => new List<string>()
        };
        TagPanelHelper.FillLocalized(
            PanTags,
            list,
            ContainerTheme.TextSecondary(this));
    }

    #endregion

    #region 自定义属性

    /// <summary>唯一编号，用于区分同名动画组。</summary>
    public readonly int Uuid = LauncherCommon.GetUuid();

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
        SvgIconProperty.Changed.AddClassHandler<MyListItem>((item, e) =>
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

    /// <summary>是否启用缩放动画；影响 RectBack 圆角。</summary>
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

    /// <summary>
    ///     右边距的最小值。在存在右侧按钮时，右边距会被自动设置为 5 + 按钮数 * 25。
    /// </summary>
    public int MinPaddingRight { get; set; } = 4;

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

    // 勾选选项
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

    // 勾选状态
    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false, value != Checked); // 仅在值发生变化时触发动画 (#4596)
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
        set => TagPanelHelper.FillPlain(PanTags, value, ContainerTheme.TextSecondary(this));
    }

    #endregion

    #region 后加载控件

    // 指向背景
    private Border? _rectBack;

    public Border RectBack
    {
        get
        {
            if (_rectBack is not null)
                return _rectBack;

            var rect = new Border
            {
                CornerRadius = new CornerRadius(IsScaleAnimationEnabled || Height > 40d ? 6d : 0d),
                RenderTransform = IsScaleAnimationEnabled ? new ScaleTransform(0.8d, 0.8d) : null,
                RenderTransformOrigin = RelativePoint.Center,
                BorderThickness = new Thickness(1d),
                IsHitTestVisible = false,
                Opacity = 0d,
                Background = ContainerTheme.AccentLight(this),
                BorderBrush = ContainerTheme.ItemPressed(this)
            };
            rect.Classes.Add("ListItemBack");
            SetColumnSpan(rect, 999);
            SetRowSpan(rect, 999);
            Children.Insert(0, rect);
            _rectBack = rect;
            return _rectBack;
        }
    }

    // 按钮
    public Control? buttonStack;

    // 图标
    public Control? pathLogo;

    // 勾选条
    public Border? rectCheck;

    private StackPanel? _panTags;

    /// <summary>Tags 的存放 StackPanel。</summary>
    public StackPanel PanTags
    {
        get
        {
            if (_panTags is not null)
                return _panTags;

            var stack = new StackPanel
            {
                IsHitTestVisible = false,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3.5d, 0d, -3d, 0d)
            };
            SetColumn(stack, 3);
            SetRow(stack, 2);
            Children.Add(stack);
            _panTags = stack;
            return _panTags;
        }
    }

    private TextBlock? _labInfo;

    /// <summary>副文本控件。</summary>
    public TextBlock LabInfo
    {
        get
        {
            if (_labInfo is not null)
                return _labInfo;

            var lab = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                IsHitTestVisible = false,
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsVisible = false,
                FontSize = 12d,
                Margin = new Thickness(4d, 0d, 0d, 0d),
                Opacity = 0.6d
            };
            ContainerTheme.BindDynamic(lab, TextBlock.ForegroundProperty, "ColorBrushGray3");
            SetColumn(lab, 4);
            SetRow(lab, 2);
            Children.Add(lab);
            _labInfo = lab;
            return _labInfo;
        }
    }

    /// <summary>根 Grid（列表项自身）。</summary>
    public Grid PanBack => this;

    private readonly ColumnDefinition _columnCheck;
    private readonly ColumnDefinition _columnPaddingLeft;
    private readonly ColumnDefinition _columnLogo;
    private readonly ColumnDefinition _columnPaddingRight;

    public TextBlock LabTitle { get; }

    [Content] public InlineCollection Inlines => LabTitle.Inlines!;

    #endregion

    #region 颜色与尺寸

    /// <summary>悬停/按下/离开时的颜色、背景缩放与右侧按钮过渡（120/180 ms）。</summary>
    public void RefreshColor(object? sender, EventArgs e)
    {
        // 菜单虚拟化检测
        if (ContentHandler is not null)
        {
            ContentHandler.Invoke(this, e);
            ContentHandler = null;
        }

        // 判断当前颜色
        string stateNew;
        int time;
        if (_isMouseDown && !(Type == CheckType.RadioBox && Checked))
        {
            stateNew = "MouseDown";
            time = 120;
        }
        else if (IsPointerOver && isMouseOverAnimationEnabled)
        {
            stateNew = "MouseOver";
            time = 120;
        }
        else
        {
            stateNew = "Idle";
            time = 180;
        }

        if ((_stateLast ?? string.Empty) == stateNew)
            return;
        _stateLast = stateNew;

        var animationKey = "ListItem Color " + Uuid;

        // 动画关闭、控件未加载或页面切换中：直接落到终值
        if (!ContainerAnimation.CanAnimate(this))
        {
            ApplyColorStateInstant(stateNew);
            AniStop(animationKey);
            return;
        }

        // 有动画：同名的上一组会在 AniStart 时被丢弃，所以快速移入移出不会叠加
        var ani = new List<AniData>();
        if (stateNew is "MouseOver" or "MouseDown")
        {
            if (buttonStack is not null)
            {
                ani.Add(
                    AaOpacity(
                        buttonStack,
                        1d - buttonStack.Opacity,
                        (int)Math.Round(time * 0.7d),
                        (int)Math.Round(time * 0.3d)));
                ani.Add(
                    AaDouble(
                        delta => _columnPaddingRight.Width =
                            new GridLength(Math.Max(0d, _columnPaddingRight.Width.Value + delta)),
                        Math.Max(MinPaddingRight, 5d + (Buttons?.Count() ?? 0) * 25d)
                        - _columnPaddingRight.Width.Value,
                        (int)Math.Round(time * 0.3d),
                        (int)Math.Round(time * 0.7d)));
            }

            var back = RectBack; // 鼠标移入时才实例化
            ani.Add(
                AaColor(
                    back,
                    Border.BackgroundProperty,
                    ContainerAnimation.ColorOf(
                        stateNew == "MouseDown"
                            ? ContainerTheme.ItemPressed(this)
                            : ContainerTheme.ItemHover(this)),
                    time));
            ani.Add(AaOpacity(back, 1d - back.Opacity, time, ease: new AniEaseOutFluent()));

            if (IsScaleAnimationEnabled)
            {
                var backScale = ContainerAnimation.EnsureScale(back, 0.8d, 0.8d);
                var selfScale = ContainerAnimation.EnsureScale(this);
                ani.Add(
                    AaScaleTransform(
                        back,
                        1d - backScale.ScaleX,
                        (int)Math.Round(time * 1.6d),
                        ease: new AniEaseOutFluent()));
                if (_isMouseDown)
                    ani.Add(
                        AaScaleTransform(
                            this,
                            0.98d - selfScale.ScaleX,
                            (int)Math.Round(time * 0.9d),
                            ease: new AniEaseOutFluent()));
                else
                    ani.Add(
                        AaScaleTransform(
                            this,
                            1d - selfScale.ScaleX,
                            (int)Math.Round(time * 1.2d),
                            ease: new AniEaseOutFluent()));
            }
        }
        else
        {
            if (buttonStack is not null)
            {
                ani.Add(AaOpacity(buttonStack, -buttonStack.Opacity, (int)Math.Round(time * 0.4d)));
                ani.Add(
                    AaDouble(
                        delta => _columnPaddingRight.Width =
                            new GridLength(Math.Max(0d, _columnPaddingRight.Width.Value + delta)),
                        MinPaddingRight - _columnPaddingRight.Width.Value,
                        (int)Math.Round(time * 0.4d)));
            }

            if (_rectBack is not null)
            {
                ani.Add(AaOpacity(_rectBack, -_rectBack.Opacity, time));
                if (IsScaleAnimationEnabled)
                {
                    var backScale = ContainerAnimation.EnsureScale(_rectBack);
                    var selfScale = ContainerAnimation.EnsureScale(this);
                    ani.Add(
                        AaColor(
                            _rectBack,
                            Border.BackgroundProperty,
                            ContainerAnimation.ColorOf(ContainerTheme.AccentLight(this)),
                            time));
                    ani.Add(
                        AaScaleTransform(
                            this,
                            1d - selfScale.ScaleX,
                            time * 3,
                            ease: new AniEaseOutFluent()));
                    ani.Add(
                        AaScaleTransform(
                            _rectBack,
                            0.996d - backScale.ScaleX,
                            time,
                            ease: new AniEaseOutFluent()));
                    // 收尾回落到 0.75，下次移入时从缩小状态展开
                    ani.Add(AaScaleTransform(_rectBack, -0.246d, 1, after: true));
                }
            }
        }

        AniStart(ani, animationKey);
    }

    /// <summary>无动画路径：直接写入悬停/按下/离开的终态。</summary>
    private void ApplyColorStateInstant(string stateNew)
    {
        if (stateNew == "Idle")
        {
            buttonStack?.Opacity = 0d;
            _columnPaddingRight.Width = new GridLength(MinPaddingRight);

            var idleScale = ContainerAnimation.EnsureScale(this);
            idleScale.ScaleX = 1d;
            idleScale.ScaleY = 1d;
            if (_rectBack is not null)
            {
                if (IsScaleAnimationEnabled)
                {
                    var scale = ContainerAnimation.EnsureScale(_rectBack, 0.75d, 0.75d);
                    scale.ScaleX = 0.75d;
                    scale.ScaleY = 0.75d;
                }

                _rectBack.Background = ContainerTheme.ItemHover(this);
                _rectBack.Opacity = 0d;
            }

            return;
        }

        if (buttonStack is not null)
        {
            buttonStack.Opacity = 1d;
            _columnPaddingRight.Width = new GridLength(
                Math.Max(MinPaddingRight, 5d + (Buttons?.Count() ?? 0) * 25d));
        }

        var back = RectBack; // 鼠标移入时才实例化
        back.Background = stateNew == "MouseDown"
            ? ContainerTheme.ItemPressed(this)
            : ContainerTheme.ItemHover(this);
        back.Opacity = 1d;
        var backScale = ContainerAnimation.EnsureScale(back);
        backScale.ScaleX = 1d;
        backScale.ScaleY = 1d;
        var selfScale = ContainerAnimation.EnsureScale(this);
        var targetScale = stateNew == "MouseDown" ? 0.98d : 1d;
        selfScale.ScaleX = targetScale;
        selfScale.ScaleY = targetScale;
    }

    private void MyListItem_Loaded()
    {
        // 仅在选中时重设前景色，未选中沿用主题绑定
        if (Checked)
            ApplyCheckedForeground();
        _columnPaddingRight.Width = new GridLength(MinPaddingRight);
    }

    private void OnSizeChanged()
    {
        var logo = Logo;
        var hasLogo = IsUsingSvgIcon || !string.IsNullOrEmpty(logo);
        var isBitmapLogo = IsBitmapLogo;
        _columnCheck.Width = new GridLength(
            Type is CheckType.None or CheckType.Clickable
                ? Height < 40d
                    ? 4d
                    : 2d
                : 6d);
        _columnLogo.Width = new GridLength((hasLogo ? 34d : 0d) + (Height < 40d ? 0d : 4d));

        pathLogo?.Margin = isBitmapLogo
            ? new Thickness(4d, 5d, 3d, 5d)
            : new Thickness(
                Height < 40d ? 6d : 8d,
                8d,
                Height < 40d ? 4d : 6d,
                8d);

        LabTitle.Margin = new Thickness(4d, 0d, 0d, Height < 40d ? 0d : 2d);
    }

    #endregion

    #region 属性应用

    private void OnTitleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        LabTitle.Text = (e.NewValue as string ?? string.Empty).Replace("\r", "").Replace("\n", "");
    }

    private void OnInfoChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var value = e.NewValue as string;
        var lab = LabInfo;
        lab.Text = value ?? string.Empty;
        lab.IsVisible = !string.IsNullOrEmpty(value);
    }

    private void ApplyPaddingLeft(int value)
    {
        _columnPaddingLeft.Width = new GridLength(value);
    }

    private void ApplyScaleAnimationEnabled(bool value)
    {
        _rectBack?.CornerRadius = new CornerRadius(value ? 6d : 0d);
    }

    #endregion

    #region Logo

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(SvgIcon);

    private void UpdateLogo(string? logo)
    {
        // 删除旧 Logo
        if (pathLogo is not null)
            Children.Remove(pathLogo);
        pathLogo = null;

        // 添加新 Logo
        if (IsUsingSvgIcon)
        {
            var icon = new SvgIconControl
            {
                Tag = this,
                IsHitTestVisible = LogoClickable,
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
                // 网络图片：按原比例缩放，下载与缓存由 MyImage 负责
                pathLogo = new MyImage
                {
                    Tag = this,
                    IsHitTestVisible = LogoClickable,
                    Source = logo,
                    CornerRadius = LogoCornerRadius,
                    Stretch = Stretch.Uniform,
                    RenderTransformOrigin = RelativePoint.Center,
                    RenderTransform = new ScaleTransform(LogoScale, LogoScale),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            else if (IsBitmapPath(logo))
                // 本地位图：拉伸填满单元格；皮肤头像与披风缓存用最近邻，避免像素被平滑
                pathLogo = new ListItemLogoImage
                {
                    Tag = this,
                    IsHitTestVisible = LogoClickable,
                    Source = logo,
                    CornerRadius = LogoCornerRadius,
                    UseNearestNeighbor = IsAvatarCachePath(logo),
                    RenderTransformOrigin = RelativePoint.Center,
                    RenderTransform = new ScaleTransform(LogoScale, LogoScale),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            else
                // 矢量图
                try
                {
                    var path = new Path
                    {
                        Tag = this,
                        IsHitTestVisible = LogoClickable,
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
            OnSizeChanged(); // 设置边距
            Children.Add(pathLogo);
            // 图标的点击事件
            if (LogoClickable)
            {
                pathLogo.PointerExited += (_, _) => _isLogoDown = false;
                pathLogo.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(pathLogo).Properties.IsLeftButtonPressed)
                        _isLogoDown = true;
                };
                pathLogo.PointerReleased += (_, e) =>
                {
                    if (!_isLogoDown)
                        return;
                    _isLogoDown = false;
                    if (e.InitialPressMouseButton != MouseButton.Left)
                        return;

                    LogoClick?.Invoke(this, e);
                    // 图标点击不冒泡到整行的点击处理，自定义事件在这里补发
                    RaiseCustomEventOnce(e);
                };
            }
        }

        // 改变行距
        var hasLogo = IsUsingSvgIcon || !string.IsNullOrEmpty(logo);
        _columnLogo.Width = new GridLength((hasLogo ? 34d : 0d) + (Height < 40d ? 0d : 4d));
    }

    private static bool IsBitmapPath(string logo)
    {
        return logo.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || logo.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || logo.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>当前 Logo 是否为位图文件（png/jpg/webp）。</summary>
    private bool IsBitmapLogo => !IsUsingSvgIcon && IsBitmapPath(Logo);

    /// <summary>
    ///     Logo 是否位于皮肤头像或披风缓存目录。
    ///     这两类图是像素画，放大或缩小时需要使用最近邻采样。
    ///     TODO(FIX-C-04): 皮肤页面负责生成头像缓存（56×56 最近邻合成），这里只消费已存在的缓存文件。
    /// </summary>
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

    /// <summary>控件上是否配置了自定义事件（附加事件类型或事件集合）。</summary>
    private bool HasCustomEvent =>
        CustomEventService.GetEventType(this) != EventType.None
        || GetValue(CustomEventService.EventsProperty) is { Events.Count: > 0 };

    /// <summary>配置了自定义事件时派发一次，并标记事件已处理。</summary>
    private void RaiseCustomEventOnce(PointerEventArgs e)
    {
        if (e.Handled || !HasCustomEvent)
            return;

        CustomEventService.Raise(this);
        e.Handled = true;
    }

    private void ApplyLogoScale()
    {
        if (pathLogo is not null)
        {
            var scale = IsUsingSvgIcon ? 1d : LogoScale;
            pathLogo.RenderTransform = new ScaleTransform(scale, scale);
            pathLogo.RenderTransformOrigin = RelativePoint.Center;
        }
    }

    /// <summary>该 Logo 是否可用点击触发事件。需要在 Logo 属性之前设置。</summary>
    public bool LogoClickable { get; set; }

    private bool _isLogoDown;

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

    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;

    /// <summary>手动设置 Checked 属性。</summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
    public void SetChecked(bool value, bool user, bool anime)
    {
        try
        {
            // 自定义属性基础
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
                        SetCheckedValue(rawValue);
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
                        SetCheckedValue(rawValue);
                        return;
                    }
                }
            }

            if (value)
            {
                var checkEventArgs = new RouteEventArgs(user);
                Check?.Invoke(this, checkEventArgs);
                if (checkEventArgs.handled)
                    return;
            }

            // 保证只有一个单选 ListItem 选中
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
        if (Type != CheckType.RadioBox)
            return;
        if (Parent is not Panel panel)
            return;

        var radioBoxList = new List<MyListItem>();
        var checkedCount = 0;
        // 收集控件列表与选中个数
        foreach (var control in panel.Children.Select(MyVirtualizingElement.TryInit))
        {
            if (control is not MyListItem { Type: CheckType.RadioBox } listItem)
                continue;

            radioBoxList.Add(listItem);
            if (listItem.Checked)
                checkedCount += 1;
        }

        // 判断选中情况
        switch (checkedCount)
        {
            case 0:
                // 没有任何单选框被选中，选择第一个
                if (radioBoxList.Count > 0)
                    radioBoxList[0].Checked = true;
                break;
            case > 1 when Checked:
                // 选中项目多于 1 个且本控件选中：取消其他所有控件的选中
                foreach (var control in radioBoxList.Where(control =>
                             control.Checked && !ReferenceEquals(control, this)))
                    control.Checked = false;
                break;
            case > 1:
            {
                // 本控件未选中：只保留第一个选中的控件
                var firstChecked = false;
                foreach (var control in radioBoxList.Where(control => control.Checked))
                    if (firstChecked)
                        control.Checked = false; // 修改 Checked 会自动触发 Change 事件
                    else
                        firstChecked = true;

                break;
            }
        }
    }

    /// <summary>选中/取消时的左侧竖条弹性缩放与前景色过渡（300/120/200 ms）。</summary>
    private void RefreshCheckedVisual(bool value, bool anime)
    {
        const double customHeight = 20d; // 修改左侧神秘动画条的高度
        var animationKey = "MyListItem Checked " + Uuid;

        if (anime && ContainerAnimation.CanAnimate(this))
        {
            var anim = new List<AniData>();
            if (value)
            {
                // 由无变有：固定高度、居中对齐，再让 ScaleY 从 0 弹到 1
                if (rectCheck is not null)
                {
                    rectCheck.Height = customHeight;
                    rectCheck.VerticalAlignment = VerticalAlignment.Center;
                    rectCheck.Margin = new Thickness(-1d, 0d, 0d, 0d);
                    rectCheck.Opacity = 1d;
                    rectCheck.RenderTransformOrigin = RelativePoint.Center;
                    var scale = new ScaleTransform(1d, 0d);
                    rectCheck.RenderTransform = scale;

                    anim.Add(
                        AaDouble(
                            delta => scale.ScaleY = Math.Max(0d, scale.ScaleY + delta),
                            1d - scale.ScaleY,
                            300,
                            ease: new AniEaseOutBack(AniEasePower.Weak)));
                }

                anim.Add(
                    AaColor(
                        this,
                        ForegroundProperty,
                        ContainerAnimation.ColorOf(CheckedForeground),
                        200));
            }
            else
            {
                // 由有变无：ScaleY 缩到 0，同时淡出竖条
                if (rectCheck is not null)
                {
                    var scale = ContainerAnimation.EnsureScale(rectCheck);
                    anim.Add(
                        AaDouble(
                            delta => scale.ScaleY = Math.Max(0d, scale.ScaleY + delta),
                            -scale.ScaleY,
                            120,
                            ease: new AniEaseInFluent(AniEasePower.Weak)));
                    anim.Add(AaOpacity(rectCheck, -rectCheck.Opacity, 70, 40));
                }

                anim.Add(
                    AaColor(
                        this,
                        ForegroundProperty,
                        ContainerAnimation.ColorOf(ContainerTheme.Text(this)),
                        120));
            }

            // 颜色是逐帧相对插值，收尾时统一恢复：选中写强调色，未选中恢复主题动态绑定
            anim.Add(AaCode(ApplyCheckedForeground, after: true));
            AniStart(anim, animationKey);
            return;
        }

        AniStop(animationKey);

        if (rectCheck is not null)
        {
            rectCheck.Height = value ? customHeight : 0d;
            rectCheck.Margin = new Thickness(-1d, 0d, 0d, 0d);
            rectCheck.Opacity = value ? 1d : 0d;
            rectCheck.VerticalAlignment = VerticalAlignment.Center;
            rectCheck.RenderTransform = null;
        }

        ApplyCheckedForeground();
    }

    /// <summary>
    ///     选中项的前景色：常规行（高度 40 及以上）用强调色，紧凑行用浅一档的强调色。
    /// </summary>
    private IBrush CheckedForeground =>
        Height < 40d
            ? ContainerTheme.CheckedTextWeak(this)
            : ContainerTheme.CheckedTextStrong(this);

    private void ApplyCheckedForeground()
    {
        // 未选中时恢复动态资源绑定，保证主题切换后文字颜色继续跟随页面。
        if (Checked)
            Foreground = CheckedForeground;
        else
            ContainerTheme.BindDynamic(this, ForegroundProperty, "ColorBrush1");
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
        // 切换左栏大小
        _columnCheck.Width = new GridLength(
            value is CheckType.None or CheckType.Clickable
                ? Height < 40d
                    ? 4d
                    : 2d
                : 6d);
        // 切换竖条控件
        if (value is CheckType.None or CheckType.Clickable)
        {
            // 移除竖条控件
            if (rectCheck is not null)
            {
                Children.Remove(rectCheck);
                rectCheck = null;
            }

            SetChecked(false, false, false);
        }
        // 添加竖条控件
        else if (rectCheck is null)
        {
            var bar = new Border
            {
                Width = 5d,
                Height = Checked ? double.NaN : 0d,
                CornerRadius = new CornerRadius(2d),
                VerticalAlignment = Checked ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = Checked
                    ? new Thickness(-1d, 6d, 0d, 6d)
                    : new Thickness(-1d, 0d, 0d, 0d),
                Background = ContainerTheme.Accent(this)
            };
            bar.Classes.Add("ListItemCheck");
            SetRowSpan(bar, 4);
            Children.Add(bar);
            rectCheck = bar;
        }
    }

    private void SetButtons(IEnumerable<MyIconButton>? value)
    {
        // 没有特殊按钮，移除原 Stack
        if (buttonStack is not null)
        {
            Children.Remove(buttonStack);
            buttonStack = null;
        }

        if (value is null)
            return;

        var buttons = value.ToList();
        switch (buttons.Count)
        {
            case 0:
                break;
            case 1:
            {
                // 只有一个按钮
                var button = buttons[0];
                ConfigureButton(button);
                SetColumnSpan(button, 10);
                SetRowSpan(button, 10);
                Children.Add(button);
                buttonStack = button;
                break;
            }
            default:
            {
                // 有复数按钮，使用 StackPanel
                var stack = new StackPanel
                {
                    Opacity = 0d,
                    Margin = new Thickness(0d, 0d, 5d, 0d),
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                SetColumnSpan(stack, 10);
                SetRowSpan(stack, 10);
                foreach (var button in buttons)
                {
                    ConfigureButton(button);
                    stack.Children.Add(button);
                }

                Children.Add(stack);
                buttonStack = stack;
                break;
            }
        }
    }

    private static void ConfigureButton(MyIconButton button)
    {
        if (double.IsNaN(button.Height))
            button.Height = 25d;
        if (double.IsNaN(button.Width))
            button.Width = 25d;
        button.Opacity = 0d;
        button.Margin = new Thickness(0d, 0d, 5d, 0d);
        button.HorizontalAlignment = HorizontalAlignment.Right;
        button.VerticalAlignment = VerticalAlignment.Center;
    }

    #endregion

    #region 点击

    private bool _isMouseDown;

    private void Button_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        if (!_isMouseDown)
            return;

        _isMouseDown = false;
        buttonStack?.IsHitTestVisible = true;

        Click?.Invoke(this, e);
        ExecuteCommand();
        RaiseCustomEventOnce(e);

        if (!e.Handled)
            switch (Type)
            {
                case CheckType.Clickable:
                {
                    LauncherCommon.Log("[Control] 按下单击列表项：" + Title);
                    break;
                }

                case CheckType.RadioBox:
                {
                    LauncherCommon.Log("[Control] 按下单选列表项：" + Title);
                    if (!Checked)
                        SetChecked(true, true, true);
                    break;
                }

                case CheckType.CheckBox:
                {
                    LauncherCommon.Log("[Control] 按下复选列表项（" + !Checked + "）：" + Title);
                    SetChecked(!Checked, true, true);
                    break;
                }

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
        // 落在图标或右侧按钮上的按下不算整行点击
        if (ReferenceEquals(e.Source, this) && Type != CheckType.None)
        {
            _isMouseDown = true;
            buttonStack?.IsHitTestVisible = false;
        }

        RefreshColor(this, EventArgs.Empty);
    }

    private void Button_MouseLeave(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        buttonStack?.IsHitTestVisible = true;
        RefreshColor(this, EventArgs.Empty);
    }

    private bool _isCommandRunning;

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

    #region 菜单与事件

    /// <summary>菜单与按钮绑定：首次显示时由菜单系统填充右侧按钮。</summary>
    public Action<MyListItem, EventArgs>? ContentHandler { get; set; }

    public override string ToString()
    {
        return Title;
    }

    #endregion

    #region 生命周期

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CheckedProperty && !_isSettingChecked)
        {
            EnsureSingleChecked();
            RefreshCheckedVisual(change.NewValue is true, true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        if (Checked)
            ApplyCheckedForeground();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        ApplyCheckedForeground();
        if (_rectBack is { Opacity: <= 0d })
            _rectBack.Background = ContainerTheme.ItemHover(this);
    }

    #endregion
}
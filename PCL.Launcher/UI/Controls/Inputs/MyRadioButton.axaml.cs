using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PCL.UI.Controls.Common;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     单选按钮：圆角药丸 + 可选图标 + 文本，支持 <see cref="ColorState.White" /> /
///     <see cref="ColorState.Highlight" /> 两种配色、<see cref="GroupName" /> 分组互斥与
///     <see cref="Check" /> 事件。
///     勾选、悬停、按下与禁用状态经 <c>PCL.Animations</c> 过渡；动画关闭时直接落到终值。
/// </summary>
public partial class MyRadioButton : UserControl
{
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, RouteEventArgs e);

    public enum ColorState
    {
        White,
        Highlight
    }

    private const int AnimationTimeOfMouseIn = 90;
    private const int AnimationTimeOfMouseOut = 150;
    private const int AnimationTimeOfCheck = 120;
    private const int AnimationTimeOfPress = 60;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<Thickness> LabTextMarginProperty =
        AvaloniaProperty.Register<MyRadioButton, Thickness>(
            nameof(LabTextMargin),
            new Thickness(8d, 0d, 12d, 0d));

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioButton, bool>(
            nameof(Checked),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(GroupName));

    public static readonly StyledProperty<ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyRadioButton, ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyRadioButton, object?>(nameof(CheckCommandParameter));

    private static readonly Dictionary<object, List<WeakReference<MyRadioButton>>> GroupRegistry =
        new(GroupKeyComparer.Instance);

    private static readonly Lock GroupRegistryLock = new();

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private bool _hasLegacyLogo;
    private bool _isExecutingCheckCommand;
    private bool _isMouseDown;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private string _logo = string.Empty;
    private string _svgIcon = string.Empty;

    public MyRadioButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

        Loaded += (_, _) =>
        {
            LabTextElement.Text = Text;
            UnregisterGroup(GroupName);
            RegisterGroup(GroupName);
            RefreshColor(false);
        };

        Unloaded += (_, _) =>
        {
            UnregisterGroup(GroupName);
            StopAnimations();
        };

        PointerPressed += Radiobox_MouseDown;
        PointerReleased += Radiobox_MouseUp;
        PointerExited += Radiobox_MouseLeave;
        PointerEntered += Radiobox_MouseEnter;
    }

    // 自定义属性

    public string Logo
    {
        get => _logo;
        set
        {
            _logo = value ?? string.Empty;
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(_logo);
            if (ShapeLogoElement is null)
                return;
            ShapeLogoElement.Data = _hasLegacyLogo ? Geometry.Parse(_logo) : null;
            UpdateIconVisibility();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    }

    public string SvgIcon
    {
        get => _svgIcon;
        set
        {
            value ??= string.Empty;
            if (value == _svgIcon)
                return;
            _svgIcon = value;
            if (ShapeSvgIconElement is null)
                return;
            ShapeSvgIconElement.Icon = _svgIcon;
            ApplyLogoScale();
            UpdateIconVisibility();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    }

    private bool IsUsingSvgIcon => !string.IsNullOrWhiteSpace(_svgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1d : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false, true);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
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

    public InlineCollection Inlines => LabText.Inlines!;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Thickness LabTextMargin
    {
        get => GetValue(LabTextMarginProperty);
        set => SetValue(LabTextMarginProperty, value);
    }

    public ColorState ColorType
    {
        get;
        set
        {
            field = value;
            RefreshColor();
        }
    } = ColorState.White;

    private string BackgroundKey => InputAnimation.Key("MyRadioButton Background", Uuid);
    private string TextColorKey => InputAnimation.Key("MyRadioButton TextColor", Uuid);
    private string LogoColorKey => InputAnimation.Key("MyRadioButton LogoColor", Uuid);
    private string SvgColorKey => InputAnimation.Key("MyRadioButton SvgColor", Uuid);

    /// <summary>
    ///     文本宿主，供内联文本与排版微调使用。
    /// </summary>
    public TextBlock LabText => LabTextElement;

    private void StopAnimations()
    {
        InputAnimation.Stop(BackgroundKey, TextColorKey, LogoColorKey, SvgColorKey);
    }

    public event CheckEventHandler? Check;

    public event PreviewClickEventHandler? PreviewClick;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="raiseByMouse">是否由用户引发。</param>
    /// <param name="anime">是否播放配色过渡；批量取消选中时传 <c>false</c> 直接落到终值。</param>
    public void SetChecked(bool value, bool raiseByMouse, bool anime)
    {
        try
        {
            // 自定义属性基础

            var isChanged = Checked != value;
            _isUserInteraction = raiseByMouse;
            try
            {
                if (isChanged)
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

                // 保证只有一个单选框选中

                if (Parent is null && string.IsNullOrEmpty(GroupName))
                    return;
                EnsureSingleChecked();

                // 更改外观

                if (!isChanged)
                    return;
                RefreshColor(anime);

                // 触发事件
                if (Checked)
                    Check?.Invoke(this, raiseByMouse);
                CustomEventService.Raise(this);
            }
            finally
            {
                _isUserInteraction = false;
            }
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "单选按钮勾选改变错误");
        }
    }

    private void EnsureSingleChecked()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            if (!Checked)
                return;
            foreach (var radioButton in GetGroupMembers(GroupName)
                         .Where(radioButton => !ReferenceEquals(radioButton, this)))
                radioButton.SetChecked(false, false, false);
            return;
        }

        if (Parent is not Panel parentPanel)
            return;

        var radioButtonList = new List<MyRadioButton>();
        var checkedCount = 0;

        foreach (var control in parentPanel.Children)
            if (control is MyRadioButton radioButton)
            {
                radioButtonList.Add(radioButton);
                if (radioButton.Checked)
                    checkedCount += 1;
            }

        switch (checkedCount)
        {
            case 0:
                if (radioButtonList.Count > 0)
                    radioButtonList[0].Checked = true;
                break;
            case > 1 when Checked:
                foreach (var radioButton in radioButtonList.Where(radioButton =>
                             radioButton.Checked && !ReferenceEquals(radioButton, this)))
                    radioButton.Checked = false;
                break;
            case > 1:
            {
                var firstChecked = false;
                foreach (var radioButton in
                         radioButtonList.Where(radioButton => radioButton.Checked))
                    if (firstChecked)
                        radioButton.Checked = false;
                    else
                        firstChecked = true;

                break;
            }
        }
    }

    private void RegisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName, out var members))
            {
                members = [];
                GroupRegistry[groupName] = members;
            }

            members.RemoveAll(reference =>
                !reference.TryGetTarget(out var target) || ReferenceEquals(target, this));
            members.Add(new WeakReference<MyRadioButton>(this));
        }

        if (Checked)
            EnsureSingleChecked();
    }

    private void UnregisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName, out var members))
                return;
            members.RemoveAll(reference =>
                !reference.TryGetTarget(out var target) || ReferenceEquals(target, this));
            if (members.Count == 0)
                GroupRegistry.Remove(groupName);
        }
    }

    private static List<MyRadioButton> GetGroupMembers(string groupName)
    {
        var radioButtons = new List<MyRadioButton>();
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName, out var members))
                return radioButtons;
            members.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (members.Count == 0)
            {
                GroupRegistry.Remove(groupName);
                return radioButtons;
            }

            foreach (var reference in members)
                if (reference.TryGetTarget(out var target))
                    radioButtons.Add(target);
        }

        return radioButtons;
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

    // 点击事件

    private void Radiobox_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;
        if (Checked)
            return;
        if (!_isMouseDown)
            return;

        _isMouseDown = false;
        var args = new RouteEventArgs(true);
        PreviewClick?.Invoke(this, args);
        if (args.handled)
            return;
        SetChecked(true, true, true);
    }

    private void Radiobox_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (Checked)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _isMouseDown = true;
        RefreshColor();
    }

    private void Radiobox_MouseLeave(object? sender, PointerEventArgs e)
    {
        _isMouseDown = false;
        RefreshColor();
    }

    private void Radiobox_MouseEnter(object? sender, PointerEventArgs e)
    {
        RefreshColor();
    }

    // 外观

    private void ApplyLogoScale()
    {
        LogoHostElement?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void UpdateIconVisibility()
    {
        if (ShapeLogoElement is null || ShapeSvgIconElement is null)
            return;

        ShapeSvgIconElement.IsVisible = IsUsingSvgIcon;
        ShapeLogoElement.IsVisible = !IsUsingSvgIcon && _hasLegacyLogo;
    }

    private void RefreshLogoHostVisibility()
    {
        if (LogoHostElement is null || LabTextElement is null)
            return;

        LogoHostElement.Width = HasAnyIcon ? 16d : 0d;
        LogoHostElement.Height = 16d;
        LogoHostElement.Margin = new Thickness(12d, 0d, 0d, 0d);
        LabTextElement.Margin = LabTextMargin;
    }

    /// <summary>
    ///     供主题切换或外部主题服务调用，重新计算当前状态的配色。
    /// </summary>
    public void RefreshMyRadioButtonColor()
    {
        RefreshColor();
    }

    /// <summary>
    ///     重新计算当前状态的底色与前景色。
    /// </summary>
    /// <param name="animate">是否播放配色过渡；首次挂载或外部强制刷新时传 <c>false</c>。</param>
    private void RefreshColor(bool animate = true)
    {
        try
        {
            if (PanBackElement is null || LabTextElement is null)
                return;

            Opacity = IsEnabled ? 1d : 0.5d;

            Color background;
            Color foreground;
            int time;

            if (!IsEnabled)
            {
                background = Colors.Transparent;
                foreground = ToColor(InputTheme.TextSecondary(this));
                time = AnimationTimeOfMouseOut;
            }
            else if (ColorType == ColorState.White)
            {
                if (Checked)
                {
                    background = ToColor(InputTheme.Card(this));
                    foreground = ToColor(InputTheme.Accent(this));
                    time = AnimationTimeOfCheck;
                }
                else if (_isMouseDown)
                {
                    background = ToColor(InputTheme.WithAlpha(InputTheme.ItemChecked(this), 0x78));
                    foreground = Colors.White;
                    time = AnimationTimeOfPress;
                }
                else if (IsPointerOver)
                {
                    background = ToColor(InputTheme.WithAlpha(InputTheme.ItemChecked(this), 0x32));
                    foreground = Colors.White;
                    time = AnimationTimeOfMouseIn;
                }
                else
                {
                    background = Colors.Transparent;
                    foreground = Colors.White;
                    time = AnimationTimeOfMouseOut;
                }
            }
            else
            {
                if (Checked)
                {
                    background = ToColor(InputTheme.Accent(this));
                    foreground = Colors.White;
                    time = AnimationTimeOfCheck;
                }
                else if (_isMouseDown || IsPointerOver)
                {
                    background = ToColor(InputTheme.AccentLight(this));
                    foreground = ToColor(InputTheme.Accent(this));
                    time = AnimationTimeOfMouseIn;
                }
                else
                {
                    background = Colors.Transparent;
                    foreground = ToColor(InputTheme.Accent(this));
                    time = AnimationTimeOfMouseOut;
                }
            }

            var effective = animate && InputAnimation.CanAnimate(this);

            InputAnimation.ColorLiteral(
                PanBackElement,
                Border.BackgroundProperty,
                background,
                time,
                BackgroundKey,
                effective);
            InputAnimation.ColorLiteral(
                LabTextElement,
                TextBlock.ForegroundProperty,
                foreground,
                time,
                TextColorKey,
                effective);
            InputAnimation.ColorLiteral(
                ShapeLogoElement,
                Shape.FillProperty,
                foreground,
                time,
                LogoColorKey,
                effective);
            InputAnimation.ColorLiteral(
                ShapeSvgIconElement,
                SvgIconControl.IconBrushProperty,
                foreground,
                time,
                SvgColorKey,
                effective);
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "刷新单选按钮颜色出错");
        }
    }

    private static Color ToColor(IBrush? brush)
    {
        return brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
    }

    // 属性变更处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CheckedProperty)
        {
            if (!_isSettingChecked)
            {
                EnsureSingleChecked();
                RefreshColor();
            }

            if (_isUserInteraction)
                ExecuteCheckCommand();
        }
        else if (change.Property == GroupNameProperty)
        {
            if (!IsLoaded)
                return;
            UnregisterGroup(change.OldValue as string);
            RegisterGroup(change.NewValue as string);
        }
        else if (change.Property == TextProperty)
        {
            LabTextElement?.Text = change.NewValue as string ?? string.Empty;
        }
        else if (change.Property == LabTextMarginProperty)
        {
            if (LabTextElement is not null && change.NewValue is Thickness margin)
                LabTextElement.Margin = margin;
        }
        else if (change.Property == IsEnabledProperty)
        {
            RefreshColor();
        }
    }

    // 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ApplyLogoScale();
        UpdateIconVisibility();
        RefreshLogoHostVisibility();
        RefreshColor(false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        RefreshColor(false);
    }

    #region 分组

    private sealed class GroupKeyComparer : IEqualityComparer<object>
    {
        public static readonly GroupKeyComparer Instance = new();

        public new bool Equals(object? x, object? y)
        {
            return x is string groupNameX && y is string groupNameY
                ? string.Equals(groupNameX, groupNameY, StringComparison.Ordinal)
                : ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return obj is string groupName
                ? StringComparer.Ordinal.GetHashCode(groupName)
                : RuntimeHelpers.GetHashCode(obj);
        }
    }

    #endregion
}
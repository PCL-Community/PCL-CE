﻿using System.Collections;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.UI.Theme;

using PCL.Core.App.Localization;
namespace PCL;

[ContentProperty("Inlines")]
public partial class MyRadioButton
{
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, Common.RouteEventArgs e);

    public enum ColorState
    {
        White,
        Highlight
    }

    // 动画

    private const int animationTimeOfMouseIn = 90; // 鼠标指向动画长度
    private const int animationTimeOfMouseOut = 150; // 鼠标移出动画长度
    private const int animationTimeOfCheck = 120; // 勾选状态变更动画长度

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is MyRadioButton rb && rb.LabText is not null) rb.LabText.Text = (string)e.NewValue;
        }));

    private static readonly Dictionary<object, List<WeakReference<MyRadioButton>>> GroupRegistry =
        new(GroupKeyComparer.Instance);

    private static readonly object GroupRegistryLock = new();

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

    private bool _hasLegacyLogo;
    private bool isMouseDown;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;

    // 基础

    public int Uuid = Common.GetUuid();

    public MyRadioButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

        Loaded += (_, _) =>
        {
            LabText?.Text = (string)GetValue(TextProperty);

            ThemeService.ColorModeChanged += OnColorModeChanged;
            ThemeService.ColorThemeChanged += OnColorThemeChanged;
        };

        Unloaded += (_, _) =>
        {
            ThemeService.ColorModeChanged -= OnColorModeChanged;
            ThemeService.ColorThemeChanged -= OnColorThemeChanged;
        };

        MouseLeftButtonUp += (_, _) => Radiobox_MouseUp();
        MouseLeftButtonDown += (_, _) => Radiobox_MouseDown();
        MouseLeave += (_, _) => Radiobox_MouseLeave();
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        Loaded += RefreshColor;
        Loaded += (_, _) => RegisterGroup(GroupName);
        Unloaded += (_, _) => UnregisterGroup(GroupName);
    }

    private void OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        Dispatcher.Invoke(RefreshMyRadioButtonColor);
    }

    private void OnColorThemeChanged(ColorTheme theme)
    {
        Dispatcher.Invoke(RefreshMyRadioButtonColor);
    }

    // 自定义属性

    public string Logo
    {
        get => ShapeLogo.Data?.ToString() ?? string.Empty;
        set
        {
            if (ShapeLogo is null) return;
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
            ShapeLogo.Data = _hasLegacyLogo
                ? (Geometry)new GeometryConverter().ConvertFromString(value)
                : null;
            SvgIconControlHelper.ApplyVisibility(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshLogoHostVisibility();
        }
    }

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (ShapeLogo is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(ShapeLogo, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    private void ApplyLogoScale()
    {
        LogoHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshLogoHostVisibility()
    {
        if (LogoHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            LogoHost.Visibility = Visibility.Visible;
            LogoHost.Width = 16;
        }
        else
        {
            LogoHost.Visibility = Visibility.Visible;
            LogoHost.Width = 0;
        }

        LogoHost.Height = 16;
        LogoHost.Margin = new Thickness(12, 0, 0, 0);
        LabText.Margin = new Thickness(8, 0, 12, 0);
    }

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    // 是否选中
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register(nameof(Checked),
        typeof(bool), typeof(MyRadioButton), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) =>
            {
                var obj = (MyRadioButton)d;
                if (!obj._isSettingChecked)
                {
                    obj.EnsureSingleChecked();
                    obj.RefreshColor(null, true);
                }

                if (obj._isUserInteraction) obj.ExecuteCheckCommand();
            }));

    public static readonly DependencyProperty GroupNameProperty = DependencyProperty.Register(
        nameof(GroupName), typeof(string), typeof(MyRadioButton), new PropertyMetadata(null,
            (d, e) =>
            {
                var radioButton = (MyRadioButton)d;
                if (!radioButton.IsLoaded) return;
                radioButton.UnregisterGroup((string?)e.OldValue);
                radioButton.RegisterGroup((string?)e.NewValue);
            }));

    public static readonly DependencyProperty CheckCommandProperty = DependencyProperty.Register(nameof(CheckCommand),
        typeof(ICommand), typeof(MyRadioButton), new PropertyMetadata(null));

    public static readonly DependencyProperty CheckCommandParameterProperty = DependencyProperty.Register(
        nameof(CheckCommandParameter), typeof(object), typeof(MyRadioButton), new PropertyMetadata(null));

    public bool Checked
    {
        get => (bool)GetValue(CheckedProperty);
        set => SetChecked(value, false, true);
    }

    public string? GroupName
    {
        get => (string?)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public ICommand? CheckCommand
    {
        get => (ICommand?)GetValue(CheckCommandProperty);
        set => SetValue(CheckCommandProperty, value);
    }

    public object? CheckCommandParameter
    {
        get => GetValue(CheckCommandParameterProperty);
        set => SetValue(CheckCommandParameterProperty, value);
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get;
        set
        {
            field = value;
            RefreshColor();
        }
    } = ColorState.White; // 颜色类别

    public event CheckEventHandler? Check;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="raiseByMouse">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
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

                if (Parent is null && string.IsNullOrEmpty(GroupName)) return;
                EnsureSingleChecked();

                // 更改动画

                if (!isChanged)
                    return;
                RefreshColor(null, anime);

                // 触发事件
                if (Checked)
                    Check?.Invoke(this, raiseByMouse);
                Launcher.RaiseCustomEvent(this);
            }
            finally
            {
                _isUserInteraction = false;
            }
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "单选按钮勾选改变错误",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
    }

    private void EnsureSingleChecked()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            if (!Checked) return;
            foreach (var radioButton in GetGroupMembers(GroupName))
                if (!ReferenceEquals(radioButton, this))
                    radioButton.SetChecked(false, false, false);
            return;
        }

        if (Parent is null) return;
        if (Parent is not Panel parentPanel) return;
        var radioboxList = new List<MyRadioButton>();
        var checkedCount = 0;
        // 收集控件列表与选中个数
        foreach (var Control in parentPanel.Children)
            if (Control is MyRadioButton radioButton)
            {
                radioboxList.Add(radioButton);
                if (radioButton.Checked)
                    checkedCount += 1;
            }

        // 判断选中情况
        switch (checkedCount)
        {
            case 0:
            {
                // 没有任何单选框被选中，选择第一个
                radioboxList[0].Checked = true;
                break;
            }
            case var @case when @case > 1:
            {
                // 选中项目多于 1 个
                if (Checked)
                {
                    // 如果本控件选中，则取消其他所有控件的选中
                    foreach (var Control in radioboxList)
                        if (Control.Checked && !Control.Equals(this))
                            Control.Checked = false;
                }
                else
                {
                    // 如果本控件未选中，则只保留第一个选中的控件
                    var firstChecked = false;
                    foreach (var Control in radioboxList)
                        if (Control.Checked)
                        {
                            if (firstChecked)
                                Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                            else
                                firstChecked = true;
                        }
                }

                break;
            }
        }
    }

    private void RegisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName, out var members))
            {
                members = new List<WeakReference<MyRadioButton>>();
                GroupRegistry[groupName] = members;
            }

            members.RemoveAll(reference =>
                !reference.TryGetTarget(out var target) || ReferenceEquals(target, this));
            members.Add(new WeakReference<MyRadioButton>(this));
        }

        if (Checked) EnsureSingleChecked();
    }

    private void UnregisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName, out var members)) return;
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
            if (!GroupRegistry.TryGetValue(groupName, out var members)) return radioButtons;
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
        if (_isExecutingCheckCommand || command is null || !command.CanExecute(CheckCommandParameter))
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

    public event PreviewClickEventHandler? PreviewClick;

    private void Radiobox_MouseUp()
    {
        if (Checked)
            return;
        if (!isMouseDown)
            return;
        Common.Log("[Control] 按下单选按钮：" + Text);
        isMouseDown = false;
        var e = new Common.RouteEventArgs(true);
        PreviewClick?.Invoke(this, e);
        if (e.handled)
            return;
        SetChecked(true, true, true);
    }

    private void Radiobox_MouseDown()
    {
        if (Checked)
            return;
        isMouseDown = true;
        RefreshColor();
    }

    private void Radiobox_MouseLeave()
    {
        isMouseDown = false;
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
            if (IsLoaded && !Animations.AniExitsAnyControl() &&
                !false.Equals(e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            // 勾选
                            var color3 =
                                new Common.MyColor(ThemeManager.AppResources["ColorObject3"]);
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty,
                                        color3 - ShapeLogo.Fill,
                                        animationTimeOfCheck),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        color3 - LabText.Foreground, animationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty,
                                    new Common.MyColor(255d, 255d, 255d) - Background,
                                    animationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (isMouseDown)
                        {
                            // 按下
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty,
                                    new Common.MyColor(120d,
                                        new Common.MyColor(
                                            ThemeManager.AppResources["ColorObject8"])) -
                                    Background, 60),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty,
                                        new Common.MyColor(255d, 255d, 255d) - ShapeLogo.Fill,
                                        animationTimeOfMouseIn),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new Common.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty,
                                    new Common.MyColor(50d,
                                        new Common.MyColor(
                                            ThemeManager.AppResources["ColorObject8"])) -
                                    Background,
                                    animationTimeOfMouseIn), "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty,
                                        new Common.MyColor(255d, 255d, 255d) - ShapeLogo.Fill,
                                        animationTimeOfMouseOut),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new Common.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty,
                                    new Common.MyColor(
                                        ThemeManager.AppResources["ColorBrushSemiTransparent"]) -
                                    Background, animationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            // 勾选
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty,
                                        new Common.MyColor(255d, 255d, 255d) - ShapeLogo.Fill,
                                        animationTimeOfCheck),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new Common.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        animationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty, "ColorBrush3",
                                    animationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (isMouseDown)
                        {
                            // 按下
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty, "ColorBrush6",
                                    animationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        animationTimeOfMouseIn),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        "ColorBrush3",
                                        animationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty, "ColorBrush7",
                                    animationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            Animations.AniStart(
                                new[]
                                {
                                    Animations.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        animationTimeOfMouseOut),
                                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                                        "ColorBrush3",
                                        animationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            Animations.AniStart(
                                Animations.AaColor(this, BackgroundProperty,
                                    new Common.MyColor(
                                        ThemeManager.AppResources["ColorBrushSemiTransparent"]) -
                                    Background, animationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                }
            }

            else
            {
                // 不使用动画
                Animations.AniStop("MyRadioButton Checked " + Uuid);
                Animations.AniStop("MyRadioButton Color " + Uuid);
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            Background = new Common.MyColor(255d, 255d, 255d);
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
                        }
                        else
                        {
                            Background = (Brush)ThemeManager.AppResources["ColorBrushSemiTransparent"];
                            ShapeLogo.Fill = new Common.MyColor(255d, 255d, 255d);
                            LabText.Foreground = new Common.MyColor(255d, 255d, 255d);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            SetResourceReference(BackgroundProperty, "ColorBrush3");
                            ShapeLogo.Fill = new Common.MyColor(255d, 255d, 255d);
                            LabText.Foreground = new Common.MyColor(255d, 255d, 255d);
                        }
                        else
                        {
                            Background = (Brush)ThemeManager.AppResources["ColorBrushSemiTransparent"];
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
                        }

                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新单选按钮颜色出错");
        }
    }

    public void RefreshMyRadioButtonColor()
    {
        RefreshColor();
    }
}

using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     单选方块：文本 + 圆环 + 圆点，同一父容器内互斥选中。
///     保留 <see cref="Checked" />、<see cref="CheckCommand" />、<see cref="Inlines" />、
///     <see cref="IMyRadio" /> 事件组与预览拦截语义。
///     勾选、悬停、按下与禁用状态经 <c>PCL.Animations</c> 过渡；动画关闭时直接落到终值。
/// </summary>
public partial class MyRadioBox : UserControl, IMyRadio
{
    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, RouteEventArgs e);

    private const int AnimationTimeOfMouseIn = 100;
    private const int AnimationTimeOfMouseOut = 200;
    private const int AnimationTimeOfCheck = 150;
    private const int AnimationTimeOfPress = 100;

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioBox, bool>(
            nameof(Checked),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyRadioBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyRadioBox, ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyRadioBox, object?>(nameof(CheckCommandParameter));

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private bool _isExecutingCheckCommand;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _mouseDowned;

    public MyRadioBox()
    {
        InitializeComponent();

        PointerPressed += Radiobox_MouseDown;
        PointerReleased += Radiobox_MouseUp;
        PointerEntered += Radiobox_MouseEnter;
        PointerExited += Radiobox_MouseLeave;
        Loaded += (_, _) => SyncUI(false);
        Unloaded += (_, _) => StopAnimations();
    }

    // 自定义属性

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false);
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

    /// <summary>
    ///     文本宿主，供内联文本与排版微调使用。
    /// </summary>
    public TextBlock LabText => LabTextElement;

    // 外观

    private string BorderKey => InputAnimation.Key("MyRadioBox Border", Uuid);
    private string DotKey => InputAnimation.Key("MyRadioBox Dot", Uuid);
    private string BorderColorKey => InputAnimation.Key("MyRadioBox BorderColor", Uuid);
    private string TextColorKey => InputAnimation.Key("MyRadioBox TextColor", Uuid);
    private string BackgroundKey => InputAnimation.Key("MyRadioBox Background", Uuid);

    public event IMyRadio.CheckEventHandler? Check;

    public event IMyRadio.ChangedEventHandler? Changed;

    public event PreviewCheckEventHandler? PreviewCheck;

    public event PreviewChangeEventHandler? PreviewChange;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    public void SetChecked(bool value, bool user)
    {
        try
        {
            // Preview 事件
            if (value && user)
            {
                var args = new RouteEventArgs(user);
                PreviewCheck?.Invoke(this, args);
                if (args.handled)
                {
                    Radiobox_MouseLeave();
                    return;
                }
            }

            _isUserInteraction = user;
            try
            {
                // 自定义属性基础
                var isChanged = false;
                if (IsLoaded && value != Checked)
                    PreviewChange?.Invoke(this, new RouteEventArgs(user));

                if (value != Checked)
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

                    isChanged = true;
                }

                // 保证只有一个单选框选中
                if (Parent is null)
                    return;
                EnsureSingleChecked();

                // 触发事件
                if (isChanged)
                {
                    if (Checked)
                        Check?.Invoke(this, new RouteEventArgs(user));
                    Changed?.Invoke(this, new RouteEventArgs(user));
                    CustomEventService.Raise(this);
                }

                SyncUI(true);
            }
            finally
            {
                _isUserInteraction = false;
            }
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "单选框勾选改变错误");
        }
    }

    private void EnsureSingleChecked()
    {
        if (Parent is not Panel panel)
            return;

        var radioBoxList = new List<MyRadioBox>();
        var checkedCount = 0;

        foreach (var control in panel.Children)
            if (control is MyRadioBox radioBox)
            {
                radioBoxList.Add(radioBox);
                if (radioBox.Checked)
                    checkedCount += 1;
            }

        switch (checkedCount)
        {
            case 0:
                // 没有任何单选框被选中，选择第一个
                if (radioBoxList.Count > 0)
                    radioBoxList[0].Checked = true;
                break;
            case > 1 when Checked:
                // 如果本控件选中，则取消其他所有控件的选中
                foreach (var radioBox in radioBoxList.Where(radioBox =>
                             radioBox.Checked && !ReferenceEquals(radioBox, this)))
                    radioBox.Checked = false;
                break;
            case > 1:
            {
                // 如果本控件未选中，则只保留第一个选中的控件
                var firstChecked = false;
                foreach (var radioBox in radioBoxList.Where(radioBox => radioBox.Checked))
                    if (firstChecked)
                        radioBox.Checked = false;
                    else
                        firstChecked = true;

                break;
            }
        }
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

    // 交互

    private void Radiobox_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!_mouseDowned)
            return;
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        _mouseDowned = false;
        SetChecked(true, true);
        AnimateBackground(AnimationTimeOfPress, true);
    }

    private void Radiobox_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        _mouseDowned = true;
        Focus();
        AnimateBackground(AnimationTimeOfPress, true);
        if (!Checked)
            AnimateBorderPress();
    }

    private void Radiobox_MouseEnter(object? sender, PointerEventArgs e)
    {
        AnimateColors(AnimationTimeOfMouseIn);
    }

    private void Radiobox_MouseLeave()
    {
        if (_mouseDowned)
        {
            _mouseDowned = false;
            AnimateBackground(AnimationTimeOfPress, true);
            if (!Checked)
                RestoreBorderScale();
        }

        AnimateColors(AnimationTimeOfMouseOut);
    }

    private void Radiobox_MouseLeave(object? sender, PointerEventArgs e)
    {
        Radiobox_MouseLeave();
    }

    private void StopAnimations()
    {
        InputAnimation.Stop(BorderKey, DotKey, BorderColorKey, TextColorKey, BackgroundKey);
    }

    /// <summary>
    ///     按当前勾选状态同步视觉；<paramref name="animate" /> 为 <c>false</c> 或动画不可用时直接赋值。
    /// </summary>
    private void SyncUI(bool animate)
    {
        if (ShapeBorderElement is null || ShapeDotElement is null)
            return;

        if (animate && InputAnimation.CanAnimate(this))
        {
            if (Checked)
                AnimateChecked();
            else
                AnimateUnchecked();

            AnimateColors(AnimationTimeOfCheck);
            return;
        }

        StopAnimations();
        SetTerminalShapes();
        AnimateColors(0);
    }

    private void SetTerminalShapes()
    {
        ShapeBorderElement.Width = 18d;
        ShapeBorderElement.Height = 18d;
        ShapeBorderElement.Margin = new Thickness(1d, 0d, 0d, 0d);

        if (Checked)
        {
            ShapeDotElement.Width = 9d;
            ShapeDotElement.Height = 9d;
            ShapeDotElement.Opacity = 1d;
            ShapeDotElement.Margin = new Thickness(5.5d, 0d, 0d, 0d);
        }
        else
        {
            ShapeDotElement.Width = 0d;
            ShapeDotElement.Height = 0d;
            ShapeDotElement.Opacity = 0d;
            ShapeDotElement.Margin = new Thickness(10d, 0d, 0d, 0d);
        }
    }

    private void AnimateChecked()
    {
        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScale(
                    ShapeBorderElement,
                    10d - ShapeBorderElement.Width,
                    AnimationTimeOfCheck,
                    ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Weak),
                    absolute: true),
                PCL.Animations.AaScale(
                    ShapeBorderElement,
                    8d,
                    AnimationTimeOfCheck * 2,
                    (int)Math.Round(AnimationTimeOfCheck * 0.6d),
                    new PCL.Animations.AniEaseOutBack(),
                    absolute: true)
            },
            BorderKey);

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScale(
                    ShapeDotElement,
                    9d - ShapeDotElement.Width,
                    (int)Math.Round(AnimationTimeOfCheck * 2.6d),
                    ease: new PCL.Animations.AniEaseOutBack(PCL.Animations.AniEasePower.Weak),
                    absolute: true),
                PCL.Animations.AaOpacity(
                    ShapeDotElement,
                    1d - ShapeDotElement.Opacity,
                    (int)Math.Round(AnimationTimeOfCheck * 0.5d),
                    (int)Math.Round(AnimationTimeOfCheck * 0.6d))
            },
            DotKey);
    }

    private void AnimateUnchecked()
    {
        PCL.Animations.AniStart(
            PCL.Animations.AaScale(
                ShapeBorderElement,
                18d - ShapeBorderElement.Width,
                AnimationTimeOfCheck,
                ease: new PCL.Animations.AniEaseOutFluent(),
                absolute: true),
            BorderKey);

        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScale(
                    ShapeDotElement,
                    -ShapeDotElement.Width,
                    AnimationTimeOfCheck,
                    ease: new PCL.Animations.AniEaseInFluent(),
                    absolute: true),
                PCL.Animations.AaOpacity(
                    ShapeDotElement,
                    -ShapeDotElement.Opacity,
                    (int)Math.Round(AnimationTimeOfCheck * 0.5d),
                    (int)Math.Round(AnimationTimeOfCheck * 0.2d))
            },
            DotKey);
    }

    private void AnimateBorderPress()
    {
        if (!InputAnimation.CanAnimate(this))
        {
            ShapeBorderElement.Width = 16.5d;
            ShapeBorderElement.Height = 16.5d;
            return;
        }

        PCL.Animations.AniStart(
            PCL.Animations.AaScale(
                ShapeBorderElement,
                16.5d - ShapeBorderElement.Width,
                1000,
                ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong),
                absolute: true),
            BorderKey);
    }

    private void RestoreBorderScale()
    {
        if (!InputAnimation.CanAnimate(this))
        {
            ShapeBorderElement.Width = 18d;
            ShapeBorderElement.Height = 18d;
            ShapeBorderElement.Margin = new Thickness(1d, 0d, 0d, 0d);
            return;
        }

        PCL.Animations.AniStart(
            PCL.Animations.AaScale(
                ShapeBorderElement,
                18d - ShapeBorderElement.Width,
                ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong),
                absolute: true),
            BorderKey);
    }

    private void AnimateBackground(int time, bool animate)
    {
        var key = _mouseDowned || IsPointerOver
            ? InputTheme.CheckPressedKey
            : InputTheme.CheckFillKey;
        InputAnimation.Color(
            ShapeBorderElement,
            Shape.FillProperty,
            key,
            InputTheme.Brush(
                this,
                key,
                _mouseDowned || IsPointerOver ? Brushes.LightBlue : Brushes.WhiteSmoke),
            Math.Max(time, 1),
            BackgroundKey,
            animate);
    }

    private void AnimateColors(int time)
    {
        if (ShapeBorderElement is null || LabTextElement is null)
            return;

        var animate = time > 0 && InputAnimation.CanAnimate(this);
        var hover = IsPointerOver;

        var borderKey = !IsEnabled
            ? InputTheme.DisabledTextKey
            : Checked
                ? InputTheme.CheckedKey
                : hover
                    ? InputTheme.HoverKey
                    : InputTheme.TextKey;
        InputAnimation.Color(
            ShapeBorderElement,
            Shape.StrokeProperty,
            borderKey,
            InputTheme.Brush(this, borderKey, Brushes.Black),
            Math.Max(time, 1),
            BorderColorKey,
            animate);

        var textKey = !IsEnabled
            ? InputTheme.DisabledTextKey
            : hover
                ? InputTheme.HoverKey
                : InputTheme.TextKey;
        InputAnimation.Color(
            LabTextElement,
            TextBlock.ForegroundProperty,
            textKey,
            InputTheme.Brush(this, textKey, Brushes.Black),
            Math.Max(time, 1),
            TextColorKey,
            animate);

        AnimateBackground(Math.Max(time, 1), animate);
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
                SyncUI(true);
            }

            if (_isUserInteraction)
                ExecuteCheckCommand();
        }
        else if (change.Property == TextProperty)
        {
            LabTextElement?.Text = change.NewValue as string ?? string.Empty;
        }
        else if (change.Property == IsEnabledProperty)
        {
            AnimateColors(AnimationTimeOfMouseOut);
        }
    }

    // 主题

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        StopAnimations();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        SyncUI(false);
    }
}
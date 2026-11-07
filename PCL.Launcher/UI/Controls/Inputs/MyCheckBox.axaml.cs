using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using PCL.UI.Controls.Common;

namespace PCL.UI.Controls;

/// <summary>
///     可三态复选框：文本 + 方框 + 勾选/半选形状。
///     保留 <see cref="Checked" />（可空三态）、<see cref="IsThreeState" />、<see cref="Text" />、
///     <see cref="Inlines" />、<see cref="CheckCommand" /> 与 <see cref="Change" />/
///     <see cref="PreviewChange" /> 事件。
///     勾选、悬停、按下与禁用状态经 <c>PCL.Animations</c> 过渡；动画关闭时直接落到终值。
/// </summary>
public partial class MyCheckBox : UserControl
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, RouteEventArgs e);

    private const int AnimationTimeOfCheck = 150;
    private const int AnimationTimeOfMouseIn = 100;
    private const int AnimationTimeOfMouseOut = 200;
    private const int AnimationTimeOfPress = 100;

    public static readonly StyledProperty<bool?> CheckedProperty =
        AvaloniaProperty.Register<MyCheckBox, bool?>(
            nameof(Checked),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    ///     是否为三态复选框。
    /// </summary>
    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<MyCheckBox, bool>(nameof(IsThreeState));

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyCheckBox, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyCheckBox, ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyCheckBox, object?>(nameof(CheckCommandParameter));

    // 基础

    public int Uuid = InputTheme.NewUuid();

    private bool _allowMouseDown = true;
    private bool _isExecutingCheckCommand;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _mouseDowned;
    private bool? _previousState;

    public MyCheckBox()
    {
        InitializeComponent();

        PointerPressed += Checkbox_MouseDown;
        PointerReleased += Checkbox_MouseUp;
        KeyboardActivation.Attach(this, PerformClick);
        PointerEntered += Checkbox_MouseEnter;
        PointerExited += Checkbox_MouseLeave;
        Loaded += (_, _) =>
        {
            _previousState = GetFinalState(Checked, IsThreeState);
            SyncUI(false);
        };
        Unloaded += (_, _) => StopAnimations();
    }

    // 自定义属性

    public bool? Checked
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

    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
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

    private string CheckScaleShowKey => InputAnimation.Key("MyCheckBox Check Scale Show", Uuid);

    private string CheckScaleHideKey => InputAnimation.Key("MyCheckBox Check Scale Hide", Uuid);

    private string IndeterminateScaleShowKey =>
        InputAnimation.Key("MyCheckBox Indeterminate Scale Show", Uuid);

    private string IndeterminateScaleHideKey =>
        InputAnimation.Key("MyCheckBox Indeterminate Scale Hide", Uuid);

    private string BackgroundScaleKey => InputAnimation.Key("MyCheckBox Background Scale", Uuid);

    private string BorderColorKey => InputAnimation.Key("MyCheckBox BorderColor", Uuid);

    private string TextColorKey => InputAnimation.Key("MyCheckBox TextColor", Uuid);

    private string BackgroundKey => InputAnimation.Key("MyCheckBox Background", Uuid);

    private string AllowMouseDownKey => InputAnimation.Key("MyCheckBox AllowMouseDown", Uuid);

    /// <summary>
    ///     复选框勾选状态改变。
    /// </summary>
    /// <param name="user">是否为用户手动改变的勾选状态。</param>
    /// <summary>执行勾选语义；键盘 Enter/Space 与自动化调用共用。</summary>
    public void PerformClick()
    {
        if (!IsEnabled) return;
        if (IsThreeState)
            switch (Checked)
            {
                case true:
                    SetChecked(null, true);
                    break;
                case false:
                    SetChecked(true, true);
                    break;
                case null:
                    SetChecked(false, true);
                    break;
            }
        else
            SetChecked(!Checked, true);

        AnimateBackground(AnimationTimeOfPress, true);
    }

    public event ChangeEventHandler? Change;

    public event PreviewChangeEventHandler? PreviewChange;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    public void SetChecked(bool? value, bool user)
    {
        try
        {
            if (Checked.HasValue && value.HasValue && Checked.Value == value.Value)
                return;

            // Preview 事件
            if (value.HasValue && value.Value && user)
            {
                var args = new RouteEventArgs(user);
                PreviewChange?.Invoke(this, args);
                if (args.handled)
                {
                    _mouseDowned = false;
                    SyncUI(false);
                    return;
                }
            }

            // 判断真实勾选状态
            var isChecked = GetFinalState(value, IsThreeState);

            _previousState = Checked;
            _isUserInteraction = user;
            try
            {
                _isSettingChecked = true;
                SetValue(CheckedProperty, isChecked);
            }
            finally
            {
                _isSettingChecked = false;
                _isUserInteraction = false;
            }

            if (IsLoaded)
                Change?.Invoke(this, user);

            SyncUI(true);
            CustomEventService.Raise(this);
        }
        catch (Exception ex)
        {
            InputTheme.LogDebug(ex, "设置 Checked 失败");
        }
    }

    // 交互

    private void Checkbox_MouseUp(object? sender, PointerReleasedEventArgs e)
    {
        if (!_mouseDowned)
            return;
        if (e.InitialPressMouseButton != MouseButton.Left)
            return;

        _mouseDowned = false;
        PerformClick();
    }

    private void Checkbox_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (!_allowMouseDown)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _mouseDowned = true;
        Focus();
        AnimateBackground(AnimationTimeOfPress, true);
        AnimatePressedScale();
    }

    private void Checkbox_MouseEnter(object? sender, PointerEventArgs e)
    {
        AnimateColors(AnimationTimeOfMouseIn);
    }

    private void Checkbox_MouseLeave(object? sender, PointerEventArgs e)
    {
        if (_mouseDowned)
        {
            _mouseDowned = false;
            AnimateBackground(AnimationTimeOfPress, true);
            RestoreBorderScale();
        }

        AnimateColors(AnimationTimeOfMouseOut);
    }

    /// <summary>
    ///     取消进行中的状态动画并把形状与配色写入终值。
    /// </summary>
    private void StopAnimations()
    {
        InputAnimation.Stop(
            BackgroundScaleKey,
            CheckScaleShowKey,
            CheckScaleHideKey,
            IndeterminateScaleShowKey,
            IndeterminateScaleHideKey,
            BorderColorKey,
            TextColorKey,
            BackgroundKey,
            AllowMouseDownKey);
    }

    /// <summary>
    ///     按当前勾选状态同步视觉；<paramref name="animate" /> 为 <c>false</c> 或动画不可用时直接赋值。
    /// </summary>
    private void SyncUI(bool animate)
    {
        if (ShapeBorderElement is null
            || ShapeCheckElement is null
            || ShapeIndeterminateElement is null)
            return;

        var isChecked = GetFinalState(Checked, IsThreeState);
        var changed = !Nullable.Equals(isChecked, _previousState);

        if (changed && animate && InputAnimation.CanAnimate(this))
        {
            _allowMouseDown = false;

            switch (isChecked, _previousState)
            {
                case (true, null):
                    AnimateBackgroundScale();
                    AnimateIndeterminateHide();
                    AnimateCheckShow();
                    break;
                case (true, false):
                    AnimateBackgroundScale();
                    AnimateCheckShow();
                    break;
                case (false, true):
                    AnimateBackgroundScale();
                    AnimateCheckHide();
                    break;
                case (false, null):
                    AnimateBackgroundScale();
                    AnimateIndeterminateHide();
                    AnimateCheckHide();
                    break;
                case (null, true):
                    AnimateBackgroundScale();
                    AnimateCheckHide();
                    AnimateIndeterminateShow();
                    break;
                case (null, false):
                    AnimateBackgroundScale();
                    AnimateIndeterminateShow();
                    break;
            }

            PCL.Animations.AniStart(
                PCL.Animations.AaCode(() => _allowMouseDown = true, AnimationTimeOfCheck * 2),
                AllowMouseDownKey);

            AnimateColors(AnimationTimeOfCheck);
        }
        else if (!animate || !InputAnimation.CanAnimate(this))
        {
            StopAnimations();
            _allowMouseDown = true;
            SetShapes(isChecked);
            AnimateColors(0);
        }
        else
        {
            AnimateColors(IsEnabled ? AnimationTimeOfMouseOut : AnimationTimeOfCheck);
        }

        _previousState = isChecked;
    }

    private void SetShapes(bool? isChecked)
    {
        ShapeBorderElement.Width = 18d;
        ShapeBorderElement.Height = 18d;
        ShapeBorderElement.Margin = new Thickness(1d, 0d, 0d, 0d);
        InputAnimation.SetScale(ShapeCheckElement, isChecked == true ? 1d : 0d);
        InputAnimation.SetScale(ShapeIndeterminateElement, isChecked is null ? 1d : 0d);
    }

    private void AnimateBackgroundScale()
    {
        PCL.Animations.AniStart(
            new List<PCL.Animations.AniData>
            {
                PCL.Animations.AaScale(
                    ShapeBorderElement,
                    12d - ShapeBorderElement.Width,
                    AnimationTimeOfCheck,
                    ease: new PCL.Animations.AniEaseOutFluent(),
                    absolute: true),
                PCL.Animations.AaScale(
                    ShapeBorderElement,
                    6d,
                    AnimationTimeOfCheck * 2,
                    (int)Math.Round(AnimationTimeOfCheck * 0.7d),
                    new PCL.Animations.AniEaseOutBack(),
                    absolute: true)
            },
            BackgroundScaleKey);
    }

    private void AnimateCheckShow()
    {
        PCL.Animations.AniStart(
            PCL.Animations.AaScaleTransform(
                ShapeCheckElement,
                1d - InputAnimation.CurrentScale(ShapeCheckElement),
                AnimationTimeOfCheck * 2,
                (int)Math.Round(AnimationTimeOfCheck * 0.7d),
                new PCL.Animations.AniEaseOutBack(PCL.Animations.AniEasePower.Weak)),
            CheckScaleShowKey);
    }

    private void AnimateCheckHide()
    {
        PCL.Animations.AniStart(
            PCL.Animations.AaScaleTransform(
                ShapeCheckElement,
                -InputAnimation.CurrentScale(ShapeCheckElement),
                (int)Math.Round(AnimationTimeOfCheck * 0.9d),
                ease: new PCL.Animations.AniEaseInFluent(PCL.Animations.AniEasePower.Weak)),
            CheckScaleHideKey);
    }

    private void AnimateIndeterminateShow()
    {
        PCL.Animations.AniStart(
            PCL.Animations.AaScaleTransform(
                ShapeIndeterminateElement,
                1d - InputAnimation.CurrentScale(ShapeIndeterminateElement),
                AnimationTimeOfCheck * 2,
                (int)Math.Round(AnimationTimeOfCheck * 0.7d),
                new PCL.Animations.AniEaseOutBack(PCL.Animations.AniEasePower.Weak)),
            IndeterminateScaleShowKey);
    }

    private void AnimateIndeterminateHide()
    {
        PCL.Animations.AniStart(
            PCL.Animations.AaScaleTransform(
                ShapeIndeterminateElement,
                -InputAnimation.CurrentScale(ShapeIndeterminateElement),
                (int)Math.Round(AnimationTimeOfCheck * 0.9d),
                ease: new PCL.Animations.AniEaseInFluent(PCL.Animations.AniEasePower.Weak)),
            IndeterminateScaleHideKey);
    }

    private void AnimatePressedScale()
    {
        if (!InputAnimation.CanAnimate(this))
        {
            ShapeBorderElement.Width = 16.5d;
            ShapeBorderElement.Height = 16.5d;
            if (Checked == true)
                InputAnimation.SetScale(ShapeCheckElement, 0.9d);
            return;
        }

        var animations = new List<PCL.Animations.AniData>
        {
            PCL.Animations.AaScale(
                ShapeBorderElement,
                16.5d - ShapeBorderElement.Width,
                1000,
                ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong),
                absolute: true)
        };
        if (Checked == true)
            animations.Add(
                PCL.Animations.AaScaleTransform(
                    ShapeCheckElement,
                    0.9d - InputAnimation.CurrentScale(ShapeCheckElement),
                    1000,
                    ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong)));

        PCL.Animations.AniStart(animations, InputAnimation.Key("MyCheckBox Scale", Uuid));
    }

    private void RestoreBorderScale()
    {
        if (!InputAnimation.CanAnimate(this))
        {
            ShapeBorderElement.Width = 18d;
            ShapeBorderElement.Height = 18d;
            ShapeBorderElement.Margin = new Thickness(1d, 0d, 0d, 0d);
            if (Checked == true)
                InputAnimation.SetScale(ShapeCheckElement, 1d);
            return;
        }

        var animations = new List<PCL.Animations.AniData>
        {
            PCL.Animations.AaScale(
                ShapeBorderElement,
                18d - ShapeBorderElement.Width,
                ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong),
                absolute: true)
        };
        if (Checked == true)
            animations.Add(
                PCL.Animations.AaScaleTransform(
                    ShapeCheckElement,
                    1d - InputAnimation.CurrentScale(ShapeCheckElement),
                    500,
                    ease: new PCL.Animations.AniEaseOutFluent(PCL.Animations.AniEasePower.Strong)));

        PCL.Animations.AniStart(animations, InputAnimation.Key("MyCheckBox Scale", Uuid));
    }

    private void AnimateBackground(int time, bool animate)
    {
        var key = _mouseDowned || IsPointerOver
            ? InputTheme.CheckPressedKey
            : InputTheme.CheckFillKey;
        InputAnimation.Color(
            ShapeBorderElement,
            Border.BackgroundProperty,
            key,
            InputTheme.Brush(
                this,
                key,
                _mouseDowned || IsPointerOver ? Brushes.LightBlue : Brushes.WhiteSmoke),
            time,
            BackgroundKey,
            animate);
    }

    private void AnimateColors(int time)
    {
        if (ShapeBorderElement is null || LabTextElement is null)
            return;

        var animate = time > 0 && InputAnimation.CanAnimate(this);
        var isChecked = GetFinalState(Checked, IsThreeState);
        var hover = IsPointerOver;

        string borderKey;
        if (!IsEnabled)
            borderKey = InputTheme.DisabledTextKey;
        else if (isChecked == true)
            borderKey = InputTheme.CheckedKey;
        else if (hover)
            borderKey = InputTheme.HoverKey;
        else
            borderKey = InputTheme.TextKey;

        var borderFallback = InputTheme.Brush(
            this,
            borderKey,
            !IsEnabled ? Brushes.Gray : hover ? Brushes.DodgerBlue : Brushes.Black);
        InputAnimation.Color(
            ShapeBorderElement,
            Border.BorderBrushProperty,
            borderKey,
            borderFallback,
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

    private static bool? GetFinalState(bool? value, bool isThreeState)
    {
        return isThreeState ? value : value == true;
    }

    // 属性变更处理

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CheckedProperty)
        {
            if (!_isSettingChecked)
            {
                _previousState = change.OldValue as bool?;
                SyncUI(true);
            }

            if (_isUserInteraction)
                ExecuteCheckCommand();
        }
        else if (change.Property == TextProperty)
        {
            LabTextElement?.Text = change.NewValue as string ?? string.Empty;
        }
        else if (change.Property == IsThreeStateProperty || change.Property == IsEnabledProperty)
        {
            SyncUI(true);
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

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PclToggleAutomationPeer(
            this,
            () => Checked,
            PerformClick,
            () => Text);
    }
}
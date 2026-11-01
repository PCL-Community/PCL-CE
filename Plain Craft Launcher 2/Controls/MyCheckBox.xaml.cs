using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyCheckBox
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, Common.RouteEventArgs e);

    private const int animationTimeOfCheck = 150; // 勾选状态变更动画长度

    // 指向动画

    private const int animationTimeOfMouseIn = 100;

    private const int animationTimeOfMouseOut = 200;

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool?),
        typeof(MyCheckBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, e) =>
            {
                var obj = (MyCheckBox)d;
                if (!obj._isSettingChecked)
                {
                    obj._previousState = (bool?)e.OldValue;
                    obj.SyncUI();
                }

                if (obj._isUserInteraction) obj.ExecuteCheckCommand();
            }));

    /// <summary>
    ///     是否为三态复选框。
    /// </summary>
    public static readonly DependencyProperty IsThreeStateProperty =
        DependencyProperty.Register("IsThreeState", typeof(bool), typeof(MyCheckBox), new PropertyMetadata(false));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyCheckBox), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyCheckBox)sender).LabText.Text = (string)e.NewValue;
        }));

    public static readonly DependencyProperty CheckCommandProperty = DependencyProperty.Register(nameof(CheckCommand),
        typeof(ICommand), typeof(MyCheckBox), new PropertyMetadata(null));

    public static readonly DependencyProperty CheckCommandParameterProperty = DependencyProperty.Register(
        nameof(CheckCommandParameter), typeof(object), typeof(MyCheckBox), new PropertyMetadata(null));

    private bool? _previousState = false; // 上一次的勾选状态
    private bool allowMouseDown = true;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;

    // 点击事件

    private bool mouseDowned;

    // 基础

    public int Uuid = Common.GetUuid();

    public MyCheckBox()
    {
        InitializeComponent();

        MouseLeftButtonUp += (_, _) => Checkbox_MouseUp();
        MouseLeftButtonDown += (_, _) => Checkbox_MouseDown();
        MouseLeave += (_, _) => Checkbox_MouseLeave();
        IsEnabledChanged += (_, _) => Checkbox_IsEnabledChanged();
        MouseEnter += (_, _) => Checkbox_MouseEnterAnimation();
        MouseLeave += (_, _) => Checkbox_MouseLeaveAnimation();
    }

    // 自定义属性
    public bool? Checked
    {
        get => (bool?)GetValue(CheckedProperty);
        set => SetChecked(value, false);
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

    public bool IsThreeState
    {
        get => (bool)GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    } // 是否为三态复选框

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    /// <summary>
    ///     复选框勾选状态改变。
    /// </summary>
    /// <param name="user">是否为用户手动改变的勾选状态。</param>
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
                var e = new Common.RouteEventArgs(user);
                PreviewChange?.Invoke(this, e);
                if (e.handled)
                {
                    mouseDowned = true;
                    Checkbox_MouseLeave();
                    mouseDowned = false;
                    return;
                }
            }

            // 判断真实勾选状态
            var isChecked = GetFinalState(value, IsThreeState);

            _previousState = Checked; // 记录上一次的勾选状态
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

            // 更改动画
            SyncUI();
            Launcher.RaiseCustomEvent(this);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "设置 Checked 失败");
        }
    }

    private void SyncUI()
    {
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            allowMouseDown = false;

            var isChecked = GetFinalState(Checked, IsThreeState);

            switch (isChecked, _previousState)
            {
                case (true, null):
                    AniBackgroundScale();
                    AniIndeterminateHide();
                    AniCheckShow();
                    AniColorChecked();
                    AniAllowMouseDown();
                    break;

                case (true, false):
                    AniBackgroundScale();
                    AniCheckShow();
                    AniColorChecked();
                    AniAllowMouseDown();
                    break;

                case (false, true):
                    AniBackgroundScale();
                    AniCheckHide();
                    AniColorUnchecked();
                    AniAllowMouseDown();
                    break;

                case (false, null):
                    AniBackgroundScale();
                    AniIndeterminateHide();
                    AniCheckHide();
                    AniColorUnchecked();
                    AniAllowMouseDown();
                    break;

                case (null, true):
                    AniBackgroundScale();
                    AniCheckHide();
                    AniIndeterminateShow();
                    AniColorUnchecked();
                    AniAllowMouseDown();
                    break;

                case (null, false):
                    AniBackgroundScale();
                    AniIndeterminateShow();
                    AniColorUnchecked();
                    AniAllowMouseDown();
                    break;
            }
        }
        else
        {
            // 不使用动画
            Animations.AniStop("MyCheckBox Background Scale " + Uuid);
            Animations.AniStop("MyCheckBox Check Scale Show" + Uuid);
            Animations.AniStop("MyCheckBox Check Scale Hide" + Uuid);
            Animations.AniStop("MyCheckBox Indeterminate Scale Show" + Uuid);
            Animations.AniStop("MyCheckBox Indeterminate Scale Hide" + Uuid);
            Animations.AniStop("MyCheckBox BorderColor " + Uuid);
            Animations.AniStop("MyCheckBox AllowMouseDown " + Uuid);
            if (Checked == true)
            {
                ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX = 1d;
                ((ScaleTransform)ShapeCheck.RenderTransform).ScaleY = 1d;
                ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
                    IsEnabled ? "ColorBrush2" : "ColorBrushGray4");
            }
            else
            {
                ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX = 0d;
                ((ScaleTransform)ShapeCheck.RenderTransform).ScaleY = 0d;
                ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
                    IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            }
        }
    }

    private void Checkbox_MouseUp()
    {
        if (!mouseDowned)
            return;
        Common.Log("[Control] 按下复选框（" + !Checked + "）：" + Text);
        mouseDowned = false;
        if (IsThreeState)
        {
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

            Animations.AniStart(
                Animations.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite",
                    100),
                "MyCheckBox Background " + Uuid);
            return;
        }

        SetChecked(!Checked, true);
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + Uuid);
    }

    private void Checkbox_MouseDown()
    {
        if (!allowMouseDown)
            return;
        mouseDowned = true;
        Focus();
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushBg1", 100),
            "MyCheckBox Background " + Uuid);
        var scaleAnims = new List<Animations.AniData>
        {
            Animations.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong),
                absolute: true)
        };
        if (Checked == true)
            scaleAnims.Add(Animations.AaScaleTransform(ShapeCheck,
                0.9d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX, 1000,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong)));
        Animations.AniStart(scaleAnims.ToArray(), "MyCheckBox Scale " + Uuid);
    }

    private void Checkbox_MouseLeave()
    {
        if (!mouseDowned)
            return;
        mouseDowned = false;
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + Uuid);
        var scaleAnims = new List<Animations.AniData>
        {
            Animations.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong),
                absolute: true)
        };
        if (Checked == true)
            scaleAnims.Add(Animations.AaScaleTransform(ShapeCheck,
                1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                500, ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong)));
        Animations.AniStart(scaleAnims.ToArray(), "MyCheckBox Scale " + Uuid);
    }

    private void Checkbox_IsEnabledChanged()
    {
        if (IsLoaded && !Animations.AniExitsAnyControl()) // 防止默认属性变更触发动画
        {
            // 有动画
            if (IsEnabled)
            {
                // 可用
                Checkbox_MouseLeaveAnimation();
            }
            else
            {
                // 不可用
                Animations.AniStart(
                    Animations.AaColor(ShapeBorder, Border.BorderBrushProperty,
                        ThemeManager.colorGray4 - ShapeBorder.BorderBrush, animationTimeOfMouseOut),
                    "MyCheckBox BorderColor " + Uuid);
                Animations.AniStart(
                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                        ThemeManager.colorGray4 - LabText.Foreground, animationTimeOfMouseOut),
                    "MyCheckBox TextColor " + Uuid);
            }
        }
        else
        {
            // 无动画
            Animations.AniStop("MyCheckBox TextColor " + Uuid);
            Animations.AniStop("MyCheckBox BorderColor " + Uuid);
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
                IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
        }
    }

    private void Checkbox_MouseEnterAnimation()
    {
        Animations.AniStart(
            Animations.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                animationTimeOfMouseIn),
            "MyCheckBox TextColor " + Uuid);
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BorderBrushProperty, "ColorBrush3",
                animationTimeOfMouseIn),
            "MyCheckBox BorderColor " + Uuid);
    }

    private void Checkbox_MouseLeaveAnimation()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        Animations.AniStart(
            Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                IsEnabled ? "ColorBrush1" : "ColorBrushGray4", animationTimeOfMouseOut),
            "MyCheckBox TextColor " + Uuid);
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
                animationTimeOfMouseOut),
            "MyCheckBox BorderColor " + Uuid);
    }

    // 动画
    private void AniBackgroundScale()
    {
        Animations.AniStart(
            new[]
            {
                Animations.AaScale(ShapeBorder, 12d - ShapeBorder.Width, animationTimeOfCheck,
                    ease: new Animations.AniEaseOutFluent(), absolute: true),
                Animations.AaScale(ShapeBorder, 6d, animationTimeOfCheck * 2,
                    (int)Math.Round(animationTimeOfCheck * 0.7d), new Animations.AniEaseOutBack(),
                    absolute: true)
            }, "MyCheckBox Background Scale " + Uuid);
    }

    private void AniCheckShow()
    {
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeCheck,
                1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                animationTimeOfCheck * 2, (int)Math.Round(animationTimeOfCheck * 0.7d),
                new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
            "MyCheckBox Check Scale Show" + Uuid);
    }

    private void AniCheckHide()
    {
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeCheck,
                -((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                (int)Math.Round(animationTimeOfCheck * 0.9d),
                ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
            "MyCheckBox Check Scale Hide" + Uuid);
    }

    private void AniIndeterminateShow()
    {
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeIndeterminate,
                1d - ((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX, animationTimeOfCheck * 2,
                (int)Math.Round(animationTimeOfCheck * 0.7d),
                new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
            "MyCheckBox Indeterminate Scale Show" + Uuid);
    }

    private void AniIndeterminateHide()
    {
        Animations.AniStart(
            Animations.AaScaleTransform(ShapeIndeterminate,
                -((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX,
                (int)Math.Round(animationTimeOfCheck * 0.9d),
                ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
            "MyCheckBox Indeterminate Scale Hide" + Uuid);
    }

    private void AniAllowMouseDown()
    {
        Animations.AniStart(
            Animations.AaCode(() => allowMouseDown = true, animationTimeOfCheck * 2),
            "MyCheckBox AllowMouseDown " + Uuid);
    }

    private void AniColorChecked()
    {
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush2" : "ColorBrushGray4", animationTimeOfCheck),
            "MyCheckBox BorderColor " + Uuid);
    }

    private void AniColorUnchecked()
    {
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush1" : "ColorBrushGray4", animationTimeOfCheck),
            "MyCheckBox BorderColor " + Uuid);
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

    private static bool? GetFinalState(bool? value, bool isThreeState)
    {
        return isThreeState ? value : value == true;
    }
}

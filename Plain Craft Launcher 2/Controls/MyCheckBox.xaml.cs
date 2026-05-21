using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyCheckBox
{
    public delegate void ChangeEventHandler(object sender, bool user);

    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    private const int AnimationTimeOfCheck = 150; // 勾选状态变更动画长度

    // 指向动画

    private const int AnimationTimeOfMouseIn = 100;

    private const int AnimationTimeOfMouseOut = 200;

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool?),
        typeof(MyCheckBox), new PropertyMetadata(false, (d, e) =>
        {
            var obj = (MyCheckBox)d;
            if (!obj.IsLoaded) obj.SyncUI();
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

    private bool? _previousState = false; // 上一次的勾选状态
    private bool AllowMouseDown = true;

    // 点击事件

    private bool MouseDowned;

    // 基础

    public int Uuid = ModBase.GetUuid();

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
            if (Checked is var arg1 && value.HasValue && arg1.HasValue && value.Value == arg1.Value)
                return;

            // Preview 事件
            if ((!value.HasValue || value.Value) && user && value.HasValue)
            {
                var e = new ModBase.RouteEventArgs(user);
                PreviewChange?.Invoke(this, e);
                if (e.Handled)
                {
                    MouseDowned = true;
                    Checkbox_MouseLeave();
                    MouseDowned = false;
                    return;
                }
            }

            // 判断真实勾选状态
            var isChecked = GetFinalState(value, IsThreeState);

            _previousState = Checked; // 记录上一次的勾选状态
            SetValue(CheckedProperty, isChecked);
            if (IsLoaded)
                Change?.Invoke(this, user);

            // 更改动画
            SyncUI();
            ModMain.RaiseCustomEvent(this);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置 Checked 失败");
        }
    }

    private void SyncUI()
    {
        if (ModAnimation.AniControlEnabled == 0 && IsLoaded) // 防止默认属性变更触发动画
        {
            AllowMouseDown = false;

            var isChecked = GetFinalState(Checked, IsThreeState);

            switch (isChecked, _previousState)
            {
                case (true, false):
                    AniBackgroundScale();
                    AniCheckShow();
                    AniColorChecked();
                    AniAllowMouseDown();
                    break;

                case (true, null):
                    AniBackgroundScale();
                    AniIndeterminateHide();
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
            ModAnimation.AniStop("MyCheckBox Background Scale " + Uuid);
            ModAnimation.AniStop("MyCheckBox Check Scale Show" + Uuid);
            ModAnimation.AniStop("MyCheckBox Check Scale Hide" + Uuid);
            ModAnimation.AniStop("MyCheckBox Indeterminate Scale Show" + Uuid);
            ModAnimation.AniStop("MyCheckBox Indeterminate Scale Hide" + Uuid);
            ModAnimation.AniStop("MyCheckBox BorderColor " + Uuid);
            ModAnimation.AniStop("MyCheckBox AllowMouseDown " + Uuid);
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
        if (!MouseDowned)
            return;
        ModBase.Log("[Control] 按下复选框（" + !Checked + "）：" + Text);
        MouseDowned = false;
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

            ModAnimation.AniStart(
                ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
                "MyCheckBox Background " + Uuid);
            return;
        }

        SetChecked(!Checked, true);
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + Uuid);
    }

    private void Checkbox_MouseDown()
    {
        if (!AllowMouseDown)
            return;
        MouseDowned = true;
        Focus();
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushBg1", 100),
            "MyCheckBox Background " + Uuid);
        if (Checked == true)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                    ModAnimation.AaScaleTransform(ShapeCheck,
                        0.9d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX, 1000,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "MyCheckBox Scale " + Uuid);
        else
            ModAnimation.AniStart(
                ModAnimation.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                    Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                "MyCheckBox Scale " + Uuid);
    }

    private void Checkbox_MouseLeave()
    {
        if (!MouseDowned)
            return;
        MouseDowned = false;
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + Uuid);
        if (Checked == true)
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                    ModAnimation.AaScaleTransform(ShapeCheck, 1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                        500, Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                }, "MyCheckBox Scale " + Uuid);
        else
            ModAnimation.AniStart(
                ModAnimation.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                    Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                "MyCheckBox Scale " + Uuid);
    }

    private void Checkbox_IsEnabledChanged()
    {
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
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
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                            ThemeManager.ColorGray4 - ShapeBorder.BorderBrush, AnimationTimeOfMouseOut)
                    }, "MyCheckBox BorderColor " + Uuid);
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                            ThemeManager.ColorGray4 - LabText.Foreground, AnimationTimeOfMouseOut)
                    }, "MyCheckBox TextColor " + Uuid);
            }
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyCheckBox TextColor " + Uuid);
            ModAnimation.AniStop("MyCheckBox BorderColor " + Uuid);
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
                IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
        }
    }

    private void Checkbox_MouseEnterAnimation()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn)
            }, "MyCheckBox TextColor " + Uuid);
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty, "ColorBrush3", AnimationTimeOfMouseIn)
            }, "MyCheckBox BorderColor " + Uuid);
    }

    private void Checkbox_MouseLeaveAnimation()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                    IsEnabled ? "ColorBrush1" : "ColorBrushGray4", AnimationTimeOfMouseOut)
            }, "MyCheckBox TextColor " + Uuid);
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                    IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
                    AnimationTimeOfMouseOut)
            }, "MyCheckBox BorderColor " + Uuid);
    }

    // 动画
    private void AniBackgroundScale()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScale(ShapeBorder, 12d - ShapeBorder.Width, AnimationTimeOfCheck,
                    Ease: new ModAnimation.AniEaseOutFluent(), Absolute: true),
                ModAnimation.AaScale(ShapeBorder, 6d, AnimationTimeOfCheck * 2,
                    (int)Math.Round(AnimationTimeOfCheck * 0.7d), new ModAnimation.AniEaseOutBack(), Absolute: true)
            }, "MyCheckBox Background Scale " + Uuid);
    }

    private void AniCheckShow()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(ShapeCheck, 1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                    AnimationTimeOfCheck * 2, (int)Math.Round(AnimationTimeOfCheck * 0.7d),
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
            }, "MyCheckBox Check Scale Show" + Uuid);
    }

    private void AniCheckHide()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(ShapeCheck, -((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                    (int)Math.Round(AnimationTimeOfCheck * 0.9d),
                    Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak))
            }, "MyCheckBox Check Scale Hide" + Uuid);
    }

    private void AniIndeterminateShow()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(ShapeIndeterminate,
                    1d - ((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX, AnimationTimeOfCheck * 2,
                    (int)Math.Round(AnimationTimeOfCheck * 0.7d),
                    new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak))
            }, "MyCheckBox Indeterminate Scale Show" + Uuid);
    }

    private void AniIndeterminateHide()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScaleTransform(ShapeIndeterminate,
                    -((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX,
                    (int)Math.Round(AnimationTimeOfCheck * 0.9d),
                    Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak))
            }, "MyCheckBox Indeterminate Scale Hide" + Uuid);
    }

    private void AniAllowMouseDown()
    {
        ModAnimation.AniStart(new[] { ModAnimation.AaCode(() => AllowMouseDown = true, AnimationTimeOfCheck * 2) },
            "MyCheckBox AllowMouseDown " + Uuid);
    }

    private void AniColorChecked()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                    IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush2" : "ColorBrushGray4", AnimationTimeOfCheck)
            }, "MyCheckBox BorderColor " + Uuid);
    }

    private void AniColorUnchecked()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                    IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush1" : "ColorBrushGray4", AnimationTimeOfCheck)
            }, "MyCheckBox BorderColor " + Uuid);
    }

    private bool? GetFinalState(bool? value, bool isThreeState)
    {
        if (isThreeState)
        {
            // 三态复选框
            if (value.HasValue && value.Value) return true;

            if (value.HasValue && !value.Value) return false;

            return default;
            // 空值表示未选中状态
        }

        // 二态复选框
        return value == true ? true : false;
    }
}
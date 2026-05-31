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

    private const int animationTimeOfCheck = 150; // 勾选状态变更动画长度

    // 指向动画

    private const int animationTimeOfMouseIn = 100;

    private const int animationTimeOfMouseOut = 200;

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
    private bool allowMouseDown = true;

    // 点击事件

    private bool mouseDowned;

    // 基础

    public int uuid = ModBase.GetUuid();

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
            if (Checked.HasValue && value.HasValue && Checked.Value == value.Value)
                return;

            // Preview 事件
            if (value.HasValue && value.Value && user)
            {
                var e = new ModBase.RouteEventArgs(user);
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
            ModAnimation.AniStop("MyCheckBox Background Scale " + uuid);
            ModAnimation.AniStop("MyCheckBox Check Scale Show" + uuid);
            ModAnimation.AniStop("MyCheckBox Check Scale Hide" + uuid);
            ModAnimation.AniStop("MyCheckBox Indeterminate Scale Show" + uuid);
            ModAnimation.AniStop("MyCheckBox Indeterminate Scale Hide" + uuid);
            ModAnimation.AniStop("MyCheckBox BorderColor " + uuid);
            ModAnimation.AniStop("MyCheckBox AllowMouseDown " + uuid);
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
        ModBase.Log("[Control] 按下复选框（" + !Checked + "）：" + Text);
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

            ModAnimation.AniStart(
                ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
                "MyCheckBox Background " + uuid);
            return;
        }

        SetChecked(!Checked, true);
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + uuid);
    }

    private void Checkbox_MouseDown()
    {
        if (!allowMouseDown)
            return;
        mouseDowned = true;
        Focus();
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushBg1", 100),
            "MyCheckBox Background " + uuid);
        var scaleAnims = new List<ModAnimation.AniData>
        {
            ModAnimation.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), absolute: true)
        };
        if (Checked == true)
            scaleAnims.Add(ModAnimation.AaScaleTransform(ShapeCheck,
                0.9d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX, 1000,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)));
        ModAnimation.AniStart(scaleAnims.ToArray(), "MyCheckBox Scale " + uuid);
    }

    private void Checkbox_MouseLeave()
    {
        if (!mouseDowned)
            return;
        mouseDowned = false;
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Border.BackgroundProperty, "ColorBrushHalfWhite", 100),
            "MyCheckBox Background " + uuid);
        var scaleAnims = new List<ModAnimation.AniData>
        {
            ModAnimation.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), absolute: true)
        };
        if (Checked == true)
            scaleAnims.Add(ModAnimation.AaScaleTransform(ShapeCheck, 1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                500, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)));
        ModAnimation.AniStart(scaleAnims.ToArray(), "MyCheckBox Scale " + uuid);
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
                    ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                        ThemeManager.colorGray4 - ShapeBorder.BorderBrush, animationTimeOfMouseOut),
                    "MyCheckBox BorderColor " + uuid);
                ModAnimation.AniStart(
                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                        ThemeManager.colorGray4 - LabText.Foreground, animationTimeOfMouseOut),
                    "MyCheckBox TextColor " + uuid);
            }
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyCheckBox TextColor " + uuid);
            ModAnimation.AniStop("MyCheckBox BorderColor " + uuid);
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            ShapeBorder.SetResourceReference(Border.BorderBrushProperty,
                IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
        }
    }

    private void Checkbox_MouseEnterAnimation()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", animationTimeOfMouseIn),
            "MyCheckBox TextColor " + uuid);
        ModAnimation.AniStart(
            ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty, "ColorBrush3", animationTimeOfMouseIn),
            "MyCheckBox BorderColor " + uuid);
    }

    private void Checkbox_MouseLeaveAnimation()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        ModAnimation.AniStart(
            ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                IsEnabled ? "ColorBrush1" : "ColorBrushGray4", animationTimeOfMouseOut),
            "MyCheckBox TextColor " + uuid);
        ModAnimation.AniStart(
            ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? Checked == true ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4",
                animationTimeOfMouseOut),
            "MyCheckBox BorderColor " + uuid);
    }

    // 动画
    private void AniBackgroundScale()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaScale(ShapeBorder, 12d - ShapeBorder.Width, animationTimeOfCheck,
                    ease: new ModAnimation.AniEaseOutFluent(), absolute: true),
                ModAnimation.AaScale(ShapeBorder, 6d, animationTimeOfCheck * 2,
                    (int)Math.Round(animationTimeOfCheck * 0.7d), new ModAnimation.AniEaseOutBack(), absolute: true)
            }, "MyCheckBox Background Scale " + uuid);
    }

    private void AniCheckShow()
    {
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeCheck, 1d - ((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                animationTimeOfCheck * 2, (int)Math.Round(animationTimeOfCheck * 0.7d),
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            "MyCheckBox Check Scale Show" + uuid);
    }

    private void AniCheckHide()
    {
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeCheck, -((ScaleTransform)ShapeCheck.RenderTransform).ScaleX,
                (int)Math.Round(animationTimeOfCheck * 0.9d),
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            "MyCheckBox Check Scale Hide" + uuid);
    }

    private void AniIndeterminateShow()
    {
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeIndeterminate,
                1d - ((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX, animationTimeOfCheck * 2,
                (int)Math.Round(animationTimeOfCheck * 0.7d),
                new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
            "MyCheckBox Indeterminate Scale Show" + uuid);
    }

    private void AniIndeterminateHide()
    {
        ModAnimation.AniStart(
            ModAnimation.AaScaleTransform(ShapeIndeterminate,
                -((ScaleTransform)ShapeIndeterminate.RenderTransform).ScaleX,
                (int)Math.Round(animationTimeOfCheck * 0.9d),
                ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            "MyCheckBox Indeterminate Scale Hide" + uuid);
    }

    private void AniAllowMouseDown()
    {
        ModAnimation.AniStart(ModAnimation.AaCode(() => allowMouseDown = true, animationTimeOfCheck * 2),
            "MyCheckBox AllowMouseDown " + uuid);
    }

    private void AniColorChecked()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush2" : "ColorBrushGray4", animationTimeOfCheck),
            "MyCheckBox BorderColor " + uuid);
    }

    private void AniColorUnchecked()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(ShapeBorder, Border.BorderBrushProperty,
                IsEnabled ? IsMouseOver ? "ColorBrush3" : "ColorBrush1" : "ColorBrushGray4", animationTimeOfCheck),
            "MyCheckBox BorderColor " + uuid);
    }

    private static bool? GetFinalState(bool? value, bool isThreeState)
    {
        return isThreeState ? value : value == true;
    }
}

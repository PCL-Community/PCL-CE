using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyRadioBox : IMyRadio
{
    public delegate void PreviewChangeEventHandler(object sender, ModBase.RouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, ModBase.RouteEventArgs e);

    // 指向动画

    private const int AnimationTimeOfMouseIn = 100; // 鼠标指向动画长度
    private const int AnimationTimeOfMouseOut = 200; // 鼠标指向动画长度

    private const int AnimationTimeOfCheck = 150; // 勾选状态变更动画长度

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool),
        typeof(MyRadioBox), new PropertyMetadata(false, (dRaw, e) =>
        {
            var d = (MyRadioBox)dRaw;
            if (!d.IsLoaded) d.SyncUI();
        }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioBox), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyRadioBox)sender).LabText.Text = (string)e.NewValue;
        }));

    private bool AllowMouseDown = true;

    // 点击事件

    private bool MouseDowned;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyRadioBox()
    {
        InitializeComponent();
        MouseLeftButtonUp += (_, _) => Radiobox_MouseUp();
        MouseLeftButtonDown += (_, _) => Radiobox_MouseDown();
        MouseLeave += (_, _) => Radiobox_MouseLeave();
        IsEnabledChanged += (_, _) => Radiobox_IsEnabledChanged();
        MouseEnter += (_, _) => Radiobox_MouseEnterAnimation();
        MouseLeave += (_, _) => Radiobox_MouseLeaveAnimation();
    }

    // 自定义属性
    public bool Checked
    {
        get => (bool)GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

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
                var e = new ModBase.RouteEventArgs(user);
                PreviewCheck?.Invoke(this, e);
                if (e.Handled)
                {
                    Radiobox_MouseLeave();
                    return;
                }
            }

            // 自定义属性基础
            var IsChanged = false;
            if (IsLoaded && value != Checked)
                PreviewChange?.Invoke(this, new ModBase.RouteEventArgs(user));
            if (value != Checked)
            {
                SetValue(CheckedProperty, value);
                IsChanged = true;
            }

            // 保证只有一个单选框选中
            if (Parent is null)
                return;
            var RadioboxList = new List<MyRadioBox>();
            var CheckedCount = 0;
            foreach (var Control in ((Panel)Parent).Children) // 收集控件列表与选中个数
                if (Control is MyRadioBox radioBox)
                {
                    RadioboxList.Add(radioBox);
                    if (radioBox.Checked)
                        CheckedCount += 1;
                }

            switch (CheckedCount) // 判断选中情况
            {
                case 0:
                {
                    // 没有任何单选框被选中，选择第一个
                    RadioboxList[0].Checked = true;
                    break;
                }
                case var @case when @case > 1:
                {
                    // 选中项目多于 1 个
                    if (Checked)
                    {
                        // 如果本控件选中，则取消其他所有控件的选中
                        foreach (var Control in RadioboxList)
                            if (Control.Checked && !Control.Equals(this))
                                Control.Checked = false;
                    }
                    else
                    {
                        // 如果本控件未选中，则只保留第一个选中的控件
                        var FirstChecked = false;
                        foreach (var Control in RadioboxList)
                            if (Control.Checked)
                            {
                                if (FirstChecked)
                                    Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                else
                                    FirstChecked = true;
                            }
                    }

                    break;
                }
            }

            // 触发事件
            if (IsChanged)
            {
                if (Checked)
                    Check?.Invoke(this, new ModBase.RouteEventArgs(user));
                Changed?.Invoke(this, new ModBase.RouteEventArgs(user));
                ModMain.RaiseCustomEvent(this);
            }

            // 更改动画
            SyncUI();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "单选框勾选改变错误", ModBase.LogLevel.Hint);
        }
    }

    private void SyncUI()
    {
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            if (Checked)
            {
                // 由无变有
                if (ShapeDot.Opacity < 0.01d)
                    ShapeDot.Opacity = 1d;
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaScale(ShapeBorder, 10d - ShapeBorder.Width, AnimationTimeOfCheck,
                            Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak), Absolute: true),
                        ModAnimation.AaScale(ShapeBorder, 8d, AnimationTimeOfCheck * 2,
                            (int)Math.Round(AnimationTimeOfCheck * 0.6d), new ModAnimation.AniEaseOutBack(),
                            Absolute: true)
                    }, "MyRadioBox Border " + Uuid);
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaScale(ShapeDot, 9d - ShapeDot.Width,
                            (int)Math.Round(AnimationTimeOfCheck * 2.6d),
                            Ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak), Absolute: true),
                        ModAnimation.AaOpacity(ShapeDot, 1d - ShapeDot.Opacity,
                            (int)Math.Round(AnimationTimeOfCheck * 0.5d), (int)Math.Round(AnimationTimeOfCheck * 0.6d))
                    }, "MyRadioBox Dot " + Uuid);
                ModAnimation.AniStart(
                    ModAnimation.AaColor(ShapeBorder, Shape.StrokeProperty,
                        IsMouseOver ? "ColorBrush3" : IsEnabled ? "ColorBrush2" : "ColorBrushGray4",
                        AnimationTimeOfCheck),
                    "MyRadioBox BorderColor " + Uuid);
            }
            else
            {
                // 由有变无
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaScale(ShapeBorder, 18d - ShapeBorder.Width, AnimationTimeOfCheck,
                            Ease: new ModAnimation.AniEaseOutFluent(), Absolute: true)
                    }, "MyRadioBox Border " + Uuid);
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaScale(ShapeDot, -ShapeDot.Width, AnimationTimeOfCheck,
                            Ease: new ModAnimation.AniEaseInFluent(), Absolute: true),
                        ModAnimation.AaOpacity(ShapeDot, -ShapeDot.Opacity,
                            (int)Math.Round(AnimationTimeOfCheck * 0.5d), (int)Math.Round(AnimationTimeOfCheck * 0.2d))
                    }, "MyRadioBox Dot " + Uuid);
                ModAnimation.AniStart(
                    ModAnimation.AaColor(ShapeBorder, Shape.StrokeProperty,
                        IsMouseOver ? "ColorBrush3" : IsEnabled ? "ColorBrush1" : "ColorBrushGray4",
                        AnimationTimeOfCheck),
                    "MyRadioBox BorderColor " + Uuid);
            }
        }
        else
        {
            // 不使用动画
            ModAnimation.AniStop("MyRadioBox Border " + Uuid);
            ModAnimation.AniStop("MyRadioBox Dot " + Uuid);
            ModAnimation.AniStop("MyRadioBox BorderColor " + Uuid);
            if (Checked)
            {
                ShapeDot.Width = 9d;
                ShapeDot.Height = 9d;
                ShapeDot.Opacity = 1d;
                ShapeDot.Margin = new Thickness(5.5d, 0d, 0d, 0d);
                ShapeBorder.SetResourceReference(Shape.StrokeProperty, IsEnabled ? "ColorBrush2" : "ColorBrushGray4");
            }
            else
            {
                ShapeDot.Width = 0d;
                ShapeDot.Height = 0d;
                ShapeDot.Opacity = 0d;
                ShapeDot.Margin = new Thickness(10d, 0d, 0d, 0d);
                ShapeBorder.SetResourceReference(Shape.StrokeProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            }
        }
    }

    private void Radiobox_MouseUp()
    {
        if (!MouseDowned)
            return;
        ModBase.Log("[Control] 按下单选框：" + Text);
        SetChecked(true, true);
        MouseDowned = false;
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushHalfWhite", 100),
            "MyRadioBox Background " + Uuid);
    }

    private void Radiobox_MouseDown()
    {
        MouseDowned = true;
        Focus();
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushBg1", 100),
            "MyRadioBox Background " + Uuid);
        if (!Checked)
            ModAnimation.AniStart(
                ModAnimation.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                    Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                "MyRadioBox Border " + Uuid);
    }

    private void Radiobox_MouseLeave()
    {
        if (!MouseDowned)
            return;
        MouseDowned = false;
        ModAnimation.AniStart(ModAnimation.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushHalfWhite", 100),
            "MyRadioBox Background " + Uuid);
        if (!Checked)
            ModAnimation.AniStart(
                ModAnimation.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                    Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong), Absolute: true),
                "MyRadioBox Border " + Uuid);
    }

    private void Radiobox_IsEnabledChanged()
    {
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            // 有动画
            if (IsEnabled)
            {
                // 可用
                Radiobox_MouseLeaveAnimation();
            }
            else
            {
                // 不可用
                ModAnimation.AniStart(
                    ModAnimation.AaColor(ShapeBorder, Shape.StrokeProperty, ThemeManager.ColorGray4 - ShapeBorder.Stroke,
                        AnimationTimeOfMouseOut), "MyRadioBox BorderColor " + Uuid);
                ModAnimation.AniStart(
                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                        ThemeManager.ColorGray4 - LabText.Foreground, AnimationTimeOfMouseOut),
                    "MyRadioBox TextColor " + Uuid);
            }
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyRadioBox BorderColor " + Uuid);
            ModAnimation.AniStop("MyRadioBox TextColor " + Uuid);
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            ShapeBorder.SetResourceReference(Shape.StrokeProperty,
                IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
        }
    }

    private void Radiobox_MouseEnterAnimation()
    {
        ModAnimation.AniStart(
            ModAnimation.AaColor(ShapeBorder, Shape.StrokeProperty, "ColorBrush3", AnimationTimeOfMouseIn),
            "MyRadioBox BorderColor " + Uuid);
        ModAnimation.AniStart(
            ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3", AnimationTimeOfMouseIn),
            "MyRadioBox TextColor " + Uuid);
    }

    private void Radiobox_MouseLeaveAnimation()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(ShapeBorder, Shape.StrokeProperty,
                    IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4", AnimationTimeOfMouseOut),
                "MyRadioBox BorderColor " + Uuid);
            ModAnimation.AniStart(
                ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                    IsEnabled ? "ColorBrush1" : "ColorBrushGray4", AnimationTimeOfMouseOut),
                "MyRadioBox TextColor " + Uuid);
        }
        else
        {
            ModAnimation.AniStop("MyRadioBox BorderColor " + Uuid);
            ModAnimation.AniStop("MyRadioBox TextColor " + Uuid);
            ShapeBorder.SetResourceReference(Shape.StrokeProperty,
                IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
        }
    }
}

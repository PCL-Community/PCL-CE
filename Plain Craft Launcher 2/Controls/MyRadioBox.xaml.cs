﻿using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shapes;

using PCL.Core.App.Localization;
namespace PCL;

[ContentProperty("Inlines")]
public partial class MyRadioBox : IMyRadio
{
    public delegate void PreviewChangeEventHandler(object sender, Common.RouteEventArgs e);

    public delegate void PreviewCheckEventHandler(object sender, Common.RouteEventArgs e);

    // 指向动画

    private const int animationTimeOfMouseIn = 100; // 鼠标指向动画长度
    private const int animationTimeOfMouseOut = 200; // 鼠标指向动画长度

    private const int animationTimeOfCheck = 150; // 勾选状态变更动画长度

    // 在使用 XAML 设置 Checked 属性时，不会触发 Checked_Set 方法，所以需要在这里手动触发 UI 改变
    public static readonly DependencyProperty CheckedProperty = DependencyProperty.Register("Checked", typeof(bool),
        typeof(MyRadioBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (dRaw, e) =>
            {
                var d = (MyRadioBox)dRaw;
                if (!d._isSettingChecked)
                {
                    d.EnsureSingleChecked();
                    d.SyncUI();
                }

                if (d._isUserInteraction) d.ExecuteCheckCommand();
            }));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioBox), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyRadioBox)sender).LabText.Text = (string)e.NewValue;
        }));

    public static readonly DependencyProperty CheckCommandProperty = DependencyProperty.Register(nameof(CheckCommand),
        typeof(ICommand), typeof(MyRadioBox), new PropertyMetadata(null));

    public static readonly DependencyProperty CheckCommandParameterProperty = DependencyProperty.Register(
        nameof(CheckCommandParameter), typeof(object), typeof(MyRadioBox), new PropertyMetadata(null));

    private bool allowMouseDown = true;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;

    // 点击事件

    private bool mouseDowned;

    // 基础

    public int Uuid = Common.GetUuid();

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
                var e = new Common.RouteEventArgs(user);
                PreviewCheck?.Invoke(this, e);
                if (e.handled)
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
                    PreviewChange?.Invoke(this, new Common.RouteEventArgs(user));
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
                        Check?.Invoke(this, new Common.RouteEventArgs(user));
                    Changed?.Invoke(this, new Common.RouteEventArgs(user));
                    Launcher.RaiseCustomEvent(this);
                }

                // 更改动画
                SyncUI();
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
                "单选框勾选改变错误",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
    }

    private void EnsureSingleChecked()
    {
        if (Parent is null)
            return;
        var radioboxList = new List<MyRadioBox>();
        var checkedCount = 0;
        foreach (var Control in ((Panel)Parent).Children) // 收集控件列表与选中个数
            if (Control is MyRadioBox radioBox)
            {
                radioboxList.Add(radioBox);
                if (radioBox.Checked)
                    checkedCount += 1;
            }

        switch (checkedCount) // 判断选中情况
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

    private void SyncUI()
    {
        if (ControlVisualHelpers.ShouldAnimate(this)) // 防止默认属性变更触发动画
        {
            if (Checked)
            {
                // 由无变有
                if (ShapeDot.Opacity < 0.01d)
                    ShapeDot.Opacity = 1d;
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScale(ShapeBorder, 10d - ShapeBorder.Width,
                            animationTimeOfCheck,
                            ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak),
                            absolute: true),
                        Animations.AaScale(ShapeBorder, 8d, animationTimeOfCheck * 2,
                            (int)Math.Round(animationTimeOfCheck * 0.6d),
                            new Animations.AniEaseOutBack(),
                            absolute: true)
                    }, "MyRadioBox Border " + Uuid);
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScale(ShapeDot, 9d - ShapeDot.Width,
                            (int)Math.Round(animationTimeOfCheck * 2.6d),
                            ease: new Animations.AniEaseOutBack(Animations.AniEasePower.Weak),
                            absolute: true),
                        Animations.AaOpacity(ShapeDot, 1d - ShapeDot.Opacity,
                            (int)Math.Round(animationTimeOfCheck * 0.5d), (int)Math.Round(animationTimeOfCheck * 0.6d))
                    }, "MyRadioBox Dot " + Uuid);
                Animations.AniStart(
                    Animations.AaColor(ShapeBorder, Shape.StrokeProperty,
                        IsMouseOver ? "ColorBrush3" : IsEnabled ? "ColorBrush2" : "ColorBrushGray4",
                        animationTimeOfCheck),
                    "MyRadioBox BorderColor " + Uuid);
            }
            else
            {
                // 由有变无
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                            animationTimeOfCheck,
                            ease: new Animations.AniEaseOutFluent(), absolute: true)
                    }, "MyRadioBox Border " + Uuid);
                Animations.AniStart(
                    new[]
                    {
                        Animations.AaScale(ShapeDot, -ShapeDot.Width, animationTimeOfCheck,
                            ease: new Animations.AniEaseInFluent(), absolute: true),
                        Animations.AaOpacity(ShapeDot, -ShapeDot.Opacity,
                            (int)Math.Round(animationTimeOfCheck * 0.5d), (int)Math.Round(animationTimeOfCheck * 0.2d))
                    }, "MyRadioBox Dot " + Uuid);
                Animations.AniStart(
                    Animations.AaColor(ShapeBorder, Shape.StrokeProperty,
                        IsMouseOver ? "ColorBrush3" : IsEnabled ? "ColorBrush1" : "ColorBrushGray4",
                        animationTimeOfCheck),
                    "MyRadioBox BorderColor " + Uuid);
            }
        }
        else
        {
            // 不使用动画
            Animations.AniStop("MyRadioBox Border " + Uuid);
            Animations.AniStop("MyRadioBox Dot " + Uuid);
            Animations.AniStop("MyRadioBox BorderColor " + Uuid);
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
        if (!mouseDowned)
            return;
        Common.Log("[Control] 按下单选框：" + Text);
        SetChecked(true, true);
        mouseDowned = false;
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushHalfWhite", 100),
            "MyRadioBox Background " + Uuid);
    }

    private void Radiobox_MouseDown()
    {
        mouseDowned = true;
        Focus();
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushBg1", 100),
            "MyRadioBox Background " + Uuid);
        if (!Checked)
            Animations.AniStart(
                Animations.AaScale(ShapeBorder, 16.5d - ShapeBorder.Width, 1000,
                    ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong),
                    absolute: true),
                "MyRadioBox Border " + Uuid);
    }

    private void Radiobox_MouseLeave()
    {
        if (!mouseDowned)
            return;
        mouseDowned = false;
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Shape.FillProperty, "ColorBrushHalfWhite", 100),
            "MyRadioBox Background " + Uuid);
        if (!Checked)
            Animations.AniStart(
                Animations.AaScale(ShapeBorder, 18d - ShapeBorder.Width,
                    ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Strong),
                    absolute: true),
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
                Animations.AniStart(
                    Animations.AaColor(ShapeBorder, Shape.StrokeProperty,
                        ThemeManager.colorGray4 - ShapeBorder.Stroke,
                        animationTimeOfMouseOut), "MyRadioBox BorderColor " + Uuid);
                Animations.AniStart(
                    Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                        ThemeManager.colorGray4 - LabText.Foreground, animationTimeOfMouseOut),
                    "MyRadioBox TextColor " + Uuid);
            }
        }
        else
        {
            // 无动画
            Animations.AniStop("MyRadioBox BorderColor " + Uuid);
            Animations.AniStop("MyRadioBox TextColor " + Uuid);
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
            ShapeBorder.SetResourceReference(Shape.StrokeProperty,
                IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
        }
    }

    private void Radiobox_MouseEnterAnimation()
    {
        Animations.AniStart(
            Animations.AaColor(ShapeBorder, Shape.StrokeProperty, "ColorBrush3",
                animationTimeOfMouseIn),
            "MyRadioBox BorderColor " + Uuid);
        Animations.AniStart(
            Animations.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                animationTimeOfMouseIn),
            "MyRadioBox TextColor " + Uuid);
    }

    private void Radiobox_MouseLeaveAnimation()
    {
        if (!IsEnabled)
            return; // MouseLeave 比 IsEnabledChanged 后执行，所以如果自定义事件修改了 IsEnabled，将导致显示错误
        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            Animations.AniStart(
                Animations.AaColor(ShapeBorder, Shape.StrokeProperty,
                    IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4", animationTimeOfMouseOut),
                "MyRadioBox BorderColor " + Uuid);
            Animations.AniStart(
                Animations.AaColor(LabText, TextBlock.ForegroundProperty,
                    IsEnabled ? "ColorBrush1" : "ColorBrushGray4", animationTimeOfMouseOut),
                "MyRadioBox TextColor " + Uuid);
        }
        else
        {
            Animations.AniStop("MyRadioBox BorderColor " + Uuid);
            Animations.AniStop("MyRadioBox TextColor " + Uuid);
            ShapeBorder.SetResourceReference(Shape.StrokeProperty,
                IsEnabled ? Checked ? "ColorBrush2" : "ColorBrush1" : "ColorBrushGray4");
            LabText.SetResourceReference(TextBlock.ForegroundProperty, IsEnabled ? "ColorBrush1" : "ColorBrushGray4");
        }
    }
}

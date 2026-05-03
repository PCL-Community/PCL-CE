using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyTextButton : Label
{
    public delegate void ClickEventHandler(object sender, EventArgs e);

    // 指向动画

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyTextButton), new PropertyMetadata("", (sender, e) =>
        {
            if (Equals(e.OldValue, e.NewValue)) return;
            var button = (MyTextButton)sender;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(button, -button.Opacity, 50),
                    ModAnimation.AaCode(() => button.Content = e.NewValue, After: true),
                    ModAnimation.AaOpacity(button, 1d, 170)
                }, "MyTextButton Text " + button.Uuid);
        }));
    
    private string ColorName;

    // 鼠标事件

    public bool IsMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyTextButton()
    {
        SetResourceReference(ForegroundProperty, "ColorBrush1");
        Background = ModSecret.ColorSemiTransparent;
        PreviewMouseLeftButtonDown += MyTextButton_MouseLeftButtonDown;
        MouseLeave += (_, _) => MyTextButton_MouseLeave();
        PreviewMouseLeftButtonUp += MyTextButton_MouseLeftButtonUp;
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseLeftButtonDown += (_, _) => RefreshColor();
        MouseLeftButtonUp += (_, _) => RefreshColor();
    }

    // 文本

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event ClickEventHandler? Click;

    private void MyTextButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        IsMouseDown = true;
        e.Handled = true;
    }

    private void MyTextButton_MouseLeave()
    {
        IsMouseDown = false;
    }

    private void MyTextButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDown) return;
        IsMouseDown = false;
        ModBase.Log("[Control] 按下文本按钮：" + Text);
        Click?.Invoke(this, null);
        ModMain.RaiseCustomEvent(this);
        e.Handled = true;
    }

    private void RefreshColor()
    {
        // 判断当前颜色
        string ForeName;
        int Time;
        if (IsMouseDown)
        {
            ForeName = "ColorBrush4";
            Time = 30;
        }
        else if (IsMouseOver)
        {
            ForeName = "ColorBrush3";
            Time = AnimationTimeIn;
        }
        else
        {
            ForeName = "ColorBrush1";
            Time = AnimationTimeOut;
        }

        // 重复性验证
        if ((ColorName ?? "") == (ForeName ?? ""))
            return;
        ColorName = ForeName;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(ModAnimation.AaColor(this, ForegroundProperty, ForeName, Time),
                "MyTextButton Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyTextButton Color " + Uuid);
            SetResourceReference(ForegroundProperty, ForeName);
        }
    }
}
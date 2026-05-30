using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyTextButton : Label
{
    public delegate void ClickEventHandler(object sender, EventArgs e);

    // 指向动画

    private const int animationTimeIn = 100;
    private const int animationTimeOut = 200;

    public static readonly DependencyProperty textProperty = DependencyProperty.Register("Text", typeof(string),
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
                }, "MyTextButton Text " + button.uuid);
        }));
    
    private string colorName;

    // 鼠标事件

    public bool isMouseDown;

    // 基础

    public int uuid = ModBase.GetUuid();

    public MyTextButton()
    {
        SetResourceReference(ForegroundProperty, "ColorBrush1");
        Background = ThemeManager.colorSemiTransparent;
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
        get => (string)GetValue(textProperty);
        set => SetValue(textProperty, value);
    }

    public event ClickEventHandler? Click;

    private (string ForeName, int Time) GetVisualState()
    {
        if (isMouseDown)
            return ("ColorBrush4", 30);
        if (IsMouseOver)
            return ("ColorBrush3", animationTimeIn);
        return ("ColorBrush1", animationTimeOut);
    }

    private void MyTextButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        isMouseDown = true;
        e.Handled = true;
    }

    private void MyTextButton_MouseLeave()
    {
        isMouseDown = false;
    }

    private void MyTextButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown) return;
        isMouseDown = false;
        ModBase.Log("[Control] 按下文本按钮：" + Text);
        Click?.Invoke(this, null);
        ModMain.RaiseCustomEvent(this);
        e.Handled = true;
    }

    private void RefreshColor()
    {
        var (ForeName, Time) = GetVisualState();

        // 重复性验证
        if ((colorName ?? "") == (ForeName ?? ""))
            return;
        colorName = ForeName;
        // 触发颜色动画
        ControlVisualHelpers.AnimateColorOrSetResource(this, ForegroundProperty, ForeName, Time,
            "MyTextButton Color " + uuid, ControlVisualHelpers.ShouldAnimate(this));
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyTextButton : Label
{
    public delegate void ClickEventHandler(object sender, EventArgs e);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyTextButton), new PropertyMetadata("", (sender, e) =>
        {
            if (Equals(e.OldValue, e.NewValue)) return;
            ((MyTextButton)sender).Content = e.NewValue;
        }));
    
    private string colorName;

    // 鼠标事件

    public bool isMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

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
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public event ClickEventHandler? Click;

    private string GetVisualState()
    {
        if (isMouseDown)
            return "ColorBrush4";
        if (IsMouseOver)
            return "ColorBrush3";
        return "ColorBrush1";
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
        var foreName = GetVisualState();

        // 重复性验证
        if ((colorName ?? "") == (foreName ?? ""))
            return;
        colorName = foreName;
        SetResourceReference(ForegroundProperty, foreName);
    }
}

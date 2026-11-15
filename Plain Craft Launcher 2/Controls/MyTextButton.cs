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

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyTextButton), new PropertyMetadata("", (sender, e) =>
        {
            if (Equals(e.OldValue, e.NewValue)) return;
            var button = (MyTextButton)sender;
            Animations.AniStart(
                new[]
                {
                    Animations.AaOpacity(button, -button.Opacity, 50),
                    Animations.AaCode(() => button.Content = e.NewValue, after: true),
                    Animations.AaOpacity(button, 1d, 170)
                }, "MyTextButton Text " + button.Uuid);
        }));
    
    private string colorName;

    // 鼠标事件

    public bool isMouseDown;

    // 基础

    public int Uuid = Common.GetUuid();

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(nameof(Command),
        typeof(ICommand), typeof(MyTextButton), new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(MyTextButton), new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

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
        Common.Log("[Control] 按下文本按钮：" + Text);
        Click?.Invoke(this, null);
        Launcher.RaiseCustomEvent(this);
        ExecuteCommand();
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
            "MyTextButton Color " + Uuid, ControlVisualHelpers.ShouldAnimate(this));
    }

    private bool _isCommandRunning;

    private void ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || !command.CanExecute(CommandParameter))
            return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }
}

using PCL.Core.UI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyScrollViewer : ScrollViewer
{
    /// <summary>
    ///     是否启用平滑滚轮动画（逐帧调用 <see cref="ScrollViewer.ScrollToVerticalOffset" />）。<br />
    ///     虚拟化列表中请设为 <see langword="false" />：逐帧改变偏移量会让虚拟化面板每帧都 realize/remove 容器，
    ///     既浪费 CPU 也会让滚动时的容器回收失效。
    /// </summary>
    public static readonly DependencyProperty EnableSmoothWheelProperty =
        DependencyProperty.Register(nameof(EnableSmoothWheel), typeof(bool), typeof(MyScrollViewer),
            new PropertyMetadata(true));

    private double realOffset;

    public MyScrollBar scrollBar;

    public MyScrollViewer()
    {
        PreviewMouseWheel += MyScrollViewer_PreviewMouseWheel;
        ScrollChanged += MyScrollViewer_ScrollChanged;
        IsVisibleChanged += MyScrollViewer_IsVisibleChanged;
        Loaded += (_, _) => Load();
        PreviewGotKeyboardFocus += MyScrollViewer_PreviewGotKeyboardFocus;
    }

    public double DeltaMult { get; set; } = 1d;

    public bool EnableSmoothWheel
    {
        get => (bool)GetValue(EnableSmoothWheelProperty);
        set => SetValue(EnableSmoothWheelProperty, value);
    }

    private void MyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!EnableSmoothWheel)
        {
            // 交回原生像素滚动，避免平滑动画逐帧改变偏移量
            Tooltip.Dismiss();
            return;
        }

        if (e.Delta == 0 || ScrollableHeight <= 0d)
            return;

        var src = e.Source;
        if (Content is FrameworkElement { TemplatedParent: null })
        {
            switch (src)
            {
                case ComboBox { IsDropDownOpen: true }:
                case TextBox { AcceptsReturn: true }:
                case ComboBoxItem:
                case CheckBox:
                    return;
            }
        }

        e.Handled = true;
        PerformVerticalOffsetDelta(-e.Delta);

        Tooltip.Dismiss();
    }

    public void PerformVerticalOffsetDelta(double delta)
    {
        Animations.AniStart(Animations.AaDouble(animDelta =>
        {
            realOffset = Common.MathClamp(realOffset + (double)animDelta!, 0d, ExtentHeight - ActualHeight);
            ScrollToVerticalOffset(realOffset);
        }, delta * DeltaMult, 300, 0, new Animations.AniEaseOutFluent((Animations.AniEasePower)6)));
    }

    private void MyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        realOffset = VerticalOffset;
        if (Launcher.frmMain is not null &&
            (e.VerticalChange != 0 || e.ViewportHeightChange != 0))
            Launcher.frmMain.BtnExtraBack.ShowRefresh();
    }

    private void MyScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Launcher.frmMain!.BtnExtraBack.ShowRefresh();
    }

    private void Load()
    {
        scrollBar = (MyScrollBar)GetTemplateChild("PART_VerticalScrollBar")!;
    }

    private void MyScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is MySlider)
            e.Handled = true; // #3854，阻止获得焦点时自动滚动
    }
}
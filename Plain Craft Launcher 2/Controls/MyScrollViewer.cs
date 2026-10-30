using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.UI.Controls;

namespace PCL;

public class MyScrollViewer : ScrollViewer
{
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

    private void MyScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta == 0 || ScrollableHeight <= 0d)
            return;

        var src = e.Source;
        if (Content is FrameworkElement element && element.TemplatedParent is null)
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
        realOffset = ModBase.MathClamp(realOffset + delta * DeltaMult, 0d, ExtentHeight - ActualHeight);
        ScrollToVerticalOffset(realOffset);
    }

    private void MyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        realOffset = VerticalOffset;
        if (ModMain.frmMain is not null &&
            (e.VerticalChange != 0 || e.ViewportHeightChange != 0))
            ModMain.frmMain.BtnExtraBack.ShowRefresh();
    }

    private void MyScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ModMain.frmMain.BtnExtraBack.ShowRefresh();
    }

    private void Load()
    {
        scrollBar = (MyScrollBar)GetTemplateChild("PART_VerticalScrollBar");
    }

    private void MyScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is MySlider)
            e.Handled = true; // #3854，阻止获得焦点时自动滚动
    }
}
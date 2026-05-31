using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyScrollViewer : ScrollViewer
{
    private readonly string tooltipHideId;


    private double realOffset;

    public MyScrollBar scrollBar;

    public MyScrollViewer()
    {
        tooltipHideId = $"HideTooltip_{GetHashCode()}";
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

        if (Application.showingTooltips.Count > 0)
            foreach (var TooltipBorder in Application.showingTooltips)
                // 建议：如果动画已经在执行，则不再重复触发
                ModAnimation.AniStart(ModAnimation.AaOpacity(TooltipBorder, -1, 100), tooltipHideId);
    }

    public void PerformVerticalOffsetDelta(double delta)
    {
        ModAnimation.AniStart(ModAnimation.AaDouble(animDelta =>
        {
            realOffset = ModBase.MathClamp(realOffset + (double)animDelta, 0d, ExtentHeight - ActualHeight);
            ScrollToVerticalOffset(realOffset);
        }, delta * DeltaMult, 300, 0, new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6), false));
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
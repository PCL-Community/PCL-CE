using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyScrollViewer : ScrollViewer
{
    private readonly string TooltipHideId;


    private double RealOffset;

    public MyScrollBar ScrollBar;

    public MyScrollViewer()
    {
        TooltipHideId = $"HideTooltip_{GetHashCode()}";
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

        if (Application.ShowingTooltips.Count > 0)
            foreach (var TooltipBorder in Application.ShowingTooltips)
                // 建议：如果动画已经在执行，则不再重复触发
                ModAnimation.AniStart(ModAnimation.AaOpacity(TooltipBorder, -1, 100), TooltipHideId);
    }

    public void PerformVerticalOffsetDelta(double Delta)
    {
        ModAnimation.AniStart(ModAnimation.AaDouble(AnimDelta =>
        {
            RealOffset = ModBase.MathClamp(RealOffset + (double)AnimDelta, 0d, ExtentHeight - ActualHeight);
            ScrollToVerticalOffset(RealOffset);
        }, Delta * DeltaMult, 300, 0, new ModAnimation.AniEaseOutFluent((ModAnimation.AniEasePower)6), false));
    }

    private void MyScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        RealOffset = VerticalOffset;
        if (ModMain.FrmMain is not null &&
            (e.VerticalChange != 0 || e.ViewportHeightChange != 0))
            ModMain.FrmMain.BtnExtraBack.ShowRefresh();
    }

    private void MyScrollViewer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        ModMain.FrmMain.BtnExtraBack.ShowRefresh();
    }

    private void Load()
    {
        ScrollBar = (MyScrollBar)GetTemplateChild("PART_VerticalScrollBar");
    }

    private void MyScrollViewer_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is MySlider)
            e.Handled = true; // #3854，阻止获得焦点时自动滚动
    }
}
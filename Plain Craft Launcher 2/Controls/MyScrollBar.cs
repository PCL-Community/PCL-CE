using System.Windows.Controls.Primitives;

namespace PCL;

public class MyScrollBar : ScrollBar
{
    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyScrollBar()
    {
        IsEnabledChanged += (_, _) => RefreshColor();
        GotMouseCapture += (_, _) => RefreshColor();
        LostMouseCapture += (_, _) => RefreshColor();
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        IsVisibleChanged += (_, _) => RefreshColor();
    }

    // 指向
    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            double newOpacity;
            string newColor;
            if (!IsVisible)
            {
                newOpacity = 0d;
                newColor = "ColorBrush4";
            }
            else if (IsMouseCaptureWithin)
            {
                newOpacity = 1d;
                newColor = "ColorBrush4";
            }
            else if (IsMouseOver)
            {
                newOpacity = 0.9d;
                newColor = "ColorBrush3";
            }
            else
            {
                newOpacity = 0.5d;
                newColor = "ColorBrush4";
            }

            SetResourceReference(ForegroundProperty, newColor);
            Opacity = newOpacity;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "滚动条颜色改变出错");
        }
    }
}
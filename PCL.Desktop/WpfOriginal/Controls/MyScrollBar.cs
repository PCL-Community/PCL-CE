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

    // 指向动画

    private void RefreshColor()
    {
        try
        {
            // 判断当前颜色
            double newOpacity;
            string newColor;
            int time;
            if (!IsVisible)
            {
                newOpacity = 0d;
                time = 20; // 防止错误的尺寸判断导致闪烁
                newColor = "ColorBrush4";
            }
            else if (IsMouseCaptureWithin)
            {
                newOpacity = 1d;
                newColor = "ColorBrush4";
                time = 50;
            }
            else if (IsMouseOver)
            {
                newOpacity = 0.9d;
                newColor = "ColorBrush3";
                time = 130;
            }
            else
            {
                newOpacity = 0.5d;
                newColor = "ColorBrush4";
                time = 180;
            }

            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, ForegroundProperty, newColor, time),
                        ModAnimation.AaOpacity(this, newOpacity - Opacity, time)
                    }, "MyScrollBar Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyScrollBar Color " + Uuid);
                SetResourceReference(ForegroundProperty, newColor);
                Opacity = newOpacity;
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "滚动条颜色改变出错");
        }
    }
}
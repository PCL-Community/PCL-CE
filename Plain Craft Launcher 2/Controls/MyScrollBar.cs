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
            double NewOpacity;
            string NewColor;
            int Time;
            if (!IsVisible)
            {
                NewOpacity = 0d;
                Time = 20; // 防止错误的尺寸判断导致闪烁
                NewColor = "ColorBrush4";
            }
            else if (IsMouseCaptureWithin)
            {
                NewOpacity = 1d;
                NewColor = "ColorBrush4";
                Time = 50;
            }
            else if (IsMouseOver)
            {
                NewOpacity = 0.9d;
                NewColor = "ColorBrush3";
                Time = 130;
            }
            else
            {
                NewOpacity = 0.5d;
                NewColor = "ColorBrush4";
                Time = 180;
            }

            // 触发颜色动画
            if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
            {
                // 有动画
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaColor(this, ForegroundProperty, NewColor, Time),
                        ModAnimation.AaOpacity(this, NewOpacity - Opacity, Time)
                    }, "MyScrollBar Color " + Uuid);
            }
            else
            {
                // 无动画
                ModAnimation.AniStop("MyScrollBar Color " + Uuid);
                SetResourceReference(ForegroundProperty, NewColor);
                Opacity = NewOpacity;
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "滚动条颜色改变出错");
        }
    }
}
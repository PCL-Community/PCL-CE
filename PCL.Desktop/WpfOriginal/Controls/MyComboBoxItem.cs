using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyComboBoxItem : ComboBoxItem
{
    // 指向动画

    private const int animationTimeIn = 100;
    private const int animationTimeOut = 300;
    private string backColorName;
    private double fontOpacity;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyComboBoxItem()
    {
        Style = (Style)FindResource("MyComboBoxItem");
        Unselected += (_, _) => RefreshColor();
        MouseMove += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        Selected += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
        MouseLeftButtonUp += MyComboBoxItem_MouseLeftButtonUp;
    }

    private void RefreshColor()
    {
        // 判断当前颜色
        string newBackColorName;
        double newFontOpacity;
        int time;
        if (IsSelected)
        {
            newBackColorName = "ColorBrush6";
            newFontOpacity = 1d;
            time = animationTimeIn;
        }
        else if (IsMouseOver)
        {
            newBackColorName = "ColorBrush8";
            newFontOpacity = 1d;
            time = animationTimeIn;
        }
        else if (IsEnabled)
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 1d;
            time = animationTimeOut;
        }
        else
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 0.4d;
            time = animationTimeOut;
        }

        if ((backColorName ?? "") == (newBackColorName ?? "") && fontOpacity == newFontOpacity)
            return;
        backColorName = newBackColorName;
        fontOpacity = newFontOpacity;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, backColorName, time),
                    ModAnimation.AaOpacity(this, fontOpacity - Opacity, time)
                }, "ComboBoxItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("ComboBoxItem Color " + Uuid);
            SetResourceReference(BackgroundProperty, backColorName);
            Opacity = fontOpacity;
        }
    }

    public override string ToString()
    {
        return Content?.ToString() ?? "";
    }

    public static implicit operator string(MyComboBoxItem value)
    {
        return value.Content?.ToString() ?? "";
    }

    private void MyComboBoxItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ModBase.Log("[Control] 选择下拉列表项：" + ToString());
    }
}
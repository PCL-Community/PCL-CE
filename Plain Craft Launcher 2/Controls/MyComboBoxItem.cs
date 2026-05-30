using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyComboBoxItem : ComboBoxItem
{
    // 指向动画

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 300;
    private string BackColorName;
    private double FontOpacity;

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
        string NewBackColorName;
        double NewFontOpacity;
        int Time;
        if (IsSelected)
        {
            NewBackColorName = "ColorBrush6";
            NewFontOpacity = 1d;
            Time = AnimationTimeIn;
        }
        else if (IsMouseOver)
        {
            NewBackColorName = "ColorBrush8";
            NewFontOpacity = 1d;
            Time = AnimationTimeIn;
        }
        else if (IsEnabled)
        {
            NewBackColorName = "ColorBrushTransparent";
            NewFontOpacity = 1d;
            Time = AnimationTimeOut;
        }
        else
        {
            NewBackColorName = "ColorBrushTransparent";
            NewFontOpacity = 0.4d;
            Time = AnimationTimeOut;
        }

        if ((BackColorName ?? "") == (NewBackColorName ?? "") && FontOpacity == NewFontOpacity)
            return;
        BackColorName = NewBackColorName;
        FontOpacity = NewFontOpacity;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, BackColorName, Time),
                    ModAnimation.AaOpacity(this, FontOpacity - Opacity, Time)
                }, "ComboBoxItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("ComboBoxItem Color " + Uuid);
            SetResourceReference(BackgroundProperty, BackColorName);
            Opacity = FontOpacity;
        }
    }

    public override string ToString()
    {
        return Content?.ToString() ?? "";
    }

    public static implicit operator string(MyComboBoxItem Value)
    {
        return Value.Content?.ToString() ?? "";
    }

    private void MyComboBoxItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ModBase.Log("[Control] 选择下拉列表项：" + ToString());
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public class MyComboBoxItem : ComboBoxItem
{
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
        if (IsSelected)
        {
            newBackColorName = "ColorBrush6";
            newFontOpacity = 1d;
        }
        else if (IsMouseOver)
        {
            newBackColorName = "ColorBrush8";
            newFontOpacity = 1d;
        }
        else if (IsEnabled)
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 1d;
        }
        else
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 0.4d;
        }

        if ((backColorName ?? "") == (newBackColorName ?? "") && fontOpacity == newFontOpacity)
            return;
        backColorName = newBackColorName;
        fontOpacity = newFontOpacity;
        SetResourceReference(BackgroundProperty, backColorName);
        Opacity = fontOpacity;
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
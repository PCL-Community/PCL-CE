using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

public class MyMenuItem : MenuItem
{
    // 指向动画

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;
    private string ColorName;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyMenuItem()
    {
        Loaded += MyMenuItem_Loaded;
        MouseEnter += (_, __) => RefreshColor();
        MouseLeave += (_, __) => RefreshColor();
        IsEnabledChanged += (_, __) => RefreshColor();
    }

    private void MyMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (Icon is not null)
        {
            var IconControl = (Path)GetTemplateChild("Icon");
            if (IconControl is not null)
                IconControl.Data = (Geometry)new GeometryConverter().ConvertFromString(Conversions.ToString(Icon));
            // 对父级设置透明度
        }

        ((ContextMenu)Parent).Opacity = Conversions.ToDouble(
            Operators.AddObject(Operators.DivideObject(ModBase.Setup.Get("UiLauncherTransparent"), 1000), 0.4d));
    }

    private void RefreshColor()
    {
        // 判断当前颜色
        string BackName;
        string ForeName;
        int Time;
        if (!IsEnabled)
        {
            BackName = "ColorBrushTransparent";
            ForeName = "ColorBrushGray5";
            Time = AnimationTimeOut;
        }
        else if (IsMouseOver)
        {
            BackName = "ColorBrush6";
            ForeName = "ColorBrush2";
            Time = AnimationTimeIn;
        }
        else
        {
            BackName = "ColorBrushTransparent";
            ForeName = "ColorBrush1";
            Time = AnimationTimeOut;
        }

        // 重复性验证
        if ((ColorName ?? "") == (BackName ?? ""))
            return;
        ColorName = BackName;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, BackName, Time),
                    ModAnimation.AaColor(this, ForegroundProperty, ForeName, Time)
                }, "MyMenuItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("MyMenuItem Color " + Uuid);
            SetResourceReference(BackgroundProperty, BackName);
            SetResourceReference(ForegroundProperty, ForeName);
        }
    }
}
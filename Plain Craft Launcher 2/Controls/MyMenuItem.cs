using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;

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
        MouseEnter += (_, _) => RefreshColor();
        MouseLeave += (_, _) => RefreshColor();
        IsEnabledChanged += (_, _) => RefreshColor();
    }

    private (string BackName, string ForeName, int Time) GetVisualState()
    {
        if (!IsEnabled)
            return ("ColorBrushTransparent", "ColorBrushGray5", AnimationTimeOut);
        if (IsMouseOver)
            return ("ColorBrush6", "ColorBrush2", AnimationTimeIn);
        return ("ColorBrushTransparent", "ColorBrush1", AnimationTimeOut);
    }

    private void MyMenuItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (Icon is not null)
        {
            var IconControl = (Path)GetTemplateChild("Icon");
            if (IconControl is not null)
                IconControl.Data = (Geometry)new GeometryConverter().ConvertFromString(Icon.ToString());
            // 对父级设置透明度
        }

        ((ContextMenu)Parent).Opacity = Config.Preference.Theme.WindowOpacity / 1000.0 + 0.4;
    }

    private void RefreshColor()
    {
        var (BackName, ForeName, Time) = GetVisualState();

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
    private void MyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ModMain.RaiseCustomEvent(this);
    }
}

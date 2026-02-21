using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.IoC;

namespace PCL.Core.UI.Theme;

public class CatColorResource
{
    private const string ColorPrefix = "ColorObject";
    private const string BrushPrefix = "ColorBrush";

    /// <summary>
    /// 透明度值。
    /// </summary>
    public float Alpha { get; init; } = 255;

    /// <summary>
    /// 红色值。
    /// </summary>
    public required float Red { get; init; }

    /// <summary>
    /// 绿色值。
    /// </summary>
    public required float Green { get; init; }

    /// <summary>
    /// 蓝色值。
    /// </summary>
    public required float Blue { get; init; }

    /// <summary>
    /// 资源后缀。
    /// </summary>
    public required string Suffix { get; init; }

    /// <summary>
    /// 是否应用到 <see cref="SolidColorBrush"/> 资源。
    /// </summary>
    public bool ApplyToBrush { get; init; } = false;

    /// <summary>
    /// 是否应用到 <see cref="Color"/> 资源。
    /// </summary>
    public bool ApplyToColor { get; init; } = false;

    /// <summary>
    /// 应用该颜色到 WPF 资源字典中，仅可在 <see cref="LifecycleState.Loaded"/> 及后面的阶段调用。
    /// </summary>
    public void Apply()
    {
        var res = Lifecycle.CurrentApplication.Resources;
        var color = Color.FromScRgb(Alpha, Red, Green, Blue);
        if (ApplyToColor) res[$"{ColorPrefix}{Suffix}"] = color;
        if (ApplyToBrush) res[$"{BrushPrefix}{Suffix}"] = new SolidColorBrush(color);
    }
}

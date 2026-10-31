using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI.Theme;

public class CatColorResource
{
    private const string ColorPrefix = "ColorObject";
    private const string BrushPrefix = "ColorBrush";

    /// <summary>
    ///     透明度值。
    /// </summary>
    public float Alpha { get; init; } = 255;

    /// <summary>
    ///     红色值。
    /// </summary>
    public required float Red { get; init; }

    /// <summary>
    ///     绿色值。
    /// </summary>
    public required float Green { get; init; }

    /// <summary>
    ///     蓝色值。
    /// </summary>
    public required float Blue { get; init; }

    /// <summary>
    ///     资源后缀。
    /// </summary>
    public required string Suffix { get; init; }

    /// <summary>
    ///     是否应用到画刷资源。
    /// </summary>
    public bool ApplyToBrush { get; init; } = false;

    /// <summary>
    ///     是否应用到颜色资源。
    /// </summary>
    public bool ApplyToColor { get; init; } = false;

    /// <summary>
    ///     应用该颜色到 WPF 资源字典中，仅可在 <see cref="LifecycleState.Loaded" /> 及后面的阶段调用。
    /// </summary>
    public void Apply()
    {
        var backend = PlatformServices.Theme;
        if (backend is null) return;
        if (ApplyToColor)
            backend.SetColorResource($"{ColorPrefix}{Suffix}", Alpha, Red, Green, Blue, false);
        if (ApplyToBrush)
            backend.SetColorResource($"{BrushPrefix}{Suffix}", Alpha, Red, Green, Blue, true);
    }
}
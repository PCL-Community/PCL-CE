namespace PCL.Core.Platform;

/// <summary>
///     主题资源后端。WPF/Avalonia 分别创建各自的颜色与画刷对象。
/// </summary>
public interface IThemeBackend
{
    /// <summary>
    ///     写入一个颜色或画刷资源。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <param name="alpha">ScRGB alpha（0–1）。</param>
    /// <param name="red">ScRGB 红色分量（0–1）。</param>
    /// <param name="green">ScRGB 绿色分量（0–1）。</param>
    /// <param name="blue">ScRGB 蓝色分量（0–1）。</param>
    /// <param name="asBrush">为 <c>true</c> 时写入画刷，否则写入颜色。</param>
    void SetColorResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue,
        bool asBrush);
}
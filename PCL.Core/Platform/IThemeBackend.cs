namespace PCL.Core.Platform;

/// <summary>
///     主题资源后端：由界面层把 Core 计算出的颜色写入自己的资源体系。
/// </summary>
public interface IThemeBackend
{
    /// <summary>写入一个颜色资源（ScRGB 分量，0–1）。</summary>
    void SetColorResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue);

    /// <summary>写入一个画刷资源（ScRGB 分量，0–1）。</summary>
    void SetBrushResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue);
}
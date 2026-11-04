using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     基于 Avalonia 应用资源字典的主题后端：把 ThemeService 计算出的
///     <c>ColorObject*</c> / <c>ColorBrush*</c> 写入 <see cref="Application.Resources" />，
///     界面层通过 DynamicResource 消费同一批键。
/// </summary>
public sealed class ThemeBackend : IThemeBackend
{
    private static bool _reportedDirectWrite;
    private static bool _reportedPendingWrite;

    public void SetColorResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue)
    {
        SetResource(key, _CreateColor(alpha, red, green, blue));
    }

    public void SetBrushResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue)
    {
        SetResource(key, new SolidColorBrush(_CreateColor(alpha, red, green, blue)));
    }

    private static Color _CreateColor(
        float alpha,
        float red,
        float green,
        float blue)
    {
        return Color.FromArgb(
            _ToByte(alpha),
            _ToSrgbByte(red),
            _ToSrgbByte(green),
            _ToSrgbByte(blue));
    }

    private static void SetResource(string key, object value)
    {
        if (Application.Current is { } app)
        {
            app.Resources[key] = value;
            if (!_reportedDirectWrite)
            {
                _reportedDirectWrite = true;
                Trace.WriteLine(
                    "[PCL.Core.UI] 主题资源已直接写入 Application.Resources（Application 已就绪）");
            }

            return;
        }

        if (!_reportedPendingWrite)
        {
            _reportedPendingWrite = true;
            Trace.WriteLine("[PCL.Core.UI] Application 尚未就绪，主题资源暂存到应用容器待回填");
        }

        Lifecycle.CurrentApplication?.SetResource(key, value);
    }

    private static byte _ToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value * 255.0), 0, 255);
    }

    /// <summary>把 ScRGB 线性分量转换为 sRGB 编码分量。</summary>
    private static byte _ToSrgbByte(double linear)
    {
        var encoded = linear <= 0.0031308
            ? 12.92 * linear
            : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
        return _ToByte(encoded);
    }
}
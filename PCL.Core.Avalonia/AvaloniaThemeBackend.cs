using Avalonia;
using Avalonia.Media;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>基于 Avalonia 应用资源字典的主题后端。</summary>
public sealed class AvaloniaThemeBackend : IThemeBackend
{
    public void SetColorResource(
        string key,
        float alpha,
        float red,
        float green,
        float blue,
        bool asBrush)
    {
        var color = Color.FromArgb(
            _ToByte(alpha),
            _ToSrgbByte(red),
            _ToSrgbByte(green),
            _ToSrgbByte(blue));
        object value = asBrush ? new SolidColorBrush(color) : color;

        if (Application.Current is { } app) app.Resources[key] = value;
        else Lifecycle.CurrentApplication?.SetResource(key, value);
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
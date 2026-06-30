using System.Drawing;

namespace PCL;

/// <summary>
///     WPF 与像素尺寸换算。
/// </summary>
public static class DpiUtils
{
    public static readonly int Dpi = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

    public static double GetPixelSize(double wpfSize)
    {
        return wpfSize / 96d * Dpi;
    }

    public static double GetWpfSize(double pixelSize)
    {
        return pixelSize * 96d / Dpi;
    }
}
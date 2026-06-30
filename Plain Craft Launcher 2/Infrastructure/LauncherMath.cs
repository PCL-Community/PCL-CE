namespace PCL;

/// <summary>
///     PCL2 数值与旧颜色插值工具。用于承载 PCL2 数学 API。
/// </summary>
public static class LauncherMath
{
    public static double Clamp(double value, double min, double max)
    {
        return NumberUtils.Clamp(value, min, max);
    }

    public static double Percent(double valueA, double valueB, double percent)
    {
        return NumberUtils.Lerp(valueA, valueB, percent);
    }

    public static MyColor Percent(MyColor valueA, MyColor valueB, double percent)
    {
        return Round(valueA * (1d - percent) + valueB * percent, 6);
    }

    public static MyColor Round(MyColor color, int digits = 0)
    {
        return new MyColor
        {
            a = Math.Round(color.a, digits),
            r = Math.Round(color.r, digits),
            g = Math.Round(color.g, digits),
            b = Math.Round(color.b, digits)
        };
    }
}
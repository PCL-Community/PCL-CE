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

    public static NColor Percent(NColor valueA, NColor valueB, double percent)
    {
        return NColor.Lerp(valueA, valueB, percent);
    }

    public static NColor Round(NColor color, int digits = 0)
    {
        return NColor.Round(color, digits);
    }
}
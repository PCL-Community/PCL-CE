namespace PCL.UI.Controls.Common;

/// <summary>
///     控件层使用的数值辅助方法：通道取整、按比例插值与范围限制。
/// </summary>
public static class MathHelper
{
    /// <summary>
    ///     将一个数字限制为 0~255 的 Byte 值。
    /// </summary>
    public static byte MathByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }

    /// <summary>
    ///     把 MyColor 的四个通道四舍五入到指定小数位。
    /// </summary>
    public static MyColor MathRound(MyColor col, int w = 0)
    {
        return new MyColor
        {
            a = Math.Round(col.a, w),
            r = Math.Round(col.r, w),
            g = Math.Round(col.g, w),
            b = Math.Round(col.b, w)
        };
    }

    /// <summary>
    ///     按 percent 在 valueA 与 valueB 之间线性插值，结果保留 6 位小数。
    /// </summary>
    public static double MathPercent(double valueA, double valueB, double percent)
    {
        return Math.Round(valueA * (1d - percent) + valueB * percent, 6);
    }

    /// <summary>
    ///     按 percent 在两种颜色之间逐通道插值，结果保留 6 位小数。
    /// </summary>
    public static MyColor MathPercent(MyColor valueA, MyColor valueB, double percent)
    {
        return MathRound(valueA * (1d - percent) + valueB * percent, 6);
    }

    /// <summary>
    ///     把 value 限制在 min 与 max 之间。
    /// </summary>
    public static double MathClamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}
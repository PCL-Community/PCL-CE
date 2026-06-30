using System;

namespace PCL.Core.Utils;

/// <summary>
///     通用数值处理工具。
/// </summary>
public static class NumberUtils
{
    /// <summary>
    ///     将浮点数舍入并限制到 <see cref="byte" /> 可表示的 0~255 范围。
    /// </summary>
    public static byte ClampToByte(double value)
    {
        if (double.IsNaN(value)) return 0;
        value = Math.Clamp(value, 0d, 255d);
        return (byte)Math.Round(value);
    }

    /// <summary>
    ///     返回数值符号：正数为 1，负数为 -1，0 或 NaN 为 0。
    /// </summary>
    public static int Sign(double value)
    {
        if (value is 0d or double.NaN) return 0;
        return value > 0d ? 1 : -1;
    }

    /// <summary>
    ///     在两个数值之间做线性插值。
    /// </summary>
    public static double Lerp(
        double start,
        double end,
        double progress,
        int digits = 6)
    {
        var value = start * (1d - progress) + end * progress;
        return digits >= 0 ? Math.Round(value, digits) : value;
    }

    /// <summary>
    ///     将数值限制在指定闭区间。
    /// </summary>
    public static double Clamp(double value, double min, double max)
    {
        return min > max
            ? min
            : Math.Clamp(value, min, max);
    }
}
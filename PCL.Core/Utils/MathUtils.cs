using PCL.Core.UI;
using PCL.Core.Utils.Exts;
using System;
using System.Linq;

namespace PCL.Core.Utils;

public static class MathUtils
{
    /// <summary>
    /// 2~65 进制的转换。
    /// </summary>
    public static string RadixConvert(string input, int fromRadix, int toRadix)
    {
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz/+=";

        // 零与负数的处理
        if (string.IsNullOrEmpty(input))
        {
            return "0";
        }

        var isNegative = input is ['-', ..];
        if (isNegative)
        {
            input = input.TrimStart('-');
        }

        // 转换为十进制
        var realNum = 0L;
        var scale = 1L;
        foreach (var digit in input.Reverse().Select(l => digits.IndexOfF(l.ToString())))
        {
            realNum += digit * scale;
            scale *= fromRadix;
        }

        // 转换为指定进制
        var result = string.Empty;
        while (realNum > 0L)
        {
            var newNum = (int)(realNum % toRadix);
            realNum = (long)Math.Round((realNum - newNum) / (float)toRadix);
            result = digits[newNum] + result;
        }

        // 负数的结束处理与返回
        return (isNegative ? '-' : string.Empty) + result;
    }

    /// <summary>
    /// 计算二阶贝塞尔曲线。
    /// </summary>
    public static double CalcuteBezier(double x, double x1, double y1, double x2, double y2, double acc = 0.01d)
    {
        switch (x)
        {
            case <= 0d:
            case Double.NaN:
                return 0d;
            case >= 1d:
                return 1d;
        }

        double b;
        var a = x;

        do
        {
            b = 3 * a * ((0.33333333 + x1 - x2) * a * a + (x2 - 2 * x1) * a + x1);
            a += (x - b) * 0.5;
        } while (!(Math.Abs(b - x) < acc)); // 精度

        return 3 * a * ((0.33333333 + y1 - y2) * a * a + (y2 - 2 * y1) * a + y1);
    }

    /// <summary>
    /// 将一个数字限制为 0~255 的 Byte 值。
    /// </summary>
    public static byte LimitAsByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }

    /// <summary>
    /// 获取两数间的百分比。小数点精确到 6 位。
    /// </summary>
    /// <returns></returns>
    public static float Percent(float valueA, float valueB, float percent)
    {
        return Round(valueA * (1 - percent) + valueB * percent, 6);
    }

    /// <summary>
    /// 获取两数间的百分比。小数点精确到 6 位。
    /// </summary>
    /// <returns></returns>
    public static double Percent(double valueA, double valueB, double percent)
    {
        return Math.Round(valueA * (1 - percent) + valueB * percent, 6);
    }

    /// <summary>
    /// 获取两颜色间的百分比，根据 RGB 计算。小数点精确到 6 位。
    /// </summary>
    public static MyColor Percent(MyColor valueA, MyColor valueB, double percent)
    {
        return Round(valueA * (1 - percent) + valueB * percent, 6);
    }

    /// <summary>
    /// 符号函数。
    /// </summary>
    public static int Sgn(double value) =>
        value switch
        {
            0d => 0,
            > 0d => 1,
            _ => -1
        };

    #region Round

    /// <summary>
    /// 提供 <see cref="NColor"/> 类型支持的 Round。
    /// </summary>
    public static MyColor Round(MyColor col, int w = 0)
    {
        return new MyColor
        {
            A = Math.Round(col.A, w),
            R = Math.Round(col.R, w),
            G = Math.Round(col.G, w),
            B = Math.Round(col.B, w)
        };
    }

    /// <summary>Rounds a float value to a specified number of fractional digits
    /// using the given midpoint rounding mode.</summary>
    public static float Round(float value, int digits = 0,
                              MidpointRounding mode = MidpointRounding.ToEven)
    {
        // Handle special float values: NaN, Infinity
        if (float.IsNaN(value) || float.IsInfinity(value))
            return value;

        // Rounding to zero digits is a simple, fast path
        if (digits == 0)
            return _RoundInteger(value, mode);

        // For digits != 0, scale, round, then un‑scale
        float scale = MathF.Pow(10, digits);

        // Guard against scale overflow
        if (float.IsInfinity(scale))
        {
            // If scaling would overflow, return the original value
            // (the number is so large that fractional part is insignificant)
            return value;
        }

        float scaled = value * scale;
        float rounded = _RoundInteger(scaled, mode);
        return rounded / scale;
    }

    /// <summary>Rounds a float to the nearest integer using the given
    /// midpoint rounding mode.</summary>
    private static float _RoundInteger(float value, MidpointRounding mode)
    {
        // Always handle special values first
        if (float.IsNaN(value) || float.IsInfinity(value))
            return value;

        switch (mode)
        {
            case MidpointRounding.ToEven:
                return _RoundToEven(value);

            case MidpointRounding.AwayFromZero:
                return _RoundAwayFromZero(value);

            default:
                throw new ArgumentException("Unsupported rounding mode", nameof(mode));
        }
    }

    /// <summary>Banker’s rounding: rounds to nearest, ties to even.</summary>
    private static float _RoundToEven(float value)
    {
        float floor = MathF.Floor(value);
        float fraction = value - floor;

        if (fraction > 0.5f) return floor + 1f;
        if (fraction < 0.5f) return floor;

        // Exact tie: check whether floor is even
        bool isEven = (Math.Abs(floor) % 2) < 1e-7f; // tolerance for floating errors
        return isEven ? floor : floor + 1f;
    }

    /// <summary>Common rounding: rounds to nearest, ties away from zero.</summary>
    private static float _RoundAwayFromZero(float value)
    {
        // For positive numbers, floor(x + 0.5)
        // For negative numbers, ceiling(x - 0.5)
        return value >= 0f
            ? MathF.Floor(value + 0.5f)
            : MathF.Ceiling(value - 0.5f);
    }

    #endregion
}
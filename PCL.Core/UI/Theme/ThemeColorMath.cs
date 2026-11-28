using System;

namespace PCL.Core.UI.Theme;

/// <summary>主题色阶调整量的正反变换。</summary>
internal static class ThemeColorMath
{
    /// <summary>按原有非对称线性插值调整颜色通道。</summary>
    public static double AdjustLinear(double value, double adjustment)
    {
        if (adjustment == 0d) return value;
        value = Math.Clamp(value, 0d, 1d);
        adjustment = Math.Clamp(adjustment, -1d, 1d);
        return adjustment switch
        {
            > 0d => value + (1d - value) * adjustment,
            _ => value + value * adjustment
        };
    }

    /// <summary>从调整后的通道值反推调整量，结果限制在 -1 到 1 之间。</summary>
    public static double InverseAdjustLinear(double value, double adjusted)
    {
        if (value is <= 0d or >= 1d)
            return 0d;
        adjusted = Math.Clamp(adjusted, 0d, 1d);
        var result = adjusted >= value
            ? (adjusted - value) / (1d - value)
            : adjusted / value - 1d;
        return Math.Clamp(result, -1d, 1d);
    }
}
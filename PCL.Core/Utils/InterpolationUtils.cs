using System;

namespace PCL.Core.Utils;

/// <summary>
///     插值和缓动曲线工具。
/// </summary>
public static class InterpolationUtils
{
    /// <summary>
    ///     根据三次贝塞尔控制点求指定 x 位置对应的 y 值。起点为 (0,0)，终点为 (1,1)。
    /// </summary>
    public static double CubicBezierY(
        double x,
        double x1, double y1,
        double x2, double y2,
        double epsilon = 0.000_001d)
    {
        if (double.IsNaN(x) || x <= 0d) return 0d;
        if (x >= 1d) return 1d;

        var low = 0d;
        var high = 1d;
        var t = x;
        for (var i = 0; i < 32; i++)
        {
            t = (low + high) / 2d;
            var currentX = _CubicBezier(t, x1, x2);
            if (Math.Abs(currentX - x) <= epsilon) break;
            if (currentX < x) low = t;
            else high = t;
        }

        return _CubicBezier(t, y1, y2);
    }

    private static double _CubicBezier(double t, double p1, double p2)
    {
        var u = 1d - t;
        return 3d * u * u * t * p1 + 3d * u * t * t * p2 + t * t * t;
    }
}
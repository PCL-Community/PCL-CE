using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace PCL;

/// <summary>
///     支持小数与常见 WPF / Drawing 类型互转的颜色。
/// </summary>
public class MyColor
{
    public double a = 255d;
    public double b;
    public double g;
    public double r;

    public MyColor()
    {
    }

    public MyColor(Color color)
    {
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(string hexString)
    {
        var color = (Color)ColorConverter.ConvertFromString(hexString);
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(double newA, MyColor color)
    {
        a = newA;
        r = color.r;
        g = color.g;
        b = color.b;
    }

    public MyColor(double newR, double newG, double newB)
    {
        a = 255d;
        r = newR;
        g = newG;
        b = newB;
    }

    public MyColor(double newA, double newR, double newG, double newB)
    {
        a = newA;
        r = newR;
        g = newG;
        b = newB;
    }

    public MyColor(Brush brush)
    {
        var color = ((SolidColorBrush)brush).Color;
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(SolidColorBrush brush) : this(brush.Color)
    {
    }

    public MyColor(object? obj)
    {
        switch (obj)
        {
            case null:
                a = 255d;
                r = 255d;
                g = 255d;
                b = 255d;
                break;
            case SolidColorBrush brush:
                var color = brush.Color;
                a = color.A;
                r = color.R;
                g = color.G;
                b = color.B;
                break;
            default:
                a = Convert.ToDouble(((dynamic)obj).A);
                r = Convert.ToDouble(((dynamic)obj).R);
                g = Convert.ToDouble(((dynamic)obj).G);
                b = Convert.ToDouble(((dynamic)obj).B);
                break;
        }
    }

    public static implicit operator MyColor(string value)
    {
        return new MyColor(value);
    }

    public static implicit operator MyColor(Color value)
    {
        return new MyColor(value);
    }

    public static implicit operator MyColor(SolidColorBrush value)
    {
        return new MyColor(value.Color);
    }

    public static implicit operator MyColor(Brush value)
    {
        return new MyColor(value);
    }

    public static implicit operator Color(MyColor value)
    {
        return Color.FromArgb(
            NumberUtils.ClampToByte(value.a),
            NumberUtils.ClampToByte(value.r),
            NumberUtils.ClampToByte(value.g),
            NumberUtils.ClampToByte(value.b));
    }

    public static implicit operator System.Drawing.Color(MyColor value)
    {
        return System.Drawing.Color.FromArgb(
            NumberUtils.ClampToByte(value.a),
            NumberUtils.ClampToByte(value.r),
            NumberUtils.ClampToByte(value.g),
            NumberUtils.ClampToByte(value.b));
    }

    public static implicit operator SolidColorBrush(MyColor value)
    {
        return new SolidColorBrush((Color)value);
    }

    public static implicit operator Brush(MyColor value)
    {
        return new SolidColorBrush((Color)value);
    }

    public static MyColor operator +(MyColor left, MyColor right)
    {
        return new MyColor
        {
            a = left.a + right.a,
            r = left.r + right.r,
            g = left.g + right.g,
            b = left.b + right.b
        };
    }

    public static MyColor operator -(MyColor left, MyColor right)
    {
        return new MyColor
        {
            a = left.a - right.a,
            r = left.r - right.r,
            g = left.g - right.g,
            b = left.b - right.b
        };
    }

    public static MyColor operator *(MyColor left, double right)
    {
        return new MyColor
        {
            a = left.a * right,
            r = left.r * right,
            g = left.g * right,
            b = left.b * right
        };
    }

    public static MyColor operator /(MyColor left, double right)
    {
        return new MyColor
        {
            a = left.a / right,
            r = left.r / right,
            g = left.g / right,
            b = left.b / right
        };
    }

    public static bool operator ==(MyColor? left, MyColor? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.a == right.a && left.r == right.r && left.g == right.g && left.b == right.b;
    }

    public static bool operator !=(MyColor? left, MyColor? right)
    {
        return !(left == right);
    }

    private static double Hue(double value1, double value2, double hue)
    {
        if (hue < 0d) hue += 1d;
        if (hue > 1d) hue -= 1d;
        if (hue < 0.16667d) return value1 + (value2 - value1) * 6d * hue;
        if (hue < 0.5d) return value2;
        if (hue < 0.66667d) return value1 + (value2 - value1) * (4d - hue * 6d);
        return value1;
    }

    public MyColor FromHSL(double sourceHue, double sourceSaturation, double sourceLightness)
    {
        if (sourceSaturation == 0d)
        {
            r = sourceLightness * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            var hue = sourceHue / 360d;
            var saturation = sourceSaturation / 100d;
            var lightness = sourceLightness / 100d;
            saturation = lightness < 0.5d
                ? saturation * lightness + lightness
                : saturation * (1d - lightness) + lightness;
            lightness = 2d * lightness - saturation;
            r = 255d * Hue(lightness, saturation, hue + 1d / 3d);
            g = 255d * Hue(lightness, saturation, hue);
            b = 255d * Hue(lightness, saturation, hue - 1d / 3d);
        }

        a = 255d;
        return this;
    }

    public MyColor FromHSL2(double sourceHue, double sourceSaturation, double sourceLightness)
    {
        if (sourceSaturation == 0d)
        {
            r = sourceLightness * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            sourceHue = (sourceHue + 3600000d) % 360d;
            var centers = new[]
            {
                +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d, -0.09d,
                +0.18d, +0.05d, -0.12d, -0.02d, +0.1d, -0.06d
            };
            var centerIndex = sourceHue / 30d;
            var lowerIndex = (int)Math.Round(Math.Floor(centerIndex));
            var visualCenter = 50d -
                               ((1d - centerIndex + lowerIndex) * centers[lowerIndex] +
                                (centerIndex - lowerIndex) * centers[lowerIndex + 1]) * sourceSaturation;
            sourceLightness = (sourceLightness < visualCenter
                ? sourceLightness / visualCenter
                : 1d + (sourceLightness - visualCenter) / (100d - visualCenter)) * 50d;
            FromHSL(sourceHue, sourceSaturation, sourceLightness);
        }

        a = 255d;
        return this;
    }

    public MyColor Alpha(double value)
    {
        a = value;
        return this;
    }

    public override string ToString()
    {
        return $"({a},{r},{g},{b})";
    }

    public override bool Equals(object? obj)
    {
        return obj is MyColor other && this == other;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(a, r, g, b);
    }
}

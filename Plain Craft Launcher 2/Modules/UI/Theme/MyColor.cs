using System.Windows.Media;

namespace PCL;

/// <summary>
///     支持小数与常见类型隐式转换的颜色。
/// </summary>
public class MyColor
{
    public double a = 255d;
    public double b;
    public double g;
    public double r;

    // 构造函数
    public MyColor()
    {
    }

    public MyColor(Color col)
    {
        a = col.A;
        r = col.R;
        g = col.G;
        b = col.B;
    }

    public MyColor(string hexString)
    {
        var stringColor = (Color)ColorConverter.ConvertFromString(hexString);
        a = stringColor.A;
        r = stringColor.R;
        g = stringColor.G;
        b = stringColor.B;
    }

    public MyColor(double newA, MyColor col)
    {
        a = newA;
        r = col.r;
        g = col.g;
        b = col.b;
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

    public MyColor(SolidColorBrush brush)
    {
        var color = brush.Color;
        a = color.A;
        r = color.R;
        g = color.G;
        b = color.B;
    }

    public MyColor(object obj)
    {
        if (obj is null)
        {
            a = 255d;
            r = 255d;
            g = 255d;
            b = 255d;
        }
        else if (obj is SolidColorBrush brush)
        {
            // 避免反复获取 Color 对象造成性能下降
            var color = brush.Color;
            a = color.A;
            r = color.R;
            g = color.G;
            b = color.B;
        }
        else
        {
            a = Convert.ToDouble(((dynamic)obj).A);
            r = Convert.ToDouble(((dynamic)obj).R);
            g = Convert.ToDouble(((dynamic)obj).G);
            b = Convert.ToDouble(((dynamic)obj).B);
        }
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0d, 255d);
    }

    public static MyColor Lerp(MyColor from, MyColor to, double progress)
    {
        return Round(from * (1d - progress) + to * progress, 6);
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

    // 类型转换
    public static implicit operator MyColor(string str)
    {
        return new MyColor(str);
    }

    public static implicit operator MyColor(Color col)
    {
        return new MyColor(col);
    }

    public static implicit operator Color(MyColor conv)
    {
        return Color.FromArgb(ClampToByte(conv.a), ClampToByte(conv.r), ClampToByte(conv.g), ClampToByte(conv.b));
    }

    public static implicit operator System.Drawing.Color(MyColor conv)
    {
        return System.Drawing.Color.FromArgb(ClampToByte(conv.a), ClampToByte(conv.r), ClampToByte(conv.g),
            ClampToByte(conv.b));
    }

    public static implicit operator MyColor(SolidColorBrush bru)
    {
        return new MyColor(bru.Color);
    }

    public static implicit operator SolidColorBrush(MyColor conv)
    {
        return new SolidColorBrush(Color.FromArgb(ClampToByte(conv.a), ClampToByte(conv.r), ClampToByte(conv.g),
            ClampToByte(conv.b)));
    }

    public static implicit operator MyColor(Brush bru)
    {
        return new MyColor(bru);
    }

    public static implicit operator Brush(MyColor conv)
    {
        return new SolidColorBrush(Color.FromArgb(ClampToByte(conv.a), ClampToByte(conv.r), ClampToByte(conv.g),
            ClampToByte(conv.b)));
    }

    // 颜色运算
    public static MyColor operator +(MyColor a, MyColor b)
    {
        return new MyColor { a = a.a + b.a, b = a.b + b.b, g = a.g + b.g, r = a.r + b.r };
    }

    public static MyColor operator -(MyColor a, MyColor b)
    {
        return new MyColor { a = a.a - b.a, b = a.b - b.b, g = a.g - b.g, r = a.r - b.r };
    }

    public static MyColor operator *(MyColor a, double b)
    {
        return new MyColor { a = a.a * b, b = a.b * b, g = a.g * b, r = a.r * b };
    }

    public static MyColor operator /(MyColor a, double b)
    {
        return new MyColor { a = a.a / b, b = a.b / b, g = a.g / b, r = a.r / b };
    }

    public static bool operator ==(MyColor a, MyColor b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b;
    }

    public static bool operator !=(MyColor a, MyColor b)
    {
        if (a is null && b is null)
            return false;
        if (a is null || b is null)
            return true;
        return !(a.a == b.a && a.r == b.r && a.g == b.g && a.b == b.b);
    }

    // HSL
    public double Hue(double v1, double v2, double vH)
    {
        if (vH < 0d)
            vH += 1d;
        if (vH > 1d)
            vH -= 1d;

        return vH switch
        {
            < 0.16667d => v1 + (v2 - v1) * 6d * vH,
            < 0.5d => v2,
            < 0.66667d => v1 + (v2 - v1) * (4d - vH * 6d),
            _ => v1
        };
    }

    public MyColor FromHsl(double sH, double sS, double sL)
    {
        if (sS == 0d)
        {
            r = sL * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            var h = sH / 360d;
            var s = sS / 100d;
            var l = sL / 100d;
            s = l < 0.5d ? s * l + l : s * (1.0d - l) + l;
            l = 2d * l - s;
            r = 255d * Hue(l, s, h + 1d / 3d);
            g = 255d * Hue(l, s, h);
            b = 255d * Hue(l, s, h - 1d / 3d);
        }

        a = 255d;
        return this;
    }

    public MyColor FromHsl2(double sH, double sS, double sL)
    {
        if (sS == 0d)
        {
            r = sL * 2.55d;
            g = r;
            b = r;
        }
        else
        {
            // 初始化
            sH = (sH + 3600000d) % 360d;
            var cent = new[]
            {
                +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d, -0.09d, +0.18d, +0.05d, -0.12d, -0.02d, +0.1d,
                -0.06d
            }; // 0, 30, 60
            // 90, 120, 150
            // 180, 210, 240
            // 270, 300, 330
            // 最后两位与前两位一致，加是变亮，减是变暗
            // 计算色调对应的亮度片区
            var center = sH / 30.0d;
            var intCenter = (int)Math.Round(Math.Floor(center)); // 亮度片区编号
            center = 50d -
                     ((1d - center + intCenter) * cent[intCenter] + (center - intCenter) * cent[intCenter + 1]) *
                     sS;
            // center = 50 + (cent(intCenter) + (center - intCenter) * (cent(intCenter + 1) - cent(intCenter))) * sS
            sL = (sL < center ? sL / center : 1d + (sL - center) / (100d - center)) * 50d;
            FromHsl(sH, sS, sL);
        }

        a = 255d;
        return this;
    }

    public MyColor Alpha(double sA)
    {
        a = sA;
        return this;
    }

    public override string ToString()
    {
        return "(" + a + "," + r + "," + g + "," + b + ")";
    }

    public override bool Equals(object obj)
    {
        return obj is MyColor other && a == other.a && r == other.r && g == other.g && b == other.b;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(a, r, g, b);
    }
}
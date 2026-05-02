using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Windows.Media;

namespace PCL.Core.UI;

/// <summary>
///     支持小数与常见类型隐式转换的颜色。
/// </summary>
public class MyColor
{
    public double A = 255d;
    public double B;
    public double G;
    public double R;

    // 构造函数
    public MyColor()
    {
    }

    public MyColor(Color col)
    {
        A = col.A;
        R = col.R;
        G = col.G;
        B = col.B;
    }

    public MyColor(string HexString)
    {
        var StringColor = (Color)ColorConverter.ConvertFromString(HexString);
        A = StringColor.A;
        R = StringColor.R;
        G = StringColor.G;
        B = StringColor.B;
    }

    public MyColor(double newA, MyColor col)
    {
        A = newA;
        R = col.R;
        G = col.G;
        B = col.B;
    }

    public MyColor(double newR, double newG, double newB)
    {
        A = 255d;
        R = newR;
        G = newG;
        B = newB;
    }

    public MyColor(double newR, double newG, double newB, double newA)
    {
        A = newA;
        R = newR;
        G = newG;
        B = newB;
    }

    public MyColor(Brush brush)
    {
        var Color = ((SolidColorBrush)brush).Color;
        A = Color.A;
        R = Color.R;
        G = Color.G;
        B = Color.B;
    }

    public MyColor(SolidColorBrush brush)
    {
        var Color = brush.Color;
        A = Color.A;
        R = Color.R;
        G = Color.G;
        B = Color.B;
    }

    public MyColor(object obj)
    {
        if (obj is null)
        {
            A = 255d;
            R = 255d;
            G = 255d;
            B = 255d;
        }
        else if (obj is SolidColorBrush)
        {
            // 避免反复获取 Color 对象造成性能下降
            var Color = ((SolidColorBrush)obj).Color;
            A = Color.A;
            R = Color.R;
            G = Color.G;
            B = Color.B;
        }
        else
        {
            A = Conversions.ToDouble(((dynamic)obj).A);
            R = Conversions.ToDouble(((dynamic)obj).R);
            G = Conversions.ToDouble(((dynamic)obj).G);
            B = Conversions.ToDouble(((dynamic)obj).B);
        }
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
        return Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G), MathByte(conv.B));
    }

    public static implicit operator System.Drawing.Color(MyColor conv)
    {
        return System.Drawing.Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
            MathByte(conv.B));
    }

    public static implicit operator MyColor(SolidColorBrush bru)
    {
        return new MyColor(bru.Color);
    }

    public static implicit operator SolidColorBrush(MyColor conv)
    {
        return new SolidColorBrush(Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
            MathByte(conv.B)));
    }

    public static implicit operator MyColor(Brush bru)
    {
        return new MyColor(bru);
    }

    public static implicit operator Brush(MyColor conv)
    {
        return new SolidColorBrush(Color.FromArgb(MathByte(conv.A), MathByte(conv.R), MathByte(conv.G),
            MathByte(conv.B)));
    }

    // 颜色运算
    public static MyColor operator +(MyColor a, MyColor b)
    {
        return new MyColor { A = a.A + b.A, B = a.B + b.B, G = a.G + b.G, R = a.R + b.R };
    }

    public static MyColor operator -(MyColor a, MyColor b)
    {
        return new MyColor { A = a.A - b.A, B = a.B - b.B, G = a.G - b.G, R = a.R - b.R };
    }

    public static MyColor operator *(MyColor a, double b)
    {
        return new MyColor { A = a.A * b, B = a.B * b, G = a.G * b, R = a.R * b };
    }

    public static MyColor operator /(MyColor a, double b)
    {
        return new MyColor { A = a.A / b, B = a.B / b, G = a.G / b, R = a.R / b };
    }

    public static bool operator ==(MyColor a, MyColor b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    }

    public static bool operator !=(MyColor a, MyColor b)
    {
        if (a == null && b == null)
            return false;
        if (a == null || b == null)
            return true;
        return !(a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B);
    }

    // HSL
    public double Hue(double v1, double v2, double vH)
    {
        if (vH < 0d)
            vH += 1d;
        if (vH > 1d)
            vH -= 1d;
        if (vH < 0.16667d)
            return v1 + (v2 - v1) * 6d * vH;
        if (vH < 0.5d)
            return v2;
        if (vH < 0.66667d)
            return v1 + (v2 - v1) * (4d - vH * 6d);
        return v1;
    }

    public MyColor FromHSL(double sH, double sS, double sL)
    {
        if (sS == 0d)
        {
            R = sL * 2.55d;
            G = R;
            B = R;
        }
        else
        {
            var H = sH / 360d;
            var S = sS / 100d;
            var L = sL / 100d;
            S = L < 0.5d ? S * L + L : S * (1.0d - L) + L;
            L = 2d * L - S;
            R = 255d * Hue(L, S, H + 1d / 3d);
            G = 255d * Hue(L, S, H);
            B = 255d * Hue(L, S, H - 1d / 3d);
        }

        A = 255d;
        return this;
    }

    public MyColor FromHSL2(double sH, double sS, double sL)
    {
        if (sS == 0d)
        {
            R = sL * 2.55d;
            G = R;
            B = R;
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
            FromHSL(sH, sS, sL);
        }

        A = 255d;
        return this;
    }

    public MyColor Alpha(double sA)
    {
        A = sA;
        return this;
    }

    public override string ToString()
    {
        return "(" + A + "," + R + "," + G + "," + B + ")";
    }

    public override bool Equals(object obj)
    {
        return Operators.ConditionalCompareObjectEqual(this, obj, false);
    }

    /// <summary>
    ///     将一个数字限制为 0~255 的 Byte 值。
    /// </summary>
    private static byte MathByte(double d)
    {
        if (d < 0d)
            d = 0d;
        if (d > 255d)
            d = 255d;
        return (byte)Math.Round(Math.Round(d));
    }
}
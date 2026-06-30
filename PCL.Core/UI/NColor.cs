using System;
using System.Numerics;
using System.Windows.Media;
using PCL.Core.App.IoC;

namespace PCL.Core.UI;

// TODO: 内部实现更换成 scRGB 并且增加更多的 From 与 To 方法
// TODO: 实现 IParsable / ISpanParsable 接口

public struct NColor :
    IEquatable<NColor>,
    IAdditionOperators<NColor, NColor, NColor>,
    ISubtractionOperators<NColor, NColor, NColor>,
    IMultiplyOperators<NColor, float, NColor>,
    IDivisionOperators<NColor, float, NColor>
{
    private Vector4 _color;

    public float R
    {
        get => _color.X;
        set => _color.X = value;
    }

    public float G
    {
        get => _color.Y;
        set => _color.Y = value;
    }

    public float B
    {
        get => _color.Z;
        set => _color.Z = value;
    }

    public float A
    {
        get => _color.W;
        set => _color.W = value;
    }

    #region 构造函数

    public NColor()
    {
        _color = new Vector4(0f, 0f, 0f, 255f);
    }

    public NColor(float r, float g, float b, float a = 255f)
    {
        _color = new Vector4(r, g, b, a);
    }

    public NColor(double r, double g, double b, double a = 255d)
        : this((float)r, (float)g, (float)b, (float)a)
    {
    }

    public NColor(Color color) : this(color.R, color.G, color.B, color.A)
    {
    }

    public NColor(System.Drawing.Color color) : this(color.R, color.G, color.B, color.A)
    {
    }

    public NColor(string str)
    {
        try
        {
            var resource = Lifecycle.CurrentApplication.FindResource(str);
            switch (resource)
            {
                case Color color:
                    _color = new Vector4(color.R, color.G, color.B, color.A);
                    return;
                case SolidColorBrush brush:
                    var brushColor = brush.Color;
                    _color = new Vector4(brushColor.R, brushColor.G, brushColor.B, brushColor.A);
                    return;
            }
        }
        catch
        {
            // 忽略
        }

        if (string.IsNullOrWhiteSpace(str))
            throw new ArgumentException("颜色字符串不能为空。", nameof(str));

        var trimmedString = str.Trim();
        if (!trimmedString.StartsWith('#'))
            throw new ArgumentException("颜色字符串必须以 '#' 开头。", nameof(str));

        trimmedString = trimmedString[1..];

        int r, g, b, a;
        switch (trimmedString.Length)
        {
            case 3: // #RGB
                r = Convert.ToInt32($"{trimmedString[0]}{trimmedString[0]}", 16);
                g = Convert.ToInt32($"{trimmedString[1]}{trimmedString[1]}", 16);
                b = Convert.ToInt32($"{trimmedString[2]}{trimmedString[2]}", 16);
                a = 255;
                break;

            case 4: // #RGBA
                r = Convert.ToInt32($"{trimmedString[0]}{trimmedString[0]}", 16);
                g = Convert.ToInt32($"{trimmedString[1]}{trimmedString[1]}", 16);
                b = Convert.ToInt32($"{trimmedString[2]}{trimmedString[2]}", 16);
                a = Convert.ToInt32($"{trimmedString[3]}{trimmedString[3]}", 16);
                break;

            case 6: // #RRGGBB
                r = Convert.ToInt32(trimmedString[..2], 16);
                g = Convert.ToInt32(trimmedString[2..4], 16);
                b = Convert.ToInt32(trimmedString[4..6], 16);
                a = 255;
                break;

            case 8: // #RRGGBBAA
                r = Convert.ToInt32(trimmedString[..2], 16);
                g = Convert.ToInt32(trimmedString[2..4], 16);
                b = Convert.ToInt32(trimmedString[4..6], 16);
                a = Convert.ToInt32(trimmedString[6..8], 16);
                break;

            default:
                throw new ArgumentException($"无效的颜色字符串长度：{trimmedString.Length}。", nameof(str));
        }

        _color = new Vector4(r, g, b, a);
    }

    public NColor(object? obj)
    {
        switch (obj)
        {
            case null:
                _color = new Vector4(255f, 255f, 255f, 255f);
                break;
            case NColor color:
                _color = color._color;
                break;
            case Color color:
                _color = new Vector4(color.R, color.G, color.B, color.A);
                break;
            case System.Drawing.Color color:
                _color = new Vector4(color.R, color.G, color.B, color.A);
                break;
            case SolidColorBrush brush:
                var brushColor = brush.Color;
                _color = new Vector4(brushColor.R, brushColor.G, brushColor.B, brushColor.A);
                break;
            case Brush brush:
                var solidBrush = (SolidColorBrush)brush;
                var solidBrushColor = solidBrush.Color;
                _color = new Vector4(solidBrushColor.R, solidBrushColor.G, solidBrushColor.B, solidBrushColor.A);
                break;
            case string str:
                _color = new NColor(str)._color;
                break;
            default:
                _color = new Vector4(
                    Convert.ToSingle(((dynamic)obj).R),
                    Convert.ToSingle(((dynamic)obj).G),
                    Convert.ToSingle(((dynamic)obj).B),
                    Convert.ToSingle(((dynamic)obj).A));
                break;
        }
    }

    public NColor(float a, NColor color) : this(color.R, color.G, color.B, a)
    {
    }

    public NColor(double a, NColor color) : this(color.R, color.G, color.B, (float)a)
    {
    }

    public NColor(double a, Brush brush) : this(a, (NColor)brush)
    {
    }

    public NColor(double a, SolidColorBrush brush) : this(a, (NColor)brush)
    {
    }

    public NColor(float r, float g, float b) : this(r, g, b, 255f)
    {
    }

    public NColor(SolidColorBrush brush) : this(brush.Color)
    {
    }

    public NColor(Brush brush) : this((SolidColorBrush)brush)
    {
    }

    private NColor(Vector4 v)
    {
        _color = v;
    }

    public static NColor FromArgb(double a, double r, double g, double b)
    {
        return new NColor(r, g, b, a);
    }

    public NColor WithAlpha(double value)
    {
        var color = this;
        color.A = (float)value;
        return color;
    }

    public static NColor Lerp(NColor from, NColor to, double progress)
    {
        var p = (float)progress;
        return Round(from * (1f - p) + to * p, 6);
    }

    public static NColor Round(NColor color, int digits = 0)
    {
        return new NColor(
            Math.Round(color.R, digits),
            Math.Round(color.G, digits),
            Math.Round(color.B, digits),
            Math.Round(color.A, digits));
    }

    #endregion

    #region 运算符重载

    public static NColor operator +(NColor a, NColor b)
    {
        return new NColor(a._color + b._color);
    }

    public static NColor operator -(NColor a, NColor b)
    {
        return new NColor(a._color - b._color);
    }

    public static NColor operator *(NColor a, float b)
    {
        return new NColor(a._color * b);
    }

    public static NColor operator *(NColor a, double b)
    {
        return new NColor(a._color * (float)b);
    }

    public static NColor operator *(float a, NColor b)
    {
        return new NColor(b._color * a);
    }

    public static NColor operator *(double a, NColor b)
    {
        return new NColor(b._color * (float)a);
    }

    public static NColor operator /(NColor a, float b)
    {
        return b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NColor(a._color / b);
    }

    public static NColor operator /(NColor a, double b)
    {
        return b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NColor(a._color / (float)b);
    }

    public static bool operator ==(NColor a, NColor b)
    {
        return a._color == b._color;
    }

    public static bool operator !=(NColor a, NColor b)
    {
        return a._color != b._color;
    }

    #endregion

    #region IEquatable

    public bool Equals(NColor other)
    {
        return _color.Equals(other._color);
    }

    public override bool Equals(object? obj)
    {
        if (obj is NColor color)
            return Equals(color);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B, A);
    }

    #endregion

    #region IParsable / ISpanParsable

    // TODO: 实现 IParsable / ISpanParsable 接口

    #endregion

    #region HSL

    public static NColor FromHsl(double sH, double sS, double sL)
    {
        var color = new NColor();
        if (sS == 0)
        {
            color.R = (float)(sL * 2.55);
            color.G = color.R;
            color.B = color.R;
        }
        else
        {
            var h = sH / 360;
            var s = sS / 100;
            var l = sL / 100;
            s = l < 0.5 ? s * l + l : s * (1.0 - l) + l;
            l = 2 * l - s;
            color.R = (float)(255 * _Hue(l, s, h + 1 / 3.0));
            color.G = (float)(255 * _Hue(l, s, h));
            color.B = (float)(255 * _Hue(l, s, h - 1 / 3.0));
        }

        color.A = 255;
        return color;
    }

    private static readonly double[] _PerceptualCenterOffsets =
    [
        +0.10d, -0.06d, -0.30d, -0.19d,
        -0.15d, -0.24d, -0.32d, -0.09d,
        +0.18d, +0.05d, -0.12d, -0.02d,
        +0.10d
    ];

    public static NColor FromPerceptualHsl(double hue, double saturation, double lightness)
    {
        if (saturation == 0d)
            return FromHsl(hue, saturation, lightness);

        hue %= 360d;
        if (hue < 0d)
            hue += 360d;

        var segmentPosition = hue / 30d;
        var segmentIndex = (int)segmentPosition;
        var segmentBlend = segmentPosition - segmentIndex;

        var centerOffset = _PerceptualCenterOffsets[segmentIndex] +
                           (_PerceptualCenterOffsets[segmentIndex + 1] - _PerceptualCenterOffsets[segmentIndex]) *
                           segmentBlend;

        var visualCenter = 50d - centerOffset * saturation;
        var adjustedLightness = lightness < visualCenter
            ? lightness / visualCenter * 50d
            : (1d + (lightness - visualCenter) / (100d - visualCenter)) * 50d;

        return FromHsl(hue, saturation, adjustedLightness);
    }

    private static double _Hue(double v1, double v2, double vH)
    {
        if (vH < 0) vH += 1;
        if (vH > 1) vH -= 1;
        return vH switch
        {
            < 0.16667 => v1 + (v2 - v1) * 6 * vH,
            < 0.5 => v2,
            < 0.66667 => v1 + (v2 - v1) * (4 - vH * 6),
            _ => v1
        };
    }

    #endregion

    public override string ToString()
    {
        return $"({A},{R},{G},{B})";
    }

    #region 隐式转换

    public static implicit operator Color(NColor color)
    {
        return Color.FromArgb(
            (byte)Math.Clamp(color.A, 0, 255),
            (byte)Math.Clamp(color.R, 0, 255),
            (byte)Math.Clamp(color.G, 0, 255),
            (byte)Math.Clamp(color.B, 0, 255));
    }

    public static implicit operator System.Drawing.Color(NColor color)
    {
        return System.Drawing.Color.FromArgb(
            (byte)Math.Clamp(color.A, 0, 255),
            (byte)Math.Clamp(color.R, 0, 255),
            (byte)Math.Clamp(color.G, 0, 255),
            (byte)Math.Clamp(color.B, 0, 255));
    }

    public static implicit operator Brush(NColor color)
    {
        return new SolidColorBrush(color);
    }

    public static implicit operator SolidColorBrush(NColor color)
    {
        return new SolidColorBrush(color);
    }

    public static implicit operator NColor(Color color)
    {
        return new NColor(color);
    }

    public static implicit operator NColor(System.Drawing.Color color)
    {
        return new NColor(color);
    }

    public static implicit operator NColor(string value)
    {
        return new NColor(value);
    }

    public static implicit operator NColor(Brush brush)
    {
        return new NColor(brush);
    }

    public static implicit operator NColor(SolidColorBrush brush)
    {
        return new NColor(brush);
    }

    #endregion
}
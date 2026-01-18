using System;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using PCL.Core.App;

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

    public NColor(float a, NColor color) : this(color.R, color.G, color.B, a)
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

    private NColor(Vector4 v) => _color = v;

    #endregion

    #region 运算符重载

    public static NColor operator +(NColor a, NColor b) => new(a._color + b._color);
    public static NColor operator -(NColor a, NColor b) => new(a._color - b._color);
    public static NColor operator *(NColor a, float b) => new(a._color * b);

    public static NColor operator /(NColor a, float b) =>
        b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NColor(a._color / b);

    public static bool operator ==(NColor a, NColor b) => a._color == b._color;
    public static bool operator !=(NColor a, NColor b) => a._color != b._color;

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
    
    #region 隐式转换

    public static implicit operator Color(NColor color) =>
        Color.FromArgb(
            (byte)Math.Clamp(color.A, 0, 255),
            (byte)Math.Clamp(color.R, 0, 255),
            (byte)Math.Clamp(color.G, 0, 255),
            (byte)Math.Clamp(color.B, 0, 255));
    public static implicit operator Brush(NColor color) => new SolidColorBrush(color);
    public static implicit operator SolidColorBrush(NColor color) => new(color);

    public static implicit operator NColor(Color color) => new(color);
    public static implicit operator NColor(Brush brush) => new(brush);
    public static implicit operator NColor(SolidColorBrush brush) => new(brush);

    #endregion
}
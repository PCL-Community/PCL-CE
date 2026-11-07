using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Core.UI.Animation;

// TODO(ENG-01): 改用 scRGB 内部表示，补齐颜色转换与 IParsable / ISpanParsable 实现。

/// <summary>
///     支持小数与常见颜色类型隐式转换的颜色值类型。
///     颜色通道使用 0-255 的浮点表示。
/// </summary>
public struct NColor
    : IEquatable<NColor>,
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

    public NColor(
        float r,
        float g,
        float b,
        float a = 255f)
    {
        _color = new Vector4(r, g, b, a);
    }

    public NColor(Color color) : this(color.R, color.G, color.B, color.A)
    {
    }

    public NColor(string str)
    {
        _color = new Vector4(0f, 0f, 0f, 255f);

        if (string.IsNullOrWhiteSpace(str))
            throw new ArgumentException("颜色字符串不能为空。", nameof(str));

        if (_TryFindResource(str) is { } resource)
            switch (resource)
            {
                case Color color:
                    _color = _ToVector(color);
                    return;
                case ISolidColorBrush brush:
                    _color = _ToVector(brush.Color);
                    return;
                case NColor nColor:
                    _color = nColor._color;
                    return;
            }

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

    public NColor(IBrush brush)
    {
        _color = brush is ISolidColorBrush solid
            ? _ToVector(solid.Color)
            : throw new ArgumentException("只支持纯色画刷。", nameof(brush));
    }

    private NColor(Vector4 v)
    {
        _color = v;
    }

    private static Vector4 _ToVector(Color color)
    {
        return new Vector4(color.R, color.G, color.B, color.A);
    }

    private static object? _TryFindResource(string key)
    {
        try
        {
            var application = Application.Current;
            return application is not null
                && application.TryFindResource(key, out var value)
                    ? value
                    : null;
        }
        catch
        {
            // 忽略
            return null;
        }
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

    public static NColor operator /(NColor a, float b)
    {
        return b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NColor(a._color / b);
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

    #region 转换

    /// <summary>
    ///     转换为 <see cref="Color" />。
    /// </summary>
    public Color ToColor()
    {
        return Color.FromArgb(
            (byte)Math.Clamp(A, 0, 255),
            (byte)Math.Clamp(R, 0, 255),
            (byte)Math.Clamp(G, 0, 255),
            (byte)Math.Clamp(B, 0, 255));
    }

    /// <summary>
    ///     转换为纯色画刷。
    /// </summary>
    public SolidColorBrush ToBrush()
    {
        return new SolidColorBrush(ToColor());
    }

    public override string ToString()
    {
        return $"#{(byte)Math.Clamp(A, 0, 255):X2}{(byte)Math.Clamp(R, 0, 255):X2}"
            + $"{(byte)Math.Clamp(G, 0, 255):X2}{(byte)Math.Clamp(B, 0, 255):X2}";
    }

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

    public static implicit operator Color(NColor color)
    {
        return color.ToColor();
    }

    public static implicit operator SolidColorBrush(NColor color)
    {
        return color.ToBrush();
    }

    public static implicit operator NColor(Color color)
    {
        return new NColor(color);
    }

    public static implicit operator NColor(SolidColorBrush brush)
    {
        return new NColor(brush);
    }

    #endregion
}
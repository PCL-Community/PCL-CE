using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Windows.Media;

namespace PCL.Core.UI;

// TODO: 内部实现更换成 scRGB 并且增加更多的 From 与 To 方法

public struct NColor :
    IEquatable<NColor>,
    IAdditionOperators<NColor, NColor, NColor>,
    ISubtractionOperators<NColor, NColor, NColor>,
    IMultiplyOperators<NColor, float, NColor>,
    IDivisionOperators<NColor, float, NColor>,
    ISpanParsable<NColor>
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
        _color = new Vector4(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255),
            Math.Clamp(a, 0, 255));
    }

    public NColor(Color color) : this(color.R, color.G, color.B, a: color.A)
    {
    }

    public NColor(System.Drawing.Color color) : this(color.R, color.G, color.B, color.A)
    {
    }

    public NColor(string hex)
    {
        var color = Parse(hex, null);
        _color = new Vector4(color.R, color.G, color.B, color.A);
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

    #endregion

    #region 隐式转换

    public static implicit operator NColor(Color c) => new(c);
    public static implicit operator NColor(SolidColorBrush b) => new(b.Color);
    public static implicit operator NColor(Brush b) =>
        b is SolidColorBrush sb ? new(sb.Color)
        : throw new InvalidOperationException("不支持渐变或复杂画刷");

    public static implicit operator SolidColorBrush(NColor c) => c.ToSolidColorBrush();
    public static implicit operator Color(NColor c) => c.ToColor();

    #endregion

    #region 运算符重载

    public static NColor operator +(NColor a, NColor b)
    {
        return new NColor(a.R + b.R, a.G + b.G, a.B + b.B, a.A + b.A);
    }

    public static NColor operator -(NColor a, NColor b)
    {
        return new NColor(a.R - b.R, a.G - b.G, a.B - b.B, a.A - b.A);
    }

    public static NColor operator *(NColor a, float b)
    {
        return new NColor(a.R * b, a.G * b, a.B * b, a.A * b);
    }

    public static NColor operator /(NColor a, float b)
    {
        if (b == 0) throw new DivideByZeroException("除数不能为零。");
        return new NColor(a.R / b, a.G / b, a.B / b, a.A / b);
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

    private enum ParseResultFailType
    {
        InvalidLength,
        InvalidFormat,
        CannotBeEmpty
    }

    private record struct NColorParseResult(float R, float G, float B, float A, bool Success = false)
    {
        public ParseResultFailType FailType;

        public static NColorParseResult SetFailed(ParseResultFailType type)
        {
            return new NColorParseResult(0, 0, 0, 0)
            {
                FailType = type
            };
        }
    }

    private static NColorParseResult _Parse(ReadOnlySpan<char> s)
    {
        if (s is " " or "")
            return NColorParseResult.SetFailed(ParseResultFailType.CannotBeEmpty);

        var trimed = s.Trim();
        if (!trimed.StartsWith(['#']))
            return NColorParseResult.SetFailed(ParseResultFailType.InvalidFormat);

        int r, g, b, a;
        switch (trimed.Length)
        {
            case 3: // #RGB
                r = Convert.ToInt32($"{trimed[0]}{trimed[0]}", 16);
                g = Convert.ToInt32($"{trimed[1]}{trimed[1]}", 16);
                b = Convert.ToInt32($"{trimed[2]}{trimed[2]}", 16);
                a = 255;
                break;

            case 4: // #RGBA
                r = Convert.ToInt32($"{trimed[0]}{trimed[0]}", 16);
                g = Convert.ToInt32($"{trimed[1]}{trimed[1]}", 16);
                b = Convert.ToInt32($"{trimed[2]}{trimed[2]}", 16);
                a = Convert.ToInt32($"{trimed[3]}{trimed[3]}", 16);
                break;

            case 6: // #RRGGBB
                r = Convert.ToInt32(trimed[..2].ToString(), 16);
                g = Convert.ToInt32(trimed[2..4].ToString(), 16);
                b = Convert.ToInt32(trimed[4..6].ToString(), 16);
                a = 255;
                break;

            case 8: // #RRGGBBAA
                r = Convert.ToInt32(trimed[..2].ToString(), 16);
                g = Convert.ToInt32(trimed[2..4].ToString(), 16);
                b = Convert.ToInt32(trimed[4..6].ToString(), 16);
                a = Convert.ToInt32(trimed[6..8].ToString(), 16);
                break;

            default:
                return NColorParseResult.SetFailed(ParseResultFailType.InvalidLength);
        }

        return new NColorParseResult(r, g, b, a, true);
    }

    /// <inheritdoc />
    public static NColor Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        var res = _Parse(s);

        if (!res.Success)
        {
            switch (res.FailType)
            {
                case ParseResultFailType.InvalidLength:
                    throw new ArgumentException($"无效的颜色字符串长度：{s.Length}。", nameof(s));
                case ParseResultFailType.InvalidFormat:
                    throw new ArgumentException("颜色字符串必须以 '#' 开头。", nameof(s));
                case ParseResultFailType.CannotBeEmpty:
                    throw new ArgumentException("颜色字符串不能为空。", nameof(s));
            }
        }

        return new NColor(res.R, res.G, res.B, res.A);
    }

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out NColor result)
    {
        if (s is "" or " ")
        {
            result = default;
            return false;
        }

        var res = _Parse(s);
        if (!res.Success)
        {
            result = default;
            return false;
        }

        result = new NColor(res.R, res.G, res.B, res.A);
        return true;
    }

    /// <inheritdoc />
    public static NColor Parse(string s, IFormatProvider? provider)
    {
        return Parse(s.AsSpan(), null);
    }

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out NColor result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;

        }

        var isSuccess = TryParse(s.AsSpan(), null, out var color);
        result = color;
        return isSuccess;
    }

    /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
    public static bool TryParse([NotNullWhen(true)] string? s, out NColor result)
        => TryParse(s, null, out result);

    /// <inheritdoc cref="IParsable{TSelf}.Parse(string?, IFormatProvider?)" />
    public static NColor Parse(string s)
        => Parse(s, null);

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

    private static readonly double[] _Cent = [
    +0.1d, -0.06d, -0.3d,
        -0.19d, -0.15d, -0.24d,
        -0.32d, -0.09d, +0.18d,
        +0.05d, -0.12d, -0.02d,
        +0.1d, -0.06d
];

    public NColor FromHsl2(double sH, double sS, double sL)
    {
        var color = new NColor();

        if (sS == 0d)
        {
            color.R = (float)(sL * 2.55d);
            color.G = color.R;
            color.B = color.R;
        }
        else
        {
            // 初始化
            sH = (sH + 3600000d) % 360d;
            // 90, 120, 150
            // 180, 210, 240
            // 270, 300, 330
            // 最后两位与前两位一致，加是变亮，减是变暗
            // 计算色调对应的亮度片区
            var center = sH / 30.0d;
            var intCenter = (int)Math.Round(Math.Floor(center)); // 亮度片区编号
            center = 50d -
                     ((1d - center + intCenter) * _Cent[intCenter] + (center - intCenter) * _Cent[intCenter + 1]) *
                     sS;
            // center = 50 + (cent(intCenter) + (center - intCenter) * (cent(intCenter + 1) - cent(intCenter))) * sS
            sL = (sL < center ? sL / center : 1d + (sL - center) / (100d - center)) * 50d;
            color = FromHsl(sH, sS, sL);
        }

        color.A = (float)255d;
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

    #region Convert

    public SolidColorBrush ToSolidColorBrush() => new(ToColor());

    public Color ToColor()
    {
        var color = Color.FromArgb(Convert.ToByte(A),
            Convert.ToByte(R),
            Convert.ToByte(G),
            Convert.ToByte(B));

        return color;
    }

    public Brush ToBrush() => ToSolidColorBrush();

    /// <summary>
    /// 从任意对象安全提取 <see cref="NColor"/>。
    /// 支持 <see cref="Color"/>、<see cref="SolidColorBrush"/>、<see cref="Brush"/>、<see cref="NColor"/> 类型。
    /// 传入 <see langword="null"/> 时返回白色。
    /// </summary>
    /// <exception cref="InvalidCastException">当对象类型不受支持时抛出。</exception>
    public static NColor FromObject(object? value)
    {
        return value switch
        {
            null => new NColor(255f, 255f, 255f, 255f),
            NColor n => n,
            Color c => new NColor(c),
            SolidColorBrush sb => new NColor(sb),
            Brush b => new NColor((SolidColorBrush)b),
            _ => throw new InvalidCastException(
                $"Cannot convert {value.GetType().FullName ?? "unknown"} to {nameof(NColor)}. " +
                $"Supported types: {nameof(NColor)}, {nameof(Color)}, {nameof(SolidColorBrush)}, {nameof(Brush)}.")
        };
    }

    #endregion

    /// <inheritdoc />
    public override string ToString() => $"({A}, {R}, {G}, {B})";
}
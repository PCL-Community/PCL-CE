using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Core.UI.Animation;

/// <summary>
///     支持小数与常见颜色类型隐式转换的颜色值类型。
///     内部使用 scRGB 线性浮点表示，公开的 <see cref="R" />、<see cref="G" />、<see cref="B" />、
///     <see cref="A" /> 仍为 0-255 的 sRGB 通道，避免现有调用点改变语义。
/// </summary>
public struct NColor
    : IEquatable<NColor>,
        IAdditionOperators<NColor, NColor, NColor>,
        ISubtractionOperators<NColor, NColor, NColor>,
        IMultiplyOperators<NColor, float, NColor>,
        IDivisionOperators<NColor, float, NColor>,
        ISpanParsable<NColor>
{
    /// <summary>X/Y/Z 为 scRGB 线性 RGB，W 为 0-255 的 alpha。</summary>
    private Vector4 _scRgb;

    #region 通道

    /// <summary>sRGB 编码后的红色通道，名义范围 0-255；动画增量允许超出。</summary>
    public float R
    {
        get => _Encode(_scRgb.X) * 255f;
        set => _scRgb.X = _Decode(value / 255f);
    }

    /// <summary>sRGB 编码后的绿色通道，名义范围 0-255；动画增量允许超出。</summary>
    public float G
    {
        get => _Encode(_scRgb.Y) * 255f;
        set => _scRgb.Y = _Decode(value / 255f);
    }

    /// <summary>sRGB 编码后的蓝色通道，名义范围 0-255；动画增量允许超出。</summary>
    public float B
    {
        get => _Encode(_scRgb.Z) * 255f;
        set => _scRgb.Z = _Decode(value / 255f);
    }

    /// <summary>0-255 的 alpha 通道。</summary>
    public float A
    {
        get => _scRgb.W;
        set => _scRgb.W = value;
    }

    /// <summary>scRGB 线性红色通道，名义范围 0-1；可超出表示 HDR。</summary>
    public float ScR
    {
        get => _scRgb.X;
        set => _scRgb.X = value;
    }

    /// <summary>scRGB 线性绿色通道，名义范围 0-1；可超出表示 HDR。</summary>
    public float ScG
    {
        get => _scRgb.Y;
        set => _scRgb.Y = value;
    }

    /// <summary>scRGB 线性蓝色通道，名义范围 0-1；可超出表示 HDR。</summary>
    public float ScB
    {
        get => _scRgb.Z;
        set => _scRgb.Z = value;
    }

    /// <summary>scRGB 线性 alpha 通道，范围 0-1。</summary>
    public float ScA
    {
        get => _scRgb.W / 255f;
        set => _scRgb.W = value * 255f;
    }

    #endregion

    #region 构造函数

    public NColor()
    {
        _scRgb = new Vector4(0f, 0f, 0f, 255f);
    }

    public NColor(
        float r,
        float g,
        float b,
        float a = 255f)
    {
        _scRgb = new Vector4(
            _Decode(r / 255f),
            _Decode(g / 255f),
            _Decode(b / 255f),
            a);
    }

    public NColor(
        double r,
        double g,
        double b,
        double a = 255d)
        : this((float)r, (float)g, (float)b, (float)a)
    {
    }

    public NColor(Color color) : this(color.R, color.G, color.B, color.A)
    {
    }

    public NColor(string str)
    {
        if (!TryParse(str, CultureInfo.InvariantCulture, out var color))
            throw new ArgumentException("颜色字符串无效。", nameof(str));

        _scRgb = color._scRgb;
    }

    public NColor(float a, NColor color) : this(color.R, color.G, color.B, a)
    {
    }

    public NColor(double a, NColor color) : this((float)a, color)
    {
    }

    public NColor(double a, Color color)
        : this(color.R, color.G, color.B, (float)a)
    {
    }

    public NColor(float r, float g, float b) : this(r, g, b, 255f)
    {
    }

    public NColor(SolidColorBrush? brush) : this(brush?.Color ?? Colors.Transparent)
    {
    }

    public NColor(IBrush? brush)
    {
        _scRgb = brush switch
        {
            null => new Vector4(0f, 0f, 0f, 0f),
            ISolidColorBrush solid => _ToScRgb(solid.Color),
            _ => throw new ArgumentException("只支持纯色画刷。", nameof(brush))
        };
    }

    /// <summary>直接使用 scRGB 线性分量构造，X/Y/Z 为线性 RGB，W 为 0-255 alpha。</summary>
    private NColor(Vector4 scRgb)
    {
        _scRgb = scRgb;
    }

    #endregion

    #region 运算符重载

    public static NColor operator +(NColor a, NColor b)
    {
        return new NColor(
            a.R + b.R,
            a.G + b.G,
            a.B + b.B,
            a.A + b.A);
    }

    public static NColor operator -(NColor a, NColor b)
    {
        return new NColor(
            a.R - b.R,
            a.G - b.G,
            a.B - b.B,
            a.A - b.A);
    }

    public static NColor operator *(NColor a, float b)
    {
        return new NColor(
            a.R * b,
            a.G * b,
            a.B * b,
            a.A * b);
    }

    public static NColor operator /(NColor a, float b)
    {
        return b == 0
            ? throw new DivideByZeroException("除数不能为零。")
            : new NColor(
                a.R / b,
                a.G / b,
                a.B / b,
                a.A / b);
    }

    public static bool operator ==(NColor a, NColor b)
    {
        return a._scRgb == b._scRgb;
    }

    public static bool operator !=(NColor a, NColor b)
    {
        return a._scRgb != b._scRgb;
    }

    #endregion

    #region 颜色空间转换

    /// <summary>
    ///     按 alpha-first 顺序构造颜色，和旧 <c>MyColor(a, r, g, b)</c> 的参数顺序一致。
    /// </summary>
    public static NColor FromArgb(float a, float r, float g, float b)
    {
        return new NColor(r, g, b, a);
    }

    /// <summary>
    ///     按 alpha-first 顺序构造颜色，和旧 <c>MyColor(a, r, g, b)</c> 的参数顺序一致。
    /// </summary>
    public static NColor FromArgb(double a, double r, double g, double b)
    {
        return new NColor((float)r, (float)g, (float)b, (float)a);
    }

    /// <summary>从 scRGB 线性分量构造颜色，alpha 范围 0-1。</summary>
    public static NColor FromScRgb(
        float a,
        float r,
        float g,
        float b)
    {
        return new NColor(new Vector4(r, g, b, a * 255f));
    }

    /// <summary>从 scRGB 线性分量构造颜色，alpha 范围 0-1。</summary>
    public static NColor FromScRgb(
        double a,
        double r,
        double g,
        double b)
    {
        return FromScRgb((float)a, (float)r, (float)g, (float)b);
    }

    /// <summary>转换为 scRGB 线性分量，alpha 范围 0-1。</summary>
    public (float Alpha, float Red, float Green, float Blue) ToScRgb()
    {
        return (_scRgb.W / 255f, _scRgb.X, _scRgb.Y, _scRgb.Z);
    }

    private static Vector4 _ToScRgb(Color color)
    {
        return new Vector4(
            _Decode(color.R / 255f),
            _Decode(color.G / 255f),
            _Decode(color.B / 255f),
            color.A);
    }

    /// <summary>sRGB 编码值转换为线性值；负值按符号保持扩展。</summary>
    private static float _Decode(float srgb)
    {
        var sign = srgb < 0f ? -1f : 1f;
        var value = MathF.Abs(srgb);
        var linear = value <= 0.04045f
            ? value / 12.92f
            : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
        return sign * linear;
    }

    /// <summary>线性值转换为 sRGB 编码值；负值按符号保持扩展。</summary>
    private static float _Encode(float linear)
    {
        var sign = linear < 0f ? -1f : 1f;
        var value = MathF.Abs(linear);
        var srgb = value <= 0.0031308f
            ? 12.92f * value
            : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
        return sign * srgb;
    }

    #endregion

    #region IEquatable

    public bool Equals(NColor other)
    {
        return _scRgb.Equals(other._scRgb);
    }

    public override bool Equals(object? obj)
    {
        return obj is NColor color && Equals(color);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_scRgb.X, _scRgb.Y, _scRgb.Z, _scRgb.W);
    }

    #endregion

    #region 转换

    /// <summary>转换为 <see cref="Color" />。</summary>
    public Color ToColor()
    {
        return Color.FromArgb(
            _ToByte(A),
            _ToSrgbByte(_scRgb.X),
            _ToSrgbByte(_scRgb.Y),
            _ToSrgbByte(_scRgb.Z));
    }

    /// <summary>转换为纯色画刷。</summary>
    public SolidColorBrush ToBrush()
    {
        return new SolidColorBrush(ToColor());
    }

    public override string ToString()
    {
        var color = ToColor();
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
    }

    private static byte _ToSrgbByte(float linear)
    {
        return _ToByte(_Encode(linear) * 255f);
    }

    private static byte _ToByte(float value)
    {
        return (byte)Math.Clamp(MathF.Round(value), 0f, 255f);
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

    public static NColor FromHsl2(double sH, double sS, double sL)
    {
        if (sS == 0d)
        {
            var gray = new NColor
            {
                R = (float)(sL * 2.55d),
                A = 255f
            };
            gray.G = gray.R;
            gray.B = gray.R;
            return gray;
        }

        sH = (sH + 3600000d) % 360d;
        double[] cent =
        [
            +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d,
            -0.09d, +0.18d, +0.05d, -0.12d, -0.02d, +0.1d, -0.06d
        ];
        var center = sH / 30.0d;
        var intCenter = (int)Math.Round(Math.Floor(center));
        center = 50d
            - ((1d - center + intCenter) * cent[intCenter]
                + (center - intCenter) * cent[intCenter + 1])
            * sS;
        sL = (sL < center
                ? sL / center
                : 1d + (sL - center) / (100d - center))
            * 50d;
        return FromHsl(sH, sS, sL);
    }

    public NColor Alpha(double alpha)
    {
        var copy = this;
        copy.A = (float)alpha;
        return copy;
    }

    #endregion

    #region IParsable / ISpanParsable

    public static NColor Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return !TryParse(s, provider, out var result)
            ? throw new FormatException($"无效的颜色字符串：{s}。")
            : result;
    }

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out NColor result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        var text = s.AsSpan().Trim();
        if (text.IsEmpty)
        {
            result = default;
            return false;
        }

        if (text[0] != '#'
            && _TryFindResource(s) is { } resource
            && _TryFromResource(resource, out result))
            return true;

        return _TryParseHex(text, out result);
    }

    public static NColor Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return !TryParse(s, provider, out var result)
            ? throw new FormatException($"无效的颜色字符串：{s.ToString()}。")
            : result;
    }

    public static bool TryParse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider,
        out NColor result)
    {
        var text = s.Trim();
        if (text.IsEmpty)
        {
            result = default;
            return false;
        }

        if (text[0] != '#')
        {
            var resourceKey = text.ToString();
            if (_TryFindResource(resourceKey) is { } resource
                && _TryFromResource(resource, out result))
                return true;
        }

        return _TryParseHex(text, out result);
    }

    private static bool _TryParseHex(ReadOnlySpan<char> text, out NColor result)
    {
        result = default;
        if (text.Length < 2 || text[0] != '#')
            return false;

        var hex = text[1..];
        int r, g, b, a;
        switch (hex.Length)
        {
            case 3: // #RGB
                if (!_TryParseNibble(hex[0], out r)
                    || !_TryParseNibble(hex[1], out g)
                    || !_TryParseNibble(hex[2], out b))
                    return false;
                r *= 17;
                g *= 17;
                b *= 17;
                a = 255;
                break;

            case 4: // #RGBA
                if (!_TryParseNibble(hex[0], out r)
                    || !_TryParseNibble(hex[1], out g)
                    || !_TryParseNibble(hex[2], out b)
                    || !_TryParseNibble(hex[3], out a))
                    return false;
                r *= 17;
                g *= 17;
                b *= 17;
                a *= 17;
                break;

            case 6: // #RRGGBB
                if (!_TryParseByte(hex[..2], out r)
                    || !_TryParseByte(hex[2..4], out g)
                    || !_TryParseByte(hex[4..6], out b))
                    return false;
                a = 255;
                break;

            case 8: // #RRGGBBAA
                if (!_TryParseByte(hex[..2], out r)
                    || !_TryParseByte(hex[2..4], out g)
                    || !_TryParseByte(hex[4..6], out b)
                    || !_TryParseByte(hex[6..8], out a))
                    return false;
                break;

            default:
                return false;
        }

        result = new NColor(r, g, b, a);
        return true;
    }

    private static bool _TryParseByte(ReadOnlySpan<char> text, out int value)
    {
        value = 0;
        if (!_TryParseNibble(text[0], out var high)
            || !_TryParseNibble(text[1], out var low))
            return false;

        value = (high << 4) | low;
        return true;
    }

    private static bool _TryParseNibble(char value, out int nibble)
    {
        switch (value)
        {
            case >= '0' and <= '9':
                nibble = value - '0';
                return true;
            case >= 'a' and <= 'f':
                nibble = value - 'a' + 10;
                return true;
            case >= 'A' and <= 'F':
                nibble = value - 'A' + 10;
                return true;
            default:
                nibble = 0;
                return false;
        }
    }

    private static bool _TryFromResource(object resource, out NColor result)
    {
        switch (resource)
        {
            case Color color:
                result = new NColor(color);
                return true;
            case ISolidColorBrush brush:
                result = new NColor(brush.Color);
                return true;
            case NColor nColor:
                result = nColor;
                return true;
            default:
                result = default;
                return false;
        }
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
            // 解析阶段忽略资源查找异常，后续按十六进制格式继续尝试。
            return null;
        }
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

    public static implicit operator NColor(SolidColorBrush? brush)
    {
        return new NColor(brush);
    }

    #endregion
}
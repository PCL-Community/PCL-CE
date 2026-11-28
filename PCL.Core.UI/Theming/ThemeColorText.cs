using System.Globalization;
using Avalonia.Media;

namespace PCL.Core.UI.Theming;

/// <summary>主题颜色输入格式。</summary>
public enum ThemeColorFormat
{
    Hsv,
    Rgb,
    Hex
}

/// <summary>主题自定义颜色的文本解析、格式化与归一化。</summary>
public static class ThemeColorText
{
    public static bool TryParseNumber(string? text, out double value)
    {
        return double.TryParse(
                text?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
            && double.IsFinite(value);
    }

    public static bool TryParseHex(string text, out Color color)
    {
        color = default;
        text = text.Trim();
        if (text.StartsWith('#'))
            text = text[1..];
        if (text.Length is not (3 or 6))
            return false;

        foreach (var character in text)
            if (!Uri.IsHexDigit(character))
                return false;

        if (text.Length == 3)
            text = string.Concat(text.Select(character => $"{character}{character}"));

        return Color.TryParse($"#{text}", out color);
    }

    public static bool TryParseHsvFields(
        string? hueText,
        string? saturationText,
        string? valueText,
        out Color color,
        out double normalizedHue)
    {
        color = default;
        normalizedHue = 0d;
        if (!TryParseNumber(hueText, out var hue)
            || !TryParseNumber(saturationText, out var saturation)
            || !TryParseNumber(valueText, out var value))
            return false;

        normalizedHue = NormalizeHue(hue);
        color = CreateHsv(normalizedHue, saturation, value);
        return true;
    }

    public static bool TryParseRgbFields(
        string? redText,
        string? greenText,
        string? blueText,
        out Color color)
    {
        color = default;
        if (!TryParseNumber(redText, out var red)
            || !TryParseNumber(greenText, out var green)
            || !TryParseNumber(blueText, out var blue))
            return false;

        color = CreateRgb(red, green, blue);
        return true;
    }

    public static Color CreateHsv(double hue, double saturation, double value)
    {
        return new HsvColor(
            1d,
            NormalizeHue(hue),
            Math.Clamp(saturation, 0d, 100d) / 100d,
            Math.Clamp(value, 0d, 100d) / 100d).ToRgb();
    }

    public static Color CreateRgb(double red, double green, double blue)
    {
        return Color.FromRgb(ToByte(red), ToByte(green), ToByte(blue));
    }

    public static double NormalizeHue(double hue)
    {
        var result = hue % 360d;
        return result < 0d ? result + 360d : result;
    }

    public static byte ToByte(double value)
    {
        return (byte)Math.Round(
            Math.Clamp(value, 0d, 255d),
            MidpointRounding.AwayFromZero);
    }

    public static string FormatHex(Color color)
    {
        return $"{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static (int Hue, int Saturation, int Value) FormatHsv(Color color)
    {
        var hsv = color.ToHsv();
        var hue = (int)Math.Round(hsv.H);
        if (hue >= 360) hue = 0;
        return (
            hue,
            (int)Math.Round(hsv.S * 100d),
            (int)Math.Round(hsv.V * 100d));
    }

    public static (byte Red, byte Green, byte Blue) FormatRgb(Color color)
    {
        return (color.R, color.G, color.B);
    }
}
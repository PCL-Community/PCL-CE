using System;
using System.Globalization;
using System.Windows.Media;

namespace PCL.Core.UI.Controls.SvgIcon;

internal static class SvgPaintParser
{
    private static readonly BrushConverter _BrushConverter = new();

    public static Brush? ParseBrush(string? value, Brush currentColorBrush)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;

        if (normalized.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            return currentColorBrush;

        if (_TryParseRgbFunction(normalized, out var rgbBrush))
            return rgbBrush;

        try
        {
            return _BrushConverter.ConvertFromInvariantString(normalized) as Brush;
        }
        catch
        {
            return currentColorBrush;
        }
    }

    private static bool _TryParseRgbFunction(string value, out Brush brush)
    {
        brush = null!;

        if (!value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return false;

        var start = value.IndexOf('(');
        var end = value.LastIndexOf(')');
        if (start < 0 || end <= start)
            return false;

        var parts = value[(start + 1)..end]
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
            return false;

        var r = _ParseColorComponent(parts[0]);
        var g = _ParseColorComponent(parts[1]);
        var b = _ParseColorComponent(parts[2]);
        var a = parts.Length >= 4 ? _ParseAlpha(parts[3]) : (byte)255;

        if (r is null || g is null || b is null)
            return false;

        brush = new SolidColorBrush(Color.FromArgb(a, r.Value, g.Value, b.Value));
        return true;
    }

    private static byte? _ParseColorComponent(string value)
    {
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return null;

            return (byte)Math.Clamp(Math.Round(percent / 100D * 255D), 0D, 255D);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
            ? (byte)Math.Clamp(Math.Round(raw), 0D, 255D)
            : null;
    }

    private static byte _ParseAlpha(string value)
    {
        if (value.EndsWith('%'))
        {
            if (double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                return (byte)Math.Clamp(Math.Round(percent / 100D * 255D), 0D, 255D);

            return 255;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw)
            ? (byte)Math.Clamp(Math.Round(raw <= 1D ? raw * 255D : raw), 0D, 255D)
            : (byte)255;
    }
}
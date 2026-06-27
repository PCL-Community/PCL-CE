// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

internal static class SvgPaintParser
{
    public static IBrush? ParseBrush(string? value, IBrush currentColorBrush)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized = value.Trim();
        if (normalized.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;

        if (normalized.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            return currentColorBrush;

        if (TryParseRgbFunction(normalized, out IBrush? rgbBrush))
            return rgbBrush;

        try
        {
            return new SolidColorBrush(Color.Parse(normalized));
        }
        catch
        {
            return currentColorBrush;
        }
    }

    private static bool TryParseRgbFunction(string value, out IBrush? brush)
    {
        brush = null;

        if (!value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            return false;

        int start = value.IndexOf('(');
        int end = value.LastIndexOf(')');
        if (start < 0 || end <= start)
            return false;

        string[] parts = value[(start + 1)..end]
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 3)
            return false;

        byte? r = ParseColorComponent(parts[0]);
        byte? g = ParseColorComponent(parts[1]);
        byte? b = ParseColorComponent(parts[2]);
        byte a = parts.Length >= 4 ? ParseAlpha(parts[3]) : (byte)255;

        if (r is null || g is null || b is null)
            return false;

        brush = new SolidColorBrush(Color.FromArgb(a, r.Value, g.Value, b.Value));
        return true;
    }

    private static byte? ParseColorComponent(string value)
    {
        if (value.EndsWith('%'))
        {
            if (!double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                return null;

            return (byte)Math.Clamp(Math.Round(percent / 100d * 255d), 0d, 255d);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw)
            ? (byte)Math.Clamp(Math.Round(raw), 0d, 255d)
            : null;
    }

    private static byte ParseAlpha(string value)
    {
        if (value.EndsWith('%'))
        {
            if (double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                return (byte)Math.Clamp(Math.Round(percent / 100d * 255d), 0d, 255d);

            return 255;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw)
            ? (byte)Math.Clamp(Math.Round(raw <= 1d ? raw * 255d : raw), 0d, 255d)
            : (byte)255;
    }
}

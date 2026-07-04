// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Media;
using PCL.Core.App;
using Wacton.Unicolour;

namespace PCL.Desktop.Theme;

public static class ThemeColorPalette
{
    public const ColorTheme WindowsSystemAccentFallback = ColorTheme.CatBlue;

    private static readonly ToneProfile LightTone = new();
    private static readonly ToneProfile DarkTone = new(
        L1: 0.96d, L2: 0.75d, L3: 0.6d, L4: 0.65d,
        L5: 0.45d, L6: 0.25d, L7: 0.225d, L8: 0.2d,
        LBackground: 0.3d, LForeground: 1d, LWhite: 0.275d);

    public static IReadOnlyDictionary<string, Color> Create(bool isDarkMode, ColorTheme theme)
    {
        Dictionary<string, Color> resources = [];
        ToneProfile tone = isDarkMode ? DarkTone : LightTone;
        ThemeArgs args = GetThemeArgs(NormalizeTheme(theme));

        AddGray(resources, "Gray1", FromLch(tone.L1), applyObject: true);
        AddGray(resources, "Gray2", FromLch(tone.L2), applyObject: true);
        AddGray(resources, "Gray3", FromLch(tone.L3), applyObject: true);
        AddGray(resources, "Gray4", FromLch(tone.L4), applyObject: true);
        AddGray(resources, "Gray5", FromLch(tone.L5), applyObject: true);
        AddGray(resources, "Gray6", FromLch(tone.L6), applyObject: true);
        AddGray(resources, "Gray7", FromLch(tone.L7), applyObject: true);
        AddGray(resources, "Gray8", FromLch(tone.L8), applyObject: true);
        AddBrush(resources, "HalfWhite", FromLch(tone.LWhite, alpha: tone.AHalfWhite));
        AddBrush(resources, "SemiWhite", FromLch(tone.LWhite, alpha: tone.ASemiWhite));
        AddBrush(resources, "White", FromLch(tone.LWhite));
        AddBrush(resources, "Transparent", FromLch(tone.LWhite, alpha: tone.ATransparent));
        AddBrush(resources, "TransparentBackground", FromLch(tone.LBackground, alpha: tone.ABackground));
        AddBrush(resources, "Background", FromLch(tone.LBackground));
        AddBrush(resources, "ToolTip", FromLch(tone.LBackground, alpha: tone.AToolTip));
        AddBrush(resources, "RedBack", FromLch(tone.L7, 0.25d, 30d, tone.AHalfTransparent));
        AddBrush(resources, "Memory", FromLch(tone.LForeground));

        AddThemeColor(resources, "1", Adjust(tone.L1, args.LightAdjust * 0.1d), Adjust(tone.C1, args.ChromaAdjust * 0.25d), args.Hue);
        AddThemeColor(resources, "2", Adjust(tone.L2, args.LightAdjust), Adjust(tone.C2, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "3", Adjust(tone.L3, args.LightAdjust), Adjust(tone.C3, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "4", Adjust(tone.L4, args.LightAdjust), Adjust(tone.C4, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "5", Adjust(tone.L5, args.LightAdjust), Adjust(tone.C5, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "6", Adjust(tone.L6, args.LightAdjust), Adjust(tone.C6, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "7", Adjust(tone.L7, args.LightAdjust), Adjust(tone.C7, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "8", Adjust(tone.L8, args.LightAdjust), Adjust(tone.C8, args.ChromaAdjust), args.Hue);
        AddBrush(resources, "SemiTransparent",
            FromLch(Adjust(tone.L8, args.LightAdjust), Adjust(tone.C8, args.ChromaAdjust), args.Hue, tone.ASemiTransparent));
        AddThemeColor(resources, "Bg0", Adjust(tone.L5, args.LightAdjust), Adjust(tone.C5, args.ChromaAdjust), args.Hue);
        AddThemeColor(resources, "Bg1", Adjust(tone.L7, args.LightAdjust), Adjust(tone.C7, args.ChromaAdjust), args.Hue, tone.ASemiWhite);

        AddBrush(resources, "RedLight", Color.Parse("#ff4c4c"));
        AddBrush(resources, "RedDark", Color.Parse("#ce2111"));
        AddBrush(resources, "Fatal", Color.Parse("#c23616"));
        AddBrush(resources, "Error", Color.Parse("#e74c3c"));
        AddBrush(resources, "Warn", Color.Parse("#f39c12"));
        AddBrush(resources, "InfoDark", Color.Parse("#ffffff"));
        AddBrush(resources, "Info", Color.Parse("#000000"));
        AddBrush(resources, "Debug", Color.Parse("#95a5a6"));
        resources["ColorObjectMsgBoxShadow"] = Color.Parse("#3c3c3c");

        return resources;
    }

    public static ColorTheme NormalizeTheme(ColorTheme theme) =>
        theme == ColorTheme.SystemAccent ? WindowsSystemAccentFallback : theme;

    private static void AddGray(Dictionary<string, Color> resources, string suffix, Color color, bool applyObject)
    {
        if (applyObject)
            resources[$"ColorObject{suffix}"] = color;
        AddBrush(resources, suffix, color);
    }

    private static void AddThemeColor(
        Dictionary<string, Color> resources,
        string suffix,
        double lightness,
        double chroma,
        double hue,
        double alpha = 1d)
    {
        Color color = FromLch(lightness, chroma, hue, alpha);
        resources[$"ColorObject{suffix}"] = color;
        AddBrush(resources, suffix, color);
    }

    private static void AddBrush(Dictionary<string, Color> resources, string suffix, Color color) =>
        resources[$"ColorBrush{suffix}"] = color;

    private static Color FromLch(double lightness, double chroma = 0d, double hue = 0d, double alpha = 1d)
    {
        Unicolour color = new(ColourSpace.Oklch, lightness, chroma, hue, alpha);
        Unicolour mapped = color.MapToRgbGamut(GamutMap.OklchChromaReduction);
        var (red, green, blue) = mapped.RgbLinear;
        return Color.FromArgb(
            ToByte(mapped.Alpha.A),
            ToByte(LinearToSrgb(red)),
            ToByte(LinearToSrgb(green)),
            ToByte(LinearToSrgb(blue)));
    }

    private static ThemeArgs GetThemeArgs(ColorTheme theme) =>
        theme switch
        {
            ColorTheme.SkyBlue => new(235d, 0.36d, 0.2d),
            ColorTheme.CatBlue => new(255d, 0d, -0.2d),
            ColorTheme.DeathBlue => new(268d, -0.05d, -0.1d),
            ColorTheme.HmclBlue => new(275d, -0.03d, -0.35d),
            _ => new(255d, 0d, -0.2d)
        };

    private static double Adjust(double value, double adjustment)
    {
        value = Math.Clamp(value, 0d, 1d);
        adjustment = Math.Clamp(adjustment, -1d, 1d);
        return adjustment > 0d
            ? value + (1d - value) * adjustment
            : value + value * adjustment;
    }

    private static double LinearToSrgb(double value)
    {
        value = Math.Clamp(value, 0d, 1d);
        return value <= 0.0031308d
            ? 12.92d * value
            : 1.055d * Math.Pow(value, 1d / 2.4d) - 0.055d;
    }

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0d, 1d) * 255d);

    private sealed record ThemeArgs(double Hue, double LightAdjust, double ChromaAdjust);

    private sealed record ToneProfile(
        double L1 = 0.35d,
        double L2 = 0.5d,
        double L3 = 0.575d,
        double L4 = 0.65d,
        double L5 = 0.8d,
        double L6 = 0.92d,
        double L7 = 0.94d,
        double L8 = 0.96d,
        double LWhite = 1d,
        double LForeground = 0d,
        double LBackground = 0.995d,
        double C1 = 0.025d,
        double C2 = 0.188d,
        double C3 = 0.213d,
        double C4 = 0.168d,
        double C5 = 0.093d,
        double C6 = 0.036d,
        double C7 = 0.028d,
        double C8 = 0.018d,
        double ASemiWhite = 0.733d,
        double AHalfWhite = 0.333d,
        double ASemiTransparent = 0.004d,
        double AHalfTransparent = 0.5d,
        double ATransparent = 0d,
        double ABackground = 0.824d,
        double AToolTip = 0.9d);
}

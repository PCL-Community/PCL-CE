using System;

namespace PCL.Core.UI.Theme;

/// <summary>一套自定义主题的颜色参数。参数继续沿用 <see cref="ThemeService" /> 的色阶生成方式， 不直接保存用户选择的 RGB 值。</summary>
public sealed record ThemeCustomColor
{
    /// <summary>基础色相，0-359。</summary>
    public int Hue { get; init; } = 180;

    /// <summary>色度缩放，-1 到 1；0 表示保持 <see cref="ToneProfile" /> 的原始色度。</summary>
    public double ChromaScale { get; init; } = -0.2d;

    /// <summary>明度偏移，-1 到 1；0 表示保持 <see cref="ToneProfile" /> 的原始明度。</summary>
    public double LightnessBias { get; init; }

    /// <summary>顶部标题栏渐变的色相幅度，-90 到 90；0 表示不产生渐变色相变化。</summary>
    public int HueSpread { get; init; }

    /// <summary>把旧版四个独立参数转换为当前记录。</summary>
    public static ThemeCustomColor FromLegacy(
        int hue,
        int saturation,
        int lightness,
        int hueDelta)
    {
        return new ThemeCustomColor
        {
            Hue = _NormalizeHue(hue),
            ChromaScale = Math.Clamp(saturation / 100d - 1d, -1d, 1d),
            LightnessBias = Math.Clamp((lightness - 20) / 100d, -1d, 1d),
            HueSpread = Math.Clamp(hueDelta - 90, -90, 90)
        };
    }

    /// <summary>用 sRGB 颜色反推当前模式下的主题参数。无彩色时使用 <paramref name="fallbackHue" />。</summary>
    public static ThemeCustomColor FromSrgb(
        byte red,
        byte green,
        byte blue,
        ToneProfile tone,
        int hueSpread,
        int fallbackHue)
    {
        var color = OklabColor.FromRgb(red, green, blue);
        var hue = color.Chroma < 0.0001d
            ? fallbackHue
            : (int)Math.Round(color.Hue);

        return new ThemeCustomColor
        {
            Hue = _NormalizeHue(hue),
            ChromaScale = ThemeColorMath.InverseAdjustLinear(tone.C3, color.Chroma),
            LightnessBias = ThemeColorMath.InverseAdjustLinear(tone.L3, color.L),
            HueSpread = Math.Clamp(hueSpread, -90, 90)
        }.Normalize();
    }

    /// <summary>生成用于颜色选择器的参考颜色，对应色阶中的主强调色。</summary>
    public OklabColor ToReferenceColor(ToneProfile tone)
    {
        var normalized = Normalize();
        return OklabColor.FromLch(
            ThemeColorMath.AdjustLinear(tone.L3, normalized.LightnessBias),
            ThemeColorMath.AdjustLinear(tone.C3, normalized.ChromaScale),
            normalized.Hue);
    }

    /// <summary>生成顶部标题栏渐变的三段颜色。</summary>
    public (OklabColor Start, OklabColor Middle, OklabColor End) ToTitleBarColors(
        ToneProfile tone)
    {
        var normalized = Normalize();
        var lightness = ThemeColorMath.AdjustLinear(tone.L3, normalized.LightnessBias);
        var chroma = ThemeColorMath.AdjustLinear(tone.C3, normalized.ChromaScale);
        return (
            OklabColor.FromLch(lightness, chroma, normalized.Hue - normalized.HueSpread),
            OklabColor.FromLch(lightness, chroma, normalized.Hue),
            OklabColor.FromLch(lightness, chroma, normalized.Hue + normalized.HueSpread));
    }

    /// <summary>把各字段限制在有效范围内。</summary>
    public ThemeCustomColor Normalize()
    {
        return this with
        {
            Hue = _NormalizeHue(Hue),
            ChromaScale = Math.Clamp(ChromaScale, -1d, 1d),
            LightnessBias = Math.Clamp(LightnessBias, -1d, 1d),
            HueSpread = Math.Clamp(HueSpread, -90, 90)
        };
    }

    private static int _NormalizeHue(int hue)
    {
        return (hue % 360 + 360) % 360;
    }
}
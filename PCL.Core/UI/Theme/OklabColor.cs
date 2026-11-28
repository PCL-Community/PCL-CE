using System;
using Wacton.Unicolour;

namespace PCL.Core.UI.Theme;

/// <summary>Color definition, cast and other utilities based on OKLAB color space.</summary>
public class OklabColor
{
    /// <summary>
    /// <see cref="Unicolour" /> instance for extended use.<br /> Please avoid using it.
    /// Use <see cref="OklabColor" /> implementations instead.
    /// </summary>
    private Unicolour _UnicolourInstance { get; }

    private Oklab _Oklab
    {
        get => field ??= _UnicolourInstance.Oklab;
    } = null!;

    private Oklch _Oklch
    {
        get => field ??= _UnicolourInstance.Oklch;
    } = null!;

    /// <summary>Light channel.</summary>
    public double L => _Oklab.L;

    /// <summary>A channel.</summary>
    public double A => _Oklab.A;

    /// <summary>B channel.</summary>
    public double B => _Oklab.B;

    /// <summary>OKLCH chroma.</summary>
    public double Chroma => _Oklch.C;

    /// <summary>OKLCH hue, in degrees.</summary>
    public double Hue => _Oklch.H;

    /// <summary>Alpha channel.</summary>
    public double Alpha { get; }

    public override int GetHashCode()
    {
        return HashCode.Combine(L, A, B, Alpha);
    }

    // ReSharper disable CompareOfFloatsByEqualityOperator
    public override bool Equals(object? obj)
    {
        return obj is OklabColor c && c.L == L && c.A == A && c.B == B && c.Alpha == Alpha;
    }

    private OklabColor(
        double l,
        double a,
        double b,
        double alpha)
    {
        Alpha = alpha;
        _UnicolourInstance = new Unicolour(ColourSpace.Oklab, l, a, b, alpha);
    }

    private OklabColor(Unicolour instance)
    {
        Alpha = instance.Alpha.A;
        _UnicolourInstance = instance;
    }

    /// <summary>
    /// Create a <see cref="OklabColor" /> instance from <see cref="Unicolour" />
    /// instance.
    /// </summary>
    internal static OklabColor CreateInternal(Unicolour instance)
    {
        return new OklabColor(instance);
    }

    /// <summary>Create a <see cref="OklabColor" /> instance from OKLCH values.</summary>
    public static OklabColor FromLch(
        double l,
        double c = 0,
        double h = 0,
        double alpha = 1.0)
    {
        var unicolour = new Unicolour(ColourSpace.Oklch, l, c, h, alpha);
        return CreateInternal(unicolour);
    }

    /// <summary>
    /// Create a <see cref="OklabColor" /> instance from RGB values.<br /> <b>NOTE</b>:
    /// The alpha value is a number typed double, not byte.
    /// </summary>
    public static OklabColor FromRgb(
        byte r,
        byte g,
        byte b,
        double alpha = 1.0)
    {
        var unicolour = new Unicolour(ColourSpace.Rgb255, r, g, b, alpha);
        return CreateInternal(unicolour);
    }

    /// <summary>ScRGB 映射模式.</summary>
    public enum ScRgbMappingMode
    {
        /// <summary>禁用，可能导致色彩不准确</summary>
        Disable = -1,

        /// <summary>截断到 sRGB 范围内，可能导致某些较为鲜艳的颜色变灰</summary>
        Clip = GamutMap.RgbClipping,

        /// <summary>在 OKLCH 色彩定义下降低色度 (Chroma) 到 sRGB 范围内</summary>
        ReduceChroma = GamutMap.OklchChromaReduction,

        /// <summary>在 WXY 色彩定义下降低纯度 (Purity) 到 sRGB 范围内</summary>
        ReducePurity = GamutMap.WxyPurityReduction
    }

    /// <summary>
    /// Set conversion mapping of <see cref="ToScRgb" /> method and <see cref="ScRgb" />
    /// property.
    /// </summary>
    public static ScRgbMappingMode ScRgbMapping { get; set; } = ScRgbMappingMode.ReduceChroma;

    /// <summary>
    /// Convert to ScRGB values.<br /> <b>NOTE</b>: Use <see cref="ScRgb" /> property for
    /// better performance.
    /// </summary>
    /// <seealso cref="ScRgbMapping" />
    public (float Alpha, float Red, float Green, float Blue) ToScRgb()
    {
        var a = _UnicolourInstance.Alpha.A;
        var instance = ScRgbMapping == ScRgbMappingMode.Disable
            ? _UnicolourInstance
            : _UnicolourInstance.MapToRgbGamut((GamutMap)ScRgbMapping);
        var (r, g, b) = instance.RgbLinear;
        return ((float)a, (float)r, (float)g, (float)b);
    }

    private bool _isScRgbInitialized;

    /// <summary>Cached ScRGB values.</summary>
    public (float Alpha, float Red, float Green, float Blue) ScRgb
    {
        get
        {
            if (_isScRgbInitialized) return field;
            _isScRgbInitialized = true;
            return field = ToScRgb();
        }
    } = default;

    /// <summary>Convert to <see cref="CatColorResource" />.</summary>
    /// <param name="suffix">See <see cref="CatColorResource.Suffix" /></param>
    /// <param name="applyToColor">See <see cref="CatColorResource.ApplyToColor" /></param>
    /// <param name="applyToBrush">See <see cref="CatColorResource.ApplyToBrush" /></param>
    public CatColorResource ToCatColor(
        string suffix,
        bool applyToColor = true,
        bool applyToBrush = true)
    {
        var (a, r, g, b) = ScRgb;
        return new CatColorResource
        {
            Red = r,
            Green = g,
            Blue = b,
            Alpha = a,
            ApplyToBrush = applyToBrush,
            ApplyToColor = applyToColor,
            Suffix = suffix
        };
    }
}
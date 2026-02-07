using System;
using Wacton.Unicolour;

namespace PCL.Core.UI.Theme;

/// <summary>
/// Color definition, cast and other utilities based on OKLAB color space.
/// </summary>
public class LabColor
{
    /// <summary>
    /// <see cref="Unicolour"/> instance for extended use.<br/>
    /// Please avoid using it. Use <see cref="LabColor"/> implementations instead.
    /// </summary>
    internal Unicolour UnicolourInstance { get; }

    private Oklab _Oklab { get => field ??= UnicolourInstance.Oklab; } = null!;

    /// <summary>
    /// Light channel.
    /// </summary>
    public double L => _Oklab.L;

    /// <summary>
    /// A channel.
    /// </summary>
    public double A => _Oklab.A;

    /// <summary>
    /// B channel.
    /// </summary>
    public double B => _Oklab.B;

    /// <summary>
    /// Alpha channel.
    /// </summary>
    public double Alpha { get; }

    public override int GetHashCode() => HashCode.Combine(L, A, B, Alpha);

    // ReSharper disable CompareOfFloatsByEqualityOperator
    public override bool Equals(object? obj) => (obj is LabColor c) && c.L == L && c.A == A && c.B == B && c.Alpha == Alpha;

    private LabColor(double l, double a, double b, double alpha)
    {
        Alpha = alpha;
        UnicolourInstance = new Unicolour(ColourSpace.Oklab, l, a, b, alpha);
    }

    private LabColor(Unicolour instance)
    {
        Alpha = instance.Alpha.A;
        UnicolourInstance = instance;
    }

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from <see cref="Unicolour"/> instance.<br/>
    /// Please avoid using it. Use <see cref="LabColor.Create(double, double, double, double)"/> instead.
    /// </summary>
    internal static LabColor CreateInternal(Unicolour instance) => new(instance);

    /// <summary>
    /// Create a <see cref="LabColor"/> instance.
    /// </summary>
    public static LabColor Create(double l, double a, double b, double alpha = 1.0) => new(l, a, b, alpha);

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from OKLCH values.
    /// </summary>
    public static LabColor FromLch(double l, double c = 0, double h = 0, double alpha = 1.0)
    {
        var unicolour = new Unicolour(ColourSpace.Oklch, l, c, h, alpha);
        return CreateInternal(unicolour);
    }

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from RGB values.<br/>
    /// <b>NOTE</b>: The alpha value is a number typed double, not byte.
    /// </summary>
    public static LabColor FromRgb(byte r, byte g, byte b, double alpha = 1.0)
    {
        var unicolour = new Unicolour(ColourSpace.Rgb255, r, g, b, alpha);
        return CreateInternal(unicolour);
    }

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from ScRGB values.
    /// </summary>
    public static LabColor FromRgb(double r, double g, double b, double alpha = 1.0)
    {
        var unicolour = new Unicolour(ColourSpace.RgbLinear, r, g, b, alpha);
        return CreateInternal(unicolour);
    }

    /// <summary>
    /// Create a <see cref="LabColor"/> instance from <see cref="System.Windows.Media.Color"/>.
    /// </summary>
    public static LabColor FromWpfColor(System.Windows.Media.Color color)
        => FromRgb(color.ScR, color.ScG, color.ScB, color.ScA);

    public static implicit operator LabColor(System.Windows.Media.Color color) => FromWpfColor(color);

    /// <summary>
    /// ScRGB 映射模式.
    /// </summary>
    public enum ScRgbMappingMode
    {
        /// <summary>
        /// 禁用，可能导致色彩不准确
        /// </summary>
        Disable = -1,
        /// <summary>
        /// 截断到 sRGB 范围内，可能导致某些较为鲜艳的颜色变灰
        /// </summary>
        Clip = GamutMap.RgbClipping,
        /// <summary>
        /// 在 OKLCH 色彩定义下降低色度 (Chroma) 到 sRGB 范围内
        /// </summary>
        ReduceChroma = GamutMap.OklchChromaReduction,
        /// <summary>
        /// 在 WXY 色彩定义下降低纯度 (Purity) 到 sRGB 范围内
        /// </summary>
        ReducePurity = GamutMap.WxyPurityReduction,
    }

    /// <summary>
    /// Set conversion mapping of <see cref="ToScRgb"/> method and <see cref="ScRgb"/> property.
    /// </summary>
    public static ScRgbMappingMode ScRgbMapping { get; set; } = ScRgbMappingMode.ReduceChroma;

    /// <summary>
    /// Convert to ScRGB values.<br/>
    /// <b>NOTE</b>: Use <see cref="ScRgb"/> property for better performance.
    /// </summary>
    /// <seealso cref="ScRgbMapping"/>
    public (float Alpha, float Red, float Green, float Blue) ToScRgb()
    {
        var a = UnicolourInstance.Alpha.A;
        var instance = (ScRgbMapping == ScRgbMappingMode.Disable)
            ? UnicolourInstance
            : UnicolourInstance.MapToRgbGamut((GamutMap)ScRgbMapping);
        var (r, g, b) = instance.RgbLinear;
        return ((float)a, (float)r, (float)g, (float)b);
    }

    private bool _isScRgbInitialized = false;

    /// <summary>
    /// Cached ScRGB values.
    /// </summary>
    public (float Alpha, float Red, float Green, float Blue) ScRgb
    {
        get
        {
            if (_isScRgbInitialized) return field;
            _isScRgbInitialized = true;
            return field = ToScRgb();
        }
    } = default;

    /// <summary>
    /// Convert to <see cref="System.Windows.Media.Color"/>.
    /// </summary>
    public System.Windows.Media.Color ToWpfColor()
    {
        var (a, r, g, b) = ScRgb;
        return System.Windows.Media.Color.FromScRgb(a, r, g, b);
    }

    public static implicit operator System.Windows.Media.Color(LabColor lab) => lab.ToWpfColor();

    /// <summary>
    /// Convert to <see cref="CatColorResource"/>.
    /// </summary>
    /// <param name="suffix">See <see cref="CatColorResource.Suffix"/></param>
    /// <param name="applyToColor">See <see cref="CatColorResource.ApplyToColor"/></param>
    /// <param name="applyToBrush">See <see cref="CatColorResource.ApplyToBrush"/></param>
    public CatColorResource ToCatColor(string suffix, bool applyToColor = true, bool applyToBrush = true)
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

    internal static Unicolour MixInternal(LabColor c1, LabColor c2, double c1Amount = 0.5)
        => c1.UnicolourInstance.Mix(c2.UnicolourInstance, ColourSpace.Oklab, c1Amount);

    /// <summary>
    /// Mix two colors.
    /// </summary>
    /// <param name="c1">Color 1</param>
    /// <param name="c2">Color 2</param>
    /// <param name="c1Amount">The amount of color 1 in mixture (0.0~1.0), as color 2 takes the rest</param>
    /// <returns></returns>
    public static LabColor Mix(LabColor c1, LabColor c2, double c1Amount = 0.5) => CreateInternal(MixInternal(c1, c2, c1Amount));

    /// <summary>
    /// Mix two colors, see <see cref="Mix"/>.
    /// </summary>
    public static LabColor operator +(LabColor c1, LabColor c2) => Mix(c1, c2);
}

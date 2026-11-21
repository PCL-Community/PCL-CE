using Avalonia;
using Avalonia.Media;

namespace PCL.UI.Composition;

public sealed class BlurMaterial : MaterialBase
{
    public static readonly StyledProperty<Color> TintProperty =
        AvaloniaProperty.Register<BlurMaterial, Color>(nameof(Tint), Color.FromRgb(32, 32, 32));

    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<BlurMaterial, double>(nameof(TintOpacity), 0.72, coerce: (_, value) => Math.Clamp(value, 0, 1));

    public static readonly StyledProperty<double> NoiseProperty =
        AvaloniaProperty.Register<BlurMaterial, double>(nameof(Noise), 0.025, coerce: (_, value) => Math.Clamp(value, 0, 1));

    public Color Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public double TintOpacity
    {
        get => GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public double Noise
    {
        get => GetValue(NoiseProperty);
        set => SetValue(NoiseProperty, value);
    }
}

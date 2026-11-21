using Avalonia;
using Avalonia.Media;

namespace PCL.UI.Composition;

public sealed class MicaMaterial : MaterialBase
{
    public static readonly StyledProperty<MicaVariant> VariantProperty =
        AvaloniaProperty.Register<MicaMaterial, MicaVariant>(nameof(Variant));

    public static readonly StyledProperty<MaterialThemeMode> ModeProperty =
        AvaloniaProperty.Register<MicaMaterial, MaterialThemeMode>(nameof(Mode));

    public static readonly StyledProperty<Color> TintProperty =
        AvaloniaProperty.Register<MicaMaterial, Color>(nameof(Tint), Colors.Transparent);

    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<MicaMaterial, double>(nameof(TintOpacity), 0, coerce: (_, value) => Math.Clamp(value, 0, 1));

    public MicaVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public MaterialThemeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

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
}

public enum MicaVariant
{
    Base,
    Alt,
}

public enum MaterialThemeMode
{
    Auto,
    Light,
    Dark,
}

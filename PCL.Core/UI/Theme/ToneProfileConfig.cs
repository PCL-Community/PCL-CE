namespace PCL.Core.UI.Theme;

public record ToneProfileConfig
{
    public static readonly ToneProfile DefaultLight = new();

    public static readonly ToneProfile DefaultDark = new(
        L1: 0.96, L2: 0.75, L3: 0.6, L4: 0.65,
        L5: 0.45, L6: 0.25, L7: 0.225, L8: 0.2,
        LBackground: 0.3, LForeground: 1, LWhite: 0.275
    );

    public ToneProfile Light { get => field ?? DefaultLight; init; } = null!;

    public ToneProfile Dark { get => field ?? DefaultDark; init; } = null!;
}

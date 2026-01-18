using System.Text.Json.Serialization;

namespace PCL.Core.UI;

[JsonSerializable(typeof(GrayProfileConfig))]
public record GrayProfileConfig
{
    [JsonPropertyName("enabled")]
    public bool IsEnabled { get; init; } = false;
    
    [JsonPropertyName("profile_light")]
    public GrayProfile Light { get; init; } = new()
    {
        L1 = 25, L2 = 45, L3 = 55, L4 = 65,
        L5 = 80, L6 = 91, L7 = 95, L8 = 97,
        G1 = 100, G2 = 98, G3 = 0,
        Sa0 = 1, Sa1 = 1, LaN = 0.5
    };
    
    [JsonPropertyName("profile_dark")]
    public GrayProfile Dark { get; init; } = new()
    {
        L1 = 96, L2 = 75, L3 = 60, L4 = 65,
        L5 = 45, L6 = 25, L7 = 22, L8 = 20,
        G1 = 15, G2 = 20, G3 = 100,
        Sa0 = 1, Sa1 = 0.4, LaP = 0.75, LaN = 0.75
    };

    public GrayProfile CurrentProfile(bool isDarkMode) => isDarkMode ? Dark : Light;
}

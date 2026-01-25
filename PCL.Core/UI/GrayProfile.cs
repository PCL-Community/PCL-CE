using System.Text.Json.Serialization;

namespace PCL.Core.UI;

[JsonSerializable(typeof(GrayProfile))]
public record GrayProfile
{
    [JsonPropertyName("light_c1")] public int L1 { get; init; }
    [JsonPropertyName("light_c2")] public int L2 { get; init; }
    [JsonPropertyName("light_c3")] public int L3 { get; init; }
    [JsonPropertyName("light_c4")] public int L4 { get; init; }
    [JsonPropertyName("light_c5")] public int L5 { get; init; }
    [JsonPropertyName("light_c6")] public int L6 { get; init; }
    [JsonPropertyName("light_c7")] public int L7 { get; init; }
    [JsonPropertyName("light_c8")] public int L8 { get; init; }
    [JsonPropertyName("light_g1")] public int G1 { get; init; }
    [JsonPropertyName("light_g2")] public int G2 { get; init; }
    [JsonPropertyName("light_g3")] public int G3 { get; init; }

    private int? _lb0; [JsonPropertyName("light_b0")] public int Lb0 { get => _lb0 ?? L5; init => _lb0 = value; }
    private int? _lb1; [JsonPropertyName("light_b1")] public int Lb1 { get => _lb1 ?? L7; init => _lb1 = value; }

    [JsonPropertyName("lightadjust_positive")] public double LaP { get; init; } = 1;
    [JsonPropertyName("lightadjust_negative")] public double LaN { get; init; } = 1;

    [JsonPropertyName("sat_0")] public double Sa0 { get; init; }
    [JsonPropertyName("sat_1")] public double Sa1 { get; init; }

    [JsonPropertyName("alpha_background")] public double Ab { get; init; } = 0xBE;
    [JsonPropertyName("alpha_semi_transparent")] public double Ast { get; init; } = 0x1;
    [JsonPropertyName("alpha_transparent")] public double At { get; init; } = 0x0;
    [JsonPropertyName("alpha_half_white")] public double Ahw { get; init; } = 0x55;
    [JsonPropertyName("alpha_semi_white")] public double Asw { get; init; } = 0xDB;
    [JsonPropertyName("alpha_tooltip_background")] public double Atb { get; init; } = 0xE5;
    [JsonPropertyName("alpha_sidebar_background")] public double Asb { get; init; } = 0xD2;
}

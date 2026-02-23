using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.IdentityModel.Yggdrasil;

public record YggdrasilCredential
{
    [JsonPropertyName("username")] public required string User { get; init; }
    [JsonPropertyName("password")] public required string Password { get; init; }
    [JsonPropertyName("agent")] public required Agent Agent = new();
    [JsonPropertyName("requestUser")] public bool RequestUser { get; set; }
}

public record YggdrasilAutnenticationResult
{
    [JsonPropertyName("accessToken")] public required string AccessToken { get; set; }
    [JsonPropertyName("clientToken")] public required string ClientToken { get; set; }
}
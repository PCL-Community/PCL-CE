using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

namespace PCL.CE.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;

public record JsonWebKeys
{
    [JsonPropertyName("keys")] public required JsonWebKey[] Keys;
}
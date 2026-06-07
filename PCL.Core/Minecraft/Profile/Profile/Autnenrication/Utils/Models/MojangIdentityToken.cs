using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Autnenrication.Utils.Models;

public class MojangIdentityToken
{
    [JsonPropertyName("identityToken")]
    public required string IdentityToken { get; set; }
}
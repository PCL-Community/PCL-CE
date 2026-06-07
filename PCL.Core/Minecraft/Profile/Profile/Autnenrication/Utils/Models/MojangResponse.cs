using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Autnenrication.Utils.Models;

public record MojangResponse
{
    [JsonPropertyName("username")]
    public required string UserName { get; set; }
}
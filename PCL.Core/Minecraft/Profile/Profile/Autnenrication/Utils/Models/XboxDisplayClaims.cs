using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Autnenrication.Utils.Models;

public record XboxDisplayClaims
{
    [JsonPropertyName("xui")]
    public required List<XboxUserHash> Xui { get; set; }
}
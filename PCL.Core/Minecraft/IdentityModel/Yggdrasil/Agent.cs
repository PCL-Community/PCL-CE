using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.IdentityModel.Yggdrasil;

public record Agent
{
    [JsonPropertyName("name")] public string Name { get; init; } = "minecraft";
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
}
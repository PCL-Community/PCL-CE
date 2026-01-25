using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Instance.Interface;

public interface IJsonBasedInstance {
    JsonObject? VersionJson { get; }

    JsonObject? VersionJsonInJar { get; }
}

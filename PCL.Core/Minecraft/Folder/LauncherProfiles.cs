using System.Collections.Generic;

namespace PCL.Core.Minecraft.Folder;

// 定义 launcher_profiles.json 的数据模型
public record LauncherProfiles {
    public Dictionary<string, Profile> Profiles { get; init; } = new();
    public string SelectedProfile { get; init; } = string.Empty;
    public string ClientToken { get; init; } = string.Empty;
}

public record Profile {
    public string Icon { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string LastVersionId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string LastUsed { get; init; } = string.Empty;
}

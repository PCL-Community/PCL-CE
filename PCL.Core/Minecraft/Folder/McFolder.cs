using System;
using PCL.Core.Minecraft.Instance;

namespace PCL.Core.Minecraft.Folder;

/// <summary>
/// Represents a Minecraft folder with its name, path, and type.
/// </summary>
public record McFolder(string Name, string Path, McFolderType Type) {
    public override string ToString() => Path;

    /// <summary>
    /// 当前文件夹下的实例列表。
    /// </summary>
    public InstanceManager? InstanceList { get; set; }

    public virtual bool Equals(McFolder? other) =>
        other is not null && Name == other.Name && Path == other.Path && Type == other.Type;

    public override int GetHashCode() => HashCode.Combine(Name, Path, Type);
}

public enum McFolderType {
    Original,
    RenamedOriginal,
    Custom
}

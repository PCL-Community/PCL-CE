namespace PCL;

/// <summary>内嵌模组的轻量序列化节点（供缓存与后续依赖分析使用）。</summary>
public class EmbeddedModNode
{
    public string? FileName { get; set; }
    public string? ModId { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Loader { get; set; }
    public string? TargetMcVersion { get; set; }

    /// <summary>本内嵌 mod 的全部依赖声明；同一 ID 的不同范围/可选性分别保留。</summary>
    public List<EmbeddedDependency> DependencyRows { get; set; } = [];

    /// <summary>本内嵌 mod 声明的冲突关系（对方 ModId + 生效版本约束 + 是否硬冲突）。</summary>
    public List<EmbeddedConflict> Conflicts { get; set; } = [];

    /// <summary>本内嵌 mod 额外提供的别名 id（multi-mod 兄弟 / Fabric provides）。</summary>
    public List<string> ProvidedIds { get; set; } = [];

    /// <summary>具有独立版本的别名（主要是 Forge/NeoForge multi-mod JAR 的兄弟 mod）。</summary>
    public Dictionary<string, string> ProvidedVersions { get; set; } = new();

    public string? JijIdentifier { get; set; }
    public string? JijVersionRange { get; set; }
    public string? JijArtifactVersion { get; set; }
    public List<EmbeddedModNode> Children { get; set; } = [];
}

public class EmbeddedDependency
{
    public string? Id { get; set; }
    public string? Raw { get; set; }
    public bool Optional { get; set; }
}

public class EmbeddedConflict
{
    public string? DeclarerId { get; set; }
    public string? Target { get; set; }
    public string? Raw { get; set; }
    public bool Hard { get; set; }
}
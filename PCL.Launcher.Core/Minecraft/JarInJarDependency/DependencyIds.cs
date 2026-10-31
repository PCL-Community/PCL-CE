namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）依赖状态。</summary>
public enum JijDepStatus
{
    Installed,
    Disabled,
    Bundled,
    VersionMismatch,
    Missing
}

/// <summary>加载器与运行时平台伪依赖的统一判定。</summary>
public static class DependencyIds
{
    private static readonly HashSet<string> LoaderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "forge",
        "neoforge",
        "fabricloader",
        "quilt",
        "quilt_loader",
        "java",
        "mcp"
    };

    public static bool IsLoaderId(string? id)
    {
        return id is not null && LoaderIds.Contains(id);
    }

    public static bool IsPlatform(string? id)
    {
        return string.Equals(id, "minecraft", StringComparison.OrdinalIgnoreCase)
            || (id is not null && LoaderIds.Contains(id));
    }
}
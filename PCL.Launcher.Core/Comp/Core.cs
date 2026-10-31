using PCL.Core.App.Localization;

namespace PCL;

public static partial class Comp
{
    public enum CompDepsInstallTypes
    {
        /// <summary>
        ///     无法解析依赖
        /// </summary>
        Unresolved = 0,

        /// <summary>
        ///     用户选择安装前置
        /// </summary>
        WithDeps = 1,

        /// <summary>
        ///     用户选择只安装本体，不安装前置
        /// </summary>
        WithoutDeps = 2,

        /// <summary>
        ///     用户取消安装
        /// </summary>
        Cancel = 3
    }

    public enum CompLoaderType
    {
        /// <summary>
        ///     模组加载器
        /// </summary>
        Any = 0,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Forge = 1,

        /// <summary>
        ///     模组加载器
        /// </summary>
        LiteLoader = 3,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Fabric = 4,

        /// <summary>
        ///     模组加载器
        /// </summary>
        Quilt = 5,

        /// <summary>
        ///     模组加载器
        /// </summary>
        NeoForge = 6,

        /// <summary>
        ///     材质包
        /// </summary>
        Minecraft = 7,

        /// <summary>
        ///     光影包
        /// </summary>
        Canvas = 8,

        /// <summary>
        ///     光影包
        /// </summary>
        Iris = 9,

        /// <summary>
        ///     光影包
        /// </summary>
        OptiFine = 10,

        /// <summary>
        ///     光影包
        /// </summary>
        Vanilla = 11,

        /// <summary>
        ///     LabyMod 客户端
        /// </summary>
        LabyMod = 12
    }

    /// <summary>
    ///     搜索结果排序方式
    /// </summary>
    public enum CompSortType
    {
        /// <summary>
        ///     默认
        /// </summary>
        Default = 1,

        /// <summary>
        ///     相关性 (CurseForge Name (4) / Modrinth relevance)
        /// </summary>
        Relevance = 2,

        /// <summary>
        ///     下载量 (CurseForge TotalDownloads (6) / Modrinth downloads)
        /// </summary>
        Downloads = 3,

        /// <summary>
        ///     关注量 (CurseForge Popularity (2) / Modrinth follows)
        /// </summary>
        Follows = 4,

        /// <summary>
        ///     最新发布 (CurseForge ReleasedDate (11) / Modrinth newest)
        /// </summary>
        Newest = 5,

        /// <summary>
        ///     最近更新 (CurseForge LastUpdated (3) / Modrinth updated)
        /// </summary>
        Updated = 6
    }

    [Flags]
    public enum CompSourceType
    {
        CurseForge = 1,
        Modrinth = 2,
        Any = CurseForge | Modrinth
    }

    public enum CompType
    {
        /// <summary>
        ///     允许任意种类，或种类未知。
        /// </summary>
        Any = -1,

        /// <summary>
        ///     Mod。
        /// </summary>
        Mod = 0,

        /// <summary>
        ///     整合包。
        /// </summary>
        ModPack = 1,

        /// <summary>
        ///     资源包。
        /// </summary>
        ResourcePack = 2,

        /// <summary>
        ///     光影包。
        /// </summary>
        Shader = 3,

        /// <summary>
        ///     CurseForge：数据包。
        ///     Modrinth：数据包，或数据包与 Mod 的混合。
        /// </summary>
        DataPack = 4,

        /// <summary>
        ///     服务端插件。
        /// </summary>
        Plugin = 5,

        /// <summary>
        ///     投影原理图。
        /// </summary>
        Schematic = 6,

        /// <summary>
        ///     世界。
        /// </summary>
        World = 7
    }

    public enum DownloadReason
    {
        Standalone,
        Dependency,
        ModPack,
        Update
    }

    /// <summary>
    ///     将原始游戏版本标识转换为 <see cref="CompFile.GameVersions" /> 中的语言中立形式。<br />
    ///     预发布版本的完整标识（例如 <c>1.21-pre2</c>）会被保留，以便进行精确的兼容性判断；
    ///     远古版本（如 <c>b1.x</c>）会统一替换为 <see cref="CompFile.AncientGameVersion" />。
    ///     对已经是语言中立形式的值重复调用不会改变结果。
    /// </summary>
    /// <param name="version">原始或已规范化的游戏版本标识。</param>
    /// <returns>用于存储/比较的语言中立版本标识。</returns>
    public static string NormalizeGameVersion(string? version)
    {
        if (string.IsNullOrEmpty(version))
            return string.Empty;
        if (version[0] == '\0')
            return version;
        if (string.Equals(
                version,
                McInstanceInfo.UnknownVanillaName,
                StringComparison.OrdinalIgnoreCase))
            return CompFile.UnknownGameVersion;
        if (version.StartsWith("b1.", StringComparison.OrdinalIgnoreCase))
            return CompFile.AncientGameVersion;
        return version;
    }

    /// <summary>
    ///     将支持的游戏版本标识转换为当前语言下的显示文本。<br />
    ///     <see cref="CompFile.GameVersions" /> 中保存的是语言中立的标识，
    ///     需要在界面上展示时应调用本方法，以便切换语言后重新求值。
    /// </summary>
    /// <param name="version">语言中立的版本标识，例如 <c>1.20.1</c>、<c>1.20.1-pre</c>、<c>21w15a</c>。</param>
    public static string FormatGameVersion(string? version)
    {
        if (string.IsNullOrEmpty(version) || version == CompFile.UnknownGameVersion)
            return Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion");
        if (version == CompFile.AncientGameVersion)
            return Lang.Text("Download.Comp.Detail.CompItem.AncientVersion");
        return version.Replace(
            CompFile.PreviewVersionSuffix,
            Lang.Text("Download.Comp.Detail.CompItem.PreviewSuffix"),
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     将可能带有加载器前缀或分组标记的版本标识转换为当前语言下的显示文本。<br />
    ///     与 <see cref="FormatGameVersion" /> 的区别在于它会替换字符串中任意位置的标记，
    ///     适用于 <c>Forge 1.20.1-pre</c> 这类拼接后的分组键。
    /// </summary>
    /// <param name="versionKey">分组键或版本标识。</param>
    public static string FormatGameVersionInKey(string? versionKey)
    {
        return string.IsNullOrEmpty(versionKey)
            ? Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion")
            : versionKey
                .Replace(
                    CompFile.UnknownGameVersion,
                    Lang.Text("Download.Comp.Detail.CompItem.UnknownVersion"),
                    StringComparison.Ordinal)
                .Replace(
                    CompFile.AncientGameVersion,
                    Lang.Text("Download.Comp.Detail.CompItem.AncientVersion"),
                    StringComparison.Ordinal)
                .Replace(
                    CompFile.PreviewVersionSuffix,
                    Lang.Text("Download.Comp.Detail.CompItem.PreviewSuffix"),
                    StringComparison.Ordinal);
    }

    /// <summary>
    ///     将标签资源键列表转换为当前语言下的显示文本列表，用于本地搜索等场景。<br />
    ///     资源键本身对用户不可见，直接拼接键名会导致搜索失效。
    /// </summary>
    /// <param name="tagKeys">标签资源键列表。</param>
    public static IEnumerable<string> GetLocalizedTags(IEnumerable<string>? tagKeys)
    {
        return tagKeys is null
            ? []
            : tagKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(Lang.Text)
                .Where(text => !string.IsNullOrEmpty(text));
    }

    /// <summary>
    ///     去掉 <see cref="CompFile.GameVersions" /> 中的预发布标记，得到可与其他版本字符串比较的基础版本号。
    /// </summary>
    /// <param name="version">语言中立的版本标识。</param>
    public static string StripGameVersionPreview(string? version)
    {
        return string.IsNullOrEmpty(version)
            ? ""
            : version.EndsWith(CompFile.PreviewVersionSuffix, StringComparison.Ordinal)
                ? version[..^CompFile.PreviewVersionSuffix.Length]
                : version;
    }

    /// <summary>
    ///     判断游戏版本列表是否包含指定版本。<br />
    ///     双方都会先通过 <see cref="NormalizeGameVersion" /> 规范化为语言中立形式，
    ///     因此可以直接用实例的 <c>VanillaName</c> 或存储键进行比较。
    /// </summary>
    /// <param name="versions">语言中立的版本标识列表。</param>
    /// <param name="version">要查找的版本，例如实例的 <c>VanillaName</c>。</param>
    public static bool ContainsGameVersion(IEnumerable<string>? versions, string? version)
    {
        if (versions is null) return false;
        var normalized = NormalizeGameVersion(version);
        if (normalized.Length == 0) return false;
        return versions.Any(v => string.Equals(
            NormalizeGameVersion(v),
            normalized,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     取列表中第一个可用于下载请求的 Minecraft 版本号（已去掉预发布标记）。
    /// </summary>
    /// <param name="versions">语言中立的版本标识列表。</param>
    public static string? FirstGameVersion(IEnumerable<string>? versions)
    {
        var version = versions?.FirstOrDefault(v => v.Contains('.'));
        return version is null ? null : NormalizeGameVersion(version);
    }

    public static string GetCompTypeName(CompType type)
    {
        return Lang.Text(
            type switch
            {
                CompType.Mod => "Download.Comp.Type.Mod",
                CompType.ModPack => "Download.Comp.Type.Modpack",
                CompType.ResourcePack => "Download.Comp.Type.ResourcePack",
                CompType.Shader => "Download.Comp.Type.Shader",
                CompType.DataPack => "Download.Comp.Type.DataPack",
                CompType.Plugin => "Download.Comp.Type.Plugin",
                CompType.World => "Download.Comp.Type.World",
                CompType.Schematic => "Download.Comp.Type.Schematic",
                _ => "Download.Comp.Type.Unknown"
            });
    }

    /// <summary>
    ///     获取该资源类型的「加载中」提示所对应的本地化资源键。
    /// </summary>
    /// <param name="type">资源类型。</param>
    public static string GetCompLoadingKey(CompType type)
    {
        return type switch
        {
            CompType.Mod => "Download.Comp.List.Loading.Mod",
            CompType.ModPack => "Download.Comp.List.Loading.Modpack",
            CompType.ResourcePack => "Download.Comp.List.Loading.ResourcePack",
            CompType.Shader => "Download.Comp.List.Loading.Shader",
            CompType.DataPack => "Download.Comp.List.Loading.DataPack",
            CompType.Plugin => "Download.Comp.List.Loading.Plugin",
            CompType.World => "Download.Comp.List.Loading.World",
            CompType.Schematic => "Download.Comp.List.Loading.Schematic",
            _ => "Download.Comp.List.Loading.Unknown"
        };
    }

    /// <summary>
    ///     获取该资源类型的搜索框提示所对应的本地化资源键。
    /// </summary>
    /// <param name="type">资源类型。</param>
    public static string GetCompSearchKey(CompType type)
    {
        return type switch
        {
            CompType.Mod => "Download.Comp.List.Search.Mod",
            CompType.ModPack => "Download.Comp.List.Search.Modpack",
            CompType.ResourcePack => "Download.Comp.List.Search.ResourcePack",
            CompType.Shader => "Download.Comp.List.Search.Shader",
            CompType.DataPack => "Download.Comp.List.Search.DataPack",
            CompType.Plugin => "Download.Comp.List.Search.Plugin",
            CompType.World => "Download.Comp.List.Search.World",
            CompType.Schematic => "Download.Comp.List.Search.Schematic",
            _ => "Download.Comp.List.Search.Unknown"
        };
    }

    /// <summary>
    ///     获取该资源类型的「加载中」提示文本。
    /// </summary>
    /// <param name="type">资源类型。</param>
    public static string GetCompLoadingName(CompType type)
    {
        return Lang.Text(GetCompLoadingKey(type));
    }

    /// <summary>
    ///     获取该资源类型的搜索框提示文本。
    /// </summary>
    /// <param name="type">资源类型。</param>
    public static string GetCompSearchName(CompType type)
    {
        return Lang.Text(GetCompSearchKey(type));
    }
}
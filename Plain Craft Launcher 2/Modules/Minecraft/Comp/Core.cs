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

    public static string GetCompLoadingName(CompType type)
    {
        return Lang.Text(
            type switch
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
            });
    }

    public static string GetCompSearchName(CompType type)
    {
        return Lang.Text(
            type switch
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
            });
    }
}

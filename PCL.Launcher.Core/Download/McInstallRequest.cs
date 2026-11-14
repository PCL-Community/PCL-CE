namespace PCL;

/// <summary>
///     合并安装请求。所有字段描述“用户想安装什么”，组合合法性与前置条件由
///     <see cref="McInstallPipeline" /> 在开始安装前统一校验。
/// </summary>
public class McInstallRequest
{
    /// <summary>
    ///     欲下载的 Cleanroom。
    /// </summary>
    public GameDownload.DlCleanroomListEntry? cleanroomEntry = null;

    /// <summary>
    ///     欲下载的 Cleanroom 版本名。
    /// </summary>
    public string? cleanroomVersion;

    /// <summary>
    ///     欲下载的 Fabric API 信息。
    /// </summary>
    public Comp.CompFile? fabricApi = null;

    /// <summary>
    ///     欲下载的 Fabric Loader 版本名。
    /// </summary>
    public string? fabricVersion = null;

    /// <summary>
    ///     欲下载的 Forge。
    /// </summary>
    public GameDownload.DlForgeVersionEntry? forgeEntry = null;

    /// <summary>
    ///     欲下载的 Forge 版本名。接受例如 36.1.4 / 14.23.5.2859 / 1.19-41.1.0 的输入。
    /// </summary>
    public string? forgeVersion;

    /// <summary>
    ///     欲下载的 LabyMod 通道。
    /// </summary>
    public string? labyModChannel = null;

    /// <summary>
    ///     欲下载的 LabyMod 版本。
    /// </summary>
    public string? labyModCommitRef = null;

    /// <summary>
    ///     欲下载的 Legacy Fabric API 信息。
    /// </summary>
    public Comp.CompFile? legacyFabricApi = null;

    /// <summary>
    ///     欲下载的 Legacy Fabric Loader 版本名。
    /// </summary>
    public string? legacyFabricVersion = null;

    /// <summary>
    ///     欲下载的 LiteLoader 详细信息。
    /// </summary>
    public GameDownload.DlLiteLoaderListEntry? liteLoaderEntry = null;

    /// <summary>
    ///     可选。欲下载的 Minecraft Json 地址。
    /// </summary>
    public string? minecraftJson = null;

    /// <summary>
    ///     必填。欲下载的 Minecraft 的版本名。
    /// </summary>
    public string? minecraftName = null;

    /// <summary>
    ///     若 MMC 整合包安装包含特殊参数，则填写此项。
    /// </summary>
    public MmcPackInfo? mmcPackInfo = null;

    /// <summary>
    ///     欲下载的 NeoForge。
    /// </summary>
    public GameDownload.DlNeoForgeListEntry? neoForgeEntry = null;

    /// <summary>
    ///     欲下载的 NeoForge 版本名。
    /// </summary>
    public string? neoForgeVersion;

    /// <summary>
    ///     欲下载的 OptiFabric 信息。
    /// </summary>
    public Comp.CompFile? optiFabric = null;

    /// <summary>
    ///     欲下载的 OptiFine 详细信息。
    /// </summary>
    public GameDownload.DlOptiFineListEntry? optiFineEntry;

    /// <summary>
    ///     欲下载的 OptiFine 版本名。例如 HD_U_F6_pre1。
    /// </summary>
    public string? optiFineVersion;

    /// <summary>
    ///     必填。安装目标文件夹。
    /// </summary>
    public required string targetInstanceFolder;

    /// <summary>
    ///     必填。安装目标实例名称。
    /// </summary>
    public required string targetInstanceName;

    /// <summary>是否请求了 Forge / NeoForge / Cleanroom 中的任意一种。</summary>
    public bool HasForgelikeLoader =>
        forgeEntry is not null
        || forgeVersion is not null
        || neoForgeEntry is not null
        || neoForgeVersion is not null
        || cleanroomEntry is not null
        || cleanroomVersion is not null;

    /// <summary>是否请求了任意一个主加载器（Forge / NeoForge / Cleanroom / Fabric / Legacy Fabric / LabyMod）。</summary>
    public bool HasMainLoader =>
        forgeEntry is not null
        || forgeVersion is not null
        || neoForgeEntry is not null
        || neoForgeVersion is not null
        || cleanroomEntry is not null
        || cleanroomVersion is not null
        || fabricVersion is not null
        || legacyFabricVersion is not null
        || labyModCommitRef is not null;

    /// <summary>是否请求了任意一个加载器或附属组件。</summary>
    public bool HasAnyLoader =>
        HasMainLoader
        || liteLoaderEntry is not null
        || optiFineEntry is not null
        || optiFineVersion is not null
        || fabricApi is not null
        || legacyFabricApi is not null
        || optiFabric is not null;

    /// <summary>
    ///     该安装是否会以“模组”形式放置 OptiFine，而不是运行 OptiFine 安装器。
    ///     与 WPF 的 McInstallFolders.OptiFineAsMod 判定保持一致。
    /// </summary>
    public bool OptiFineAsMod =>
        (optiFineEntry is not null || optiFineVersion is not null)
        && (HasForgelikeLoader
            || fabricVersion is not null
            || legacyFabricVersion is not null
            || liteLoaderEntry is not null);
}
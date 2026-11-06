namespace PCL;

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
}
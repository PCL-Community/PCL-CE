namespace PCL.Navigation;

/// <summary>子页面 ID。父页面参与标识，避免不同功能模块复用同名子页。</summary>
public readonly record struct SubPageId(PageId Parent, string Value)
{
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString()
    {
        return $"{Parent}/{Value}";
    }
}

/// <summary>子页面 ID 的常量集合。父页面已经编码在值中。</summary>
public static class SubPageIds
{
    public static readonly SubPageId DownloadInstall = new(PageIds.Download, "DownloadInstall");
    public static readonly SubPageId DownloadMod = new(PageIds.Download, "DownloadMod");
    public static readonly SubPageId DownloadPack = new(PageIds.Download, "DownloadPack");
    public static readonly SubPageId DownloadDataPack = new(PageIds.Download, "DownloadDataPack");

    public static readonly SubPageId DownloadResourcePack = new(
        PageIds.Download,
        "DownloadResourcePack");

    public static readonly SubPageId DownloadShader = new(PageIds.Download, "DownloadShader");
    public static readonly SubPageId DownloadWorld = new(PageIds.Download, "DownloadWorld");

    public static readonly SubPageId DownloadCompFavorites = new(
        PageIds.Download,
        "DownloadCompFavorites");

    public static readonly SubPageId DownloadClient = new(PageIds.Download, "DownloadClient");
    public static readonly SubPageId DownloadOptiFine = new(PageIds.Download, "DownloadOptiFine");
    public static readonly SubPageId DownloadForge = new(PageIds.Download, "DownloadForge");
    public static readonly SubPageId DownloadNeoForge = new(PageIds.Download, "DownloadNeoForge");
    public static readonly SubPageId DownloadCleanroom = new(PageIds.Download, "DownloadCleanroom");
    public static readonly SubPageId DownloadFabric = new(PageIds.Download, "DownloadFabric");

    public static readonly SubPageId DownloadLiteLoader = new(
        PageIds.Download,
        "DownloadLiteLoader");

    public static readonly SubPageId DownloadLabyMod = new(PageIds.Download, "DownloadLabyMod");

    public static readonly SubPageId DownloadLegacyFabric = new(
        PageIds.Download,
        "DownloadLegacyFabric");

    public static readonly SubPageId SetupLaunch = new(PageIds.Setup, "SetupLaunch");
    public static readonly SubPageId SetupUi = new(PageIds.Setup, "SetupUi");
    public static readonly SubPageId SetupGameManage = new(PageIds.Setup, "SetupGameManage");
    public static readonly SubPageId SetupLink = new(PageIds.Setup, "SetupLink");
    public static readonly SubPageId SetupAbout = new(PageIds.Setup, "SetupAbout");
    public static readonly SubPageId SetupLog = new(PageIds.Setup, "SetupLog");
    public static readonly SubPageId SetupFeedback = new(PageIds.Setup, "SetupFeedback");
    public static readonly SubPageId SetupGameLink = new(PageIds.Setup, "SetupGameLink");
    public static readonly SubPageId SetupUpdate = new(PageIds.Setup, "SetupUpdate");
    public static readonly SubPageId SetupJava = new(PageIds.Setup, "SetupJava");
    public static readonly SubPageId SetupLauncherMisc = new(PageIds.Setup, "SetupLauncherMisc");

    public static readonly SubPageId SetupLauncherLanguage = new(
        PageIds.Setup,
        "SetupLauncherLanguage");

    public static readonly SubPageId ToolsGameLink = new(PageIds.Tools, "ToolsGameLink");
    public static readonly SubPageId ToolsTest = new(PageIds.Tools, "ToolsTest");

    public static readonly SubPageId VersionOverall = new(PageIds.InstanceSetup, "VersionOverall");
    public static readonly SubPageId VersionSetup = new(PageIds.InstanceSetup, "VersionSetup");
    public static readonly SubPageId VersionExport = new(PageIds.InstanceSetup, "VersionExport");
    public static readonly SubPageId VersionWorld = new(PageIds.InstanceSetup, "VersionWorld");

    public static readonly SubPageId VersionScreenshot = new(
        PageIds.InstanceSetup,
        "VersionScreenshot");

    public static readonly SubPageId VersionMod = new(PageIds.InstanceSetup, "VersionMod");

    public static readonly SubPageId VersionModDisabled = new(
        PageIds.InstanceSetup,
        "VersionModDisabled");

    public static readonly SubPageId VersionResourcePack = new(
        PageIds.InstanceSetup,
        "VersionResourcePack");

    public static readonly SubPageId VersionShader = new(PageIds.InstanceSetup, "VersionShader");

    public static readonly SubPageId VersionSchematic = new(
        PageIds.InstanceSetup,
        "VersionSchematic");

    public static readonly SubPageId VersionInstall = new(PageIds.InstanceSetup, "VersionInstall");
    public static readonly SubPageId VersionServer = new(PageIds.InstanceSetup, "VersionServer");

    public static readonly SubPageId VersionSavesInfo = new(
        PageIds.VersionSaves,
        "VersionSavesInfo");

    public static readonly SubPageId VersionSavesDatapack = new(
        PageIds.VersionSaves,
        "VersionSavesDatapack");
}
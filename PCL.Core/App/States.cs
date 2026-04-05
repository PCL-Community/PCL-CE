using System.Collections.Generic;
using PCL.Core.App.Configuration;

namespace PCL.Core.App;

/// <summary>
/// 全局状态类。
/// </summary>
// ReSharper disable InconsistentNaming
public static partial class States
{
    /// <summary>
    /// 唯一标识符。
    /// </summary>
    [ConfigItem<string>("Identify", "")] public static partial string Identifier { get; set; }
    
    /// <summary>
    /// 自定义变量。
    /// </summary>
    [AnyConfigItem<Dictionary<string, string>>("CustomVariables", ConfigSource.Local)] public static partial Dictionary<string, string> CustomVariables { get; set; }

    /// <summary>
    /// 提示状态。
    /// </summary>
    [ConfigGroup("Hint")] partial class HintStatesGroup
    {
        /// <summary>
        /// 过大下载线程警告。
        /// </summary>
        [ConfigItem<bool>("HintDownloadThread", false)] public partial bool LargeDownloadThread { get; set; }

        // [ConfigItem<int>("HintNotice", 0)] public partial int Notice { get; set; }

        /// <summary>
        /// 渲染器选择提示。
        /// </summary>
        [ConfigItem<bool>("HintRenderer", false)] public partial bool Renderer { get; set; }

        /// <summary>
        /// 使用调试级别 Log4j2 配置提示。
        /// </summary>
        [ConfigItem<bool>("HintDebugLog4j2Config", false)] public partial bool DebugLog4j2Config { get; set; }
        
        // [ConfigItem<int>("HintDownload", 0)] public partial int Download { get; set; }

        /// <summary>
        /// 单击 Minecraft 返回游戏版本选择提示。
        /// </summary>
        [ConfigItem<bool>("HintInstallBack", false)] public partial bool InstallPageBack { get; set; }

        /// <summary>
        /// 隐藏实例提示。
        /// </summary>
        [ConfigItem<bool>("HintHide", false)] public partial bool HideGameInstance { get; set; }

        /// <summary>
        /// 手动安装版本提示。
        /// </summary>
        [ConfigItem<bool>("HintHandInstall", false)] public partial bool ManualInstall { get; set; }

        /// <summary>
        /// 清理垃圾提示。
        /// </summary>
        [ConfigItem<int>("HintClearRubbish", 0)] public partial int CleanJunkFile { get; set; }

        /// <summary>
        /// Mod 更新提示。
        /// </summary>
        [ConfigItem<bool>("HintUpdateMod", false)] public partial bool UpdateMod { get; set; }

        /// <summary>
        /// 执行自定义主页内含命令提示。
        /// </summary>
        [ConfigItem<bool>("HintCustomCommand", false)] public partial bool HomepageCommand { get; set; }

        /// <summary>
        /// 非信任主页警告。
        /// </summary>
        [ConfigItem<bool>("HintCustomWarn", false)] public partial bool UntrustedHomepage { get; set; }

        /// <summary>
        /// 全局设置中在实例独立设置中寻找更多高级启动选项的提示。
        /// </summary>
        [ConfigItem<bool>("HintMoreAdvancedSetup", false)] public partial bool MoreInstanceSetup { get; set; }

        /// <summary>
        /// 进入实例独立设置提示。
        /// </summary>
        [ConfigItem<bool>("HintIndieSetup", false)] public partial bool IndieSetup { get; set; }

        /// <summary>
        /// 选择档案以启动游戏提示。
        /// </summary>
        [ConfigItem<bool>("HintProfileSelect", false)] public partial bool LaunchWithProfile { get; set; }

        /// <summary>
        /// 导出配置提示。
        /// </summary>
        [ConfigItem<bool>("HintExportConfig", false)] public partial bool ExportConfig { get; set; }

        /// <summary>
        /// 实时日志最大行数提示。
        /// </summary>
        [ConfigItem<bool>("HintMaxLog", false)] public partial bool MaxGameLog { get; set; }

        /// <summary>
        /// 非 ASCII 路径警告。 
        /// </summary>
        [ConfigItem<bool>("HintDisableGamePathCheckTip", false)] public partial bool NonAsciiGamePath { get; set; }

        /// <summary>
        /// 启动时的社区版提示。
        /// </summary>
        [ConfigItem<bool>("UiLauncherCEHint", true)] public partial bool CEMessage { get; set; }

        /// <summary>
        /// 投影管理首次使用提示。
        /// </summary>
        [ConfigItem<bool>("UiSchematicFirstTimeHintShown", false)] public partial bool SchematicFirstTime { get; set; }

        /// <summary>
        /// 已显示的公告。
        /// </summary>
        [ConfigItem<string>("SystemSystemAnnouncement", "")] public partial string ShowedAnnouncements { get; set; }

        /// <summary>
        /// 数据包更新警告。
        /// </summary>
        [ConfigItem<bool>("HintDatapackUpdate",false)] public partial bool FunctionDatapackUpdate { get; set; }
    }

    /// <summary>
    /// 游戏与启动相关状态。
    /// </summary>
    [ConfigGroup("Game")] partial class GameStatesGroup
    {
        /// <summary>
        /// Java 列表版本。
        /// </summary>
        [ConfigItem<int>("CacheJavaListVersion", 0)] public partial int JavaListVersion { get; set; }

        /// <summary>
        /// PCL 原版游戏账户系统相关字段
        /// </summary>
        [ConfigGroup("LegacyProfile")] partial class LegacyProfileConfigGroup
            {
            /// <summary>
            /// 原版第三方认证 UUID。
            /// </summary>
            [ConfigItem<string>("CacheAuthUuid", "", ConfigSource.SharedEncrypt)] public partial string AuthUuid { get; set; }

            /// <summary>
            /// 原版第三方认证用户名。
            /// </summary>
            [ConfigItem<string>("CacheAuthName", "", ConfigSource.SharedEncrypt)] public partial string AuthUserName { get; set; }

            /// <summary>
            /// 原版第三方认证档案名。
            /// </summary>
            [ConfigItem<string>("CacheAuthUsername", "", ConfigSource.SharedEncrypt)] public partial string AuthThirdPartyUserName { get; set; }

            /// <summary>
            /// 原版第三方认证用户密码。
            /// </summary>
            [ConfigItem<string>("CacheAuthPass", "", ConfigSource.SharedEncrypt)] public partial string AuthPassword { get; set; }

            /// <summary>
            /// 原版第三方认证服务器。
            /// </summary>
            [ConfigItem<string>("CacheAuthServerServer", "", ConfigSource.SharedEncrypt)] public partial string AuthServerAddress { get; set; }
            
            /// <summary>
            /// 原版离线档案名。
            /// </summary>
            [ConfigItem<string>("LoginLegacyName", "", ConfigSource.SharedEncrypt)] public partial string LoginLegacyName { get; set; }

            /// <summary>
            /// 原版微软登录 JSON。
            /// </summary>
            [ConfigItem<string>("LoginMsJson", "{}", ConfigSource.SharedEncrypt)] public partial string LoginMsJson { get; set; }
        }

        /// <summary>
        /// MC 版本 Drops。
        /// </summary>
        [ConfigItem<string>("CacheDrops", "")] public partial string Drops { get; set; }
        
        /// <summary>
        /// 当前实例。
        /// </summary>
        [ConfigItem<string>("LaunchInstanceSelect", "", ConfigSource.Local)] public partial string SelectedInstance { get; set; }

        /// <summary>
        /// 当前文件夹。
        /// </summary>
        [ConfigItem<string>("LaunchFolderSelect", "", ConfigSource.Local)] public partial string SelectedFolder { get; set; }

        /// <summary>
        /// 所有文件夹。
        /// </summary>
        [ConfigItem<string>("LaunchFolders", "")] public partial string Folders { get; set; }

        /// <summary>
        /// 已知所有 Java 实例。
        /// </summary>
        [ConfigItem<string>("LaunchArgumentJavaUser", "[]")] public partial string JavaList { get; set; }
        
        /// <summary>
        /// 收藏的社区资源
        /// </summary>
        [ConfigItem<string>("CompFavorites", "[]")] public partial string CompFavorites { get; set; }
    }

    /// <summary>
    /// 用户界面状态。
    /// </summary>
    [ConfigGroup("UI")] partial class UiStatesGroup
    {
        /// <summary>
        /// 当前自定义主页 URL。
        /// </summary>
        [ConfigItem<string>("CacheSavedPageUrl", "")] public partial string SavedHomepageUrl { get; set; }

        /// <summary>
        /// 当前自定义主页版本。
        /// </summary>
        [ConfigItem<string>("CacheSavedPageVersion", "")] public partial string SavedHomepageVersion { get; set; }

        /// <summary>
        /// 窗口高度。
        /// </summary>
        [ConfigItem<double>("WindowHeight", 550, ConfigSource.Local)] public partial double WindowHeight { get; set; }

        /// <summary>
        /// 窗口宽度。
        /// </summary>
        [ConfigItem<double>("WindowWidth", 900, ConfigSource.Local)] public partial double WindowWidth { get; set; }

        /// <summary>
        /// 传说中的秋仪金代码，但是没卵用。
        /// </summary>
        [ConfigItem<string>("UiLauncherThemeGold", "")] public partial string ThemeGoldCode { get; set; }

        /// <summary>
        /// 传说中的隐藏主题1，但是没卵用
        /// </summary>
        [ConfigItem<string>("UiLauncherThemeHide", "0|1|2|3|4")] public partial string ThemeHiddenV1 { get; set; }

        /// <summary>
        /// 传说中的隐藏主题2，但是没卵用。
        /// </summary>
        [ConfigItem<string>("UiLauncherThemeHide2", "0|1|2|3|4")] public partial string ThemeHiddenV2 { get; set; }

    }

    /// <summary>
    /// 系统状态。
    /// </summary>
    [ConfigGroup("System")] partial class SystemStatesGroup
    {
        /// <summary>
        /// 识别码。
        /// </summary>
        [ConfigItem<string>("LaunchUuid", "")] public partial string LaunchUuid { get; set; }

        /// <summary>
        /// 上次导出配置路径。
        /// </summary>
        [ConfigItem<string>("CacheExportConfig", "")] public partial string ExportConfigPath { get; set; }

        /// <summary>
        /// 最终用户许可协议。
        /// </summary>
        [ConfigItem<bool>("SystemEula", false)] public partial bool LauncherEula { get; set; }

        /// <summary>
        /// 启动器打开次数。
        /// </summary>
        [ConfigItem<int>("SystemCount", 0, ConfigSource.SharedEncrypt)] public partial int StartupCount { get; set; }

        /// <summary>
        /// 游戏启动次数。
        /// </summary>
        [ConfigItem<int>("SystemLaunchCount", 0, ConfigSource.SharedEncrypt)] public partial int LaunchCount { get; set; }

        /// <summary>
        /// 上个版本。
        /// </summary>
        [ConfigItem<int>("SystemLastVersionReg", 0, ConfigSource.SharedEncrypt)] public partial int LastVersion { get; set; }

        // [ConfigItem<int>("SystemHighestSavedBetaVersionReg", 0, ConfigSource.SharedEncrypt)] public partial int LastSavedBetaVersion { get; set; }

        /// <summary>
        /// 上个最高 Beta 版本。
        /// </summary>
        [ConfigItem<int>("SystemHighestBetaVersionReg", 0, ConfigSource.SharedEncrypt)] public partial int LastBetaVersion { get; set; }

        /// <summary>
        /// 上个最高 Alpha 版本。
        /// </summary>
        [ConfigItem<int>("SystemHighestAlphaVersionReg", 0, ConfigSource.SharedEncrypt)] public partial int LastAlphaVersion { get; set; }

        /// <summary>
        /// 全局配置版本。
        /// </summary>
        [ConfigItem<int>("SystemSetupVersionReg", 1)] public partial int SetupVersionGlobal { get; set; }

        /// <summary>
        /// 本地配置版本。
        /// </summary>
        [ConfigItem<int>("SystemSetupVersionIni", 1, ConfigSource.Local)] public partial int SetupVersionLocal { get; set; }

        /// <summary>
        /// 启动器公告。
        /// </summary>
        [ConfigItem<int>("SystemSystemActivity", 0, ConfigSource.Local)] public partial int AnnounceSolution { get; set; }

    }

    /// <summary>
    /// 工具状态。
    /// </summary>
    [ConfigGroup("Tool")] partial class ToolStatesGroup
    {
        /// <summary>
        /// 自定义下载目标目录。
        /// </summary>
        [ConfigItem<string>("CacheDownloadFolder", "")] public partial string DownloadFolder { get; set; }

        /// <summary>
        /// 自定义下载 UA。
        /// </summary>
        [ConfigItem<string>("ToolDownloadCustomUserAgent", "")] public partial string DownloadUserAgent { get; set; }
        
        /// <summary>
        /// 最新 MC 正式版
        /// </summary>
        [ConfigItem<string>("ToolUpdateReleaseLast", "")] public partial string LastRelease { get; set; }
        
        /// <summary>
        /// 最新 MC 快照版
        /// </summary>
        [ConfigItem<string>("ToolUpdateSnapshotLast", "")] public partial string LastSnapshot { get; set; }
    }

    /// <summary>
    /// 联机大厅状态。
    /// </summary>
    [ConfigGroup("Link")] partial class LinkStatesGroup
    {
        /// <summary>
        /// 大厅最终用户许可协议。
        /// </summary>
        [ConfigItem<bool>("LinkEula", false)] public partial bool LinkEula { get; set; }

        /// <summary>
        /// 公告缓存。
        /// </summary>
        [ConfigItem<string>("LinkAnnounceCache", "", ConfigSource.SharedEncrypt)] public partial string AnnounceCache { get; set; }

        /// <summary>
        /// 公告缓存版本。
        /// </summary>
        [ConfigItem<int>("LinkAnnounceCacheVer", 0)] public partial int AnnounceCacheVer { get; set; }

        /// <summary>
        /// Natayark ID 刷新令牌。
        /// </summary>
        [ConfigItem<string>("LinkNaidRefreshToken", "", ConfigSource.SharedEncrypt)] public partial string NaidRefreshToken { get; set; }

        /// <summary>
        /// Natayark ID 令牌过期时间。
        /// </summary>
        [ConfigItem<string>("LinkNaidRefreshExpiresAt", "", ConfigSource.SharedEncrypt)] public partial string NaidRefreshExpireTime { get; set; }

        /// <summary>
        /// 首次网络测试状态。
        /// </summary>
        [ConfigItem<bool>("LinkFirstTimeNetTest", true, ConfigSource.SharedEncrypt)] public partial bool DoFirstTimeNetTest { get; set; }

    }

    /// <summary>
    /// 实例独立状态
    /// </summary>
    [ConfigGroup("Instance", ConfigSource.GameInstance)] partial class InstanceStatesGroup
    {
        [ConfigItem<int>("VersionLaunchCount", 0)] public partial ArgConfig<int> LaunchCount { get; }
        [ConfigItem<bool>("IsStar", false)] public partial ArgConfig<bool> Starred { get; }
        [ConfigItem<int>("DisplayType", 0)] public partial ArgConfig<int> CardType { get; }
        [ConfigItem<string>("Logo", "")] public partial ArgConfig<string> LogoPath { get; }
        [ConfigItem<bool>("LogoCustom", false)] public partial ArgConfig<bool> IsLogoCustom { get; }
        [ConfigItem<string>("CustomInfo", "")] public partial ArgConfig<string> CustomInfo { get; }
        [ConfigItem<string>("Info", "")] public partial ArgConfig<string> Info { get; }
        [ConfigItem<string>("ReleaseTime", "")] public partial ArgConfig<string> ReleaseTime { get; }
        [ConfigItem<int>("State", 0)] public partial ArgConfig<int> State { get; }
        [ConfigItem<string>("VersionFabric", "")] public partial ArgConfig<string> FabricVersion { get; }
        [ConfigItem<string>("VersionLegacyFabric", "")] public partial ArgConfig<string> LegacyFabricVersion { get; }
        [ConfigItem<string>("VersionQuilt", "")] public partial ArgConfig<string> QuiltVersion { get; }
        [ConfigItem<string>("VersionLabyMod", "")] public partial ArgConfig<string> LabyModVersion { get; }
        [ConfigItem<string>("VersionOptiFine", "")] public partial ArgConfig<string> OptiFineVersion { get; }
        [ConfigItem<bool>("VersionLiteLoader", false)] public partial ArgConfig<bool> HasLiteLoader { get; }
        [ConfigItem<string>("VersionForge", "")] public partial ArgConfig<string> ForgeVersion { get; }
        [ConfigItem<string>("VersionNeoForge", "")] public partial ArgConfig<string> NeoForgeVersion { get; }
        [ConfigItem<string>("VersionCleanroom", "")] public partial ArgConfig<string> CleanroomVersion { get; }
        [ConfigItem<string>("VersionVanillaName", "Unknown")] public partial ArgConfig<string> VanillaVersionName { get; }
        [ConfigItem<string>("VersionVanilla", "0.0.0")] public partial ArgConfig<string> VanillaVersion { get; }
        [ConfigItem<string>("VersionModpackVersion", "")] public partial ArgConfig<string> ModpackVersion { get; }
        [ConfigItem<string>("VersionModpackSource", "")] public partial ArgConfig<string> ModpackSource { get; }
        [ConfigItem<string>("VersionModpackId", "")] public partial ArgConfig<string> ModpackId { get; }
    }
}

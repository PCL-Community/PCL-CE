using PCL.Core.App.Configuration;
using PCL.Core.Link;

namespace PCL.Core.App;

/// <summary>
/// 全局配置类。
/// </summary>
// ReSharper disable InconsistentNaming
public static partial class Config
{
    /// <summary>
    /// 唯一标识符。
    /// </summary>
    [ConfigItem<string>("Identify", "")] public static partial string Identifier { get; set; }

    /// <summary>
    /// 提示状态。
    /// </summary>
    [ConfigGroup("Hint")] partial class HintConfigGroup
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
    }

    /// <summary>
    /// 缓存路径。
    /// </summary>
    [ConfigGroup("Cache")] partial class CacheConfigGroup
    {
        /// <summary>
        /// 上次导出配置路径。
        /// </summary>
        [ConfigItem<string>("CacheExportConfig", "")] public partial string ExportConfigPath { get; set; }

        /// <summary>
        /// 当前自定义主页 URL。
        /// </summary>
        [ConfigItem<string>("CacheSavedPageUrl", "")] public partial string SavedHomepageUrl { get; set; }

        /// <summary>
        /// 当前自定义主页版本。
        /// </summary>
        [ConfigItem<string>("CacheSavedPageVersion", "")] public partial string SavedHomepageVersion { get; set; }

        /// <summary>
        /// 自定义下载目录。
        /// </summary>
        [ConfigItem<string>("CacheDownloadFolder", "")] public partial string DownloadFolder { get; set; }

        /// <summary>
        /// 自定义下载用户代理。
        /// </summary>
        [ConfigItem<string>("ToolDownloadCustomUserAgent", "")] public partial string DownloadUserAgent { get; set; }

        /// <summary>
        /// Java 列表版本。
        /// </summary>
        [ConfigItem<int>("CacheJavaListVersion", 0)] public partial int JavaListVersion { get; set; }

        /// <summary>
        /// 第三方认证 UUID。
        /// </summary>
        [ConfigItem<string>("CacheAuthUuid", "", ConfigSource.SharedEncrypt)] public partial string AuthUuid { get; set; }

        /// <summary>
        /// 第三方认证用户名。
        /// </summary>
        [ConfigItem<string>("CacheAuthName", "", ConfigSource.SharedEncrypt)] public partial string AuthUserName { get; set; }

        /// <summary>
        /// 第三方认证档案名。
        /// </summary>
        [ConfigItem<string>("CacheAuthUsername", "", ConfigSource.SharedEncrypt)] public partial string AuthThirdPartyUserName { get; set; }

        /// <summary>
        /// 第三方认证用户密码。
        /// </summary>
        [ConfigItem<string>("CacheAuthPass", "", ConfigSource.SharedEncrypt)] public partial string AuthPassword { get; set; }

        /// <summary>
        /// 第三方认证服务器。
        /// </summary>
        [ConfigItem<string>("CacheAuthServerServer", "", ConfigSource.SharedEncrypt)] public partial string AuthServerAddress { get; set; }
        
        /// <summary>
        /// MC 版本 Drops。
        /// </summary>
        [ConfigItem<string>("CacheDrops", "")] public partial string Drops { get; set; }
    }

    /// <summary>
    /// 系统配置。
    /// </summary>
    [ConfigGroup("System")] partial class SystemConfigGroup
    {
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
        /// 系统缓存目录。
        /// </summary>
        [ConfigItem<string>("SystemSystemCache", "")] public partial string CacheDirectory { get; set; }

        /// <summary>
        /// 启动器公告。
        /// </summary>
        [ConfigItem<int>("SystemSystemActivity", 0, ConfigSource.Local)] public partial int AnnounceSolution { get; set; }

        /// <summary>
        /// 禁用硬件加速。
        /// </summary>
        [ConfigItem<bool>("SystemDisableHardwareAcceleration", false)] public partial bool DisableHardwareAcceleration { get; set; }

        /// <summary>
        /// 遥测。
        /// </summary>
        [ConfigItem<bool>("SystemTelemetry", false)] public partial bool Telemetry { get; set; }

        /// <summary>
        /// Mirror 酱 CDK。
        /// </summary>
        [ConfigItem<string>("SystemMirrorChyanKey", "", ConfigSource.SharedEncrypt)] public partial string MirrorChyanKey { get; set; }

        /// <summary>
        /// 实时日志最大行数。
        /// </summary>
        [ConfigItem<int>("SystemMaxLog", 13)] public partial int MaxGameLog { get; set; }

        /// <summary>
        /// 识别码。
        /// </summary>
        [ConfigItem<string>("LaunchUuid", "")] public partial string LaunchUuid { get; set; }

        [ConfigGroup("Update")] partial class UpdateConfigGroup
        {
            /// <summary>
            /// 检查更新。
            /// </summary>
            [ConfigItem<int>("SystemSystemUpdate", 1, ConfigSource.Local)] public partial int UpdateMode { get; set; }

            /// <summary>
            /// 更新分支。
            /// </summary>
            [ConfigItem<int>("SystemUpdateChannel", 0, ConfigSource.Local)] public partial int UpdateChannel { get; set; }
        }

        [ConfigGroup("HttpProxy")] partial class HttpProxyConfigGroup
        {
            [ConfigItem<string>("SystemHttpProxy", "", ConfigSource.SharedEncrypt)] public partial string CustomAddress { get; set; }
            [ConfigItem<int>("SystemHttpProxyType", 1)] public partial int Type { get; set; }
            [ConfigItem<string>("SystemHttpProxyCustomUsername", "")] public partial string CustomUsername { get; set; }
            [ConfigItem<string>("SystemHttpProxyCustomPassword", "")] public partial string CustomPassword { get; set; }
        }

        [ConfigGroup("NetworkConfig")]
        partial class NetworkConfigGroup
        {
            [ConfigItem<bool>("SystemNetEnableDoH", true)] public partial bool EnableDoH { get; set; }
        }

        [ConfigGroup("Debug")] partial class DebugConfigGroup
        {
            [ConfigItem<bool>("SystemDebugMode", false)] public partial bool Enabled { get; set; }
            [ConfigItem<int>("SystemDebugAnim", 9)] public partial int AnimationSpeed { get; set; }
            [ConfigItem<bool>("SystemDebugDelay", false)] public partial bool AddRandomDelay { get; set; }
            [ConfigItem<bool>("SystemDebugSkipCopy", false)] public partial bool DontCopy { get; set; }
            [ConfigItem<bool>("SystemDebugAllowRestrictedFeature", false)] public partial bool AllowRestrictedFeature { get; set; }
        }
    }

    [ConfigGroup("Tool")] partial class ToolConfigGroup
    {
        [ConfigItem<string>("CompFavorites", "[]")] public partial string CompFavorites { get; set; }
        [ConfigItem<bool>("ToolFixAuthlib", true)] public partial bool FixAuthLib { get; set; }
        [ConfigItem<bool>("ToolHelpChinese", true)] public partial bool AutoChangeLanguage { get; set; }

        [ConfigGroup("Download")] partial class DownloadConfigGroup
        {
            [ConfigItem<int>("ToolDownloadThread", 63)] public partial int ThreadLimit { get; set; }
            [ConfigItem<int>("ToolDownloadSpeed", 42)] public partial int SpeedLimit { get; set; }
            [ConfigItem<int>("ToolDownloadSource", 1)] public partial int FileSourceSolution { get; set; }
            [ConfigItem<int>("ToolDownloadVersion", 1)] public partial int VersionSourceSolution { get; set; }
            [ConfigItem<int>("ToolDownloadTranslate", 0)] public partial int NameFormatV1 { get; set; }
            [ConfigItem<int>("ToolDownloadTranslateV2", 1)] public partial int NameFormatV2 { get; set; }
            [ConfigItem<bool>("ToolDownloadIgnoreQuilt", false)] public partial bool UiIgnoreQuilt { get; set; }
            [ConfigItem<bool>("ToolDownloadClipboard", false)] public partial bool ListenClipboard { get; set; }
            [ConfigItem<int>("ToolDownloadMod", 1)] public partial int CompSourceSolution { get; set; }
            [ConfigItem<int>("ToolModLocalNameStyle", 0)] public partial int UiCompNameSolution { get; set; }
            [ConfigItem<bool>("ToolDownloadAutoSelectVersion", true)] public partial bool AutoSelectInstance { get; set; }
        }

        [ConfigGroup("Update")] partial class UpdateConfigGroup
        {
            [ConfigItem<int>("ToolUpdateAlpha", 0, ConfigSource.SharedEncrypt)] public partial int Alpha { get; set; }
            [ConfigItem<bool>("ToolUpdateRelease", false)] public partial bool Release { get; set; }
            [ConfigItem<bool>("ToolUpdateSnapshot", false)] public partial bool Snapshot { get; set; }
            [ConfigItem<string>("ToolUpdateReleaseLast", "")] public partial string LastRelease { get; set; }
            [ConfigItem<string>("ToolUpdateSnapshotLast", "")] public partial string LastSnapshot { get; set; }
        }
    }

    [ConfigGroup("LegacyProfile")] partial class LegacyProfileConfigGroup
    {
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
    /// 联机大厅配置/状态。
    /// </summary>
    [ConfigGroup("Link")] partial class LinkConfigGroup
    {
        /// <summary>
        /// 大厅最终用户许可协议。
        /// </summary>
        [ConfigItem<bool>("LinkEula", false)] public partial bool LinkEula { get; set; }

        /// <summary>
        /// 大厅用户名。
        /// </summary>
        [ConfigItem<string>("LinkUsername", "")] public partial string Username { get; set; }

        /// <summary>
        /// 公告缓存。
        /// </summary>
        [ConfigItem<string>("LinkAnnounceCache", "", ConfigSource.SharedEncrypt)] public partial string AnnounceCache { get; set; }

        /// <summary>
        /// 公告缓存版本。
        /// </summary>
        [ConfigItem<int>("LinkAnnounceCacheVer", 0)] public partial int AnnounceCacheVer { get; set; }

        /// <summary>
        /// 中继方式。
        /// </summary>
        [ConfigItem<int>("LinkRelayType", 0)] public partial int RelayType { get; set; }

        /// <summary>
        /// 中继服务器类型 (社区/自有)。
        /// </summary>
        [ConfigItem<int>("LinkServerType", 1)] public partial int ServerType { get; set; }

        /// <summary>
        /// 延迟优先模式。
        /// </summary>
        [ConfigItem<bool>("LinkLatencyFirstMode", true)] public partial bool LatencyFirstMode { get; set; }

        /// <summary>
        /// 自定义中继服务器。
        /// </summary>
        [ConfigItem<string>("LinkRelayServer", "")] public partial string RelayServer { get; set; }

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

        /// <summary>
        /// 传输协议优先策略。
        /// </summary>
        [ConfigItem<LinkProtocolPreference>("LinkProtocolPreference", LinkProtocolPreference.Tcp)] public partial LinkProtocolPreference ProtocolPreference { get; set; }

        /// <summary>
        /// 尝试使用端口猜测打通对称性 NAT。
        /// </summary>
        [ConfigItem<bool>("LinkTryPunchSym", true)] public partial bool TryPunchSym { get; set; }

        /// <summary>
        /// 启用 IPv6。
        /// </summary>
        [ConfigItem<bool>("LinkEnableIPv6", true)] public partial bool EnableIPv6 { get; set; }
        
        /// <summary>
        /// 在日志中输出 Cli 信息以用于调试。
        /// </summary>
        [ConfigItem<bool>("LinkEnableCliOutput", false)] public partial bool EnableCliOutput { get; set; }
    }

    /// <summary>
    /// 用户界面配置。
    /// </summary>
    [ConfigGroup("UI")] partial class UiConfigGroup
    {
        /// <summary>
        /// 窗口高度。
        /// </summary>
        [ConfigItem<double>("WindowHeight", 550, ConfigSource.Local)] public partial double WindowHeight { get; set; }

        /// <summary>
        /// 窗口宽度。
        /// </summary>
        [ConfigItem<double>("WindowWidth", 900, ConfigSource.Local)] public partial double WindowWidth { get; set; }

        /// <summary>
        /// 启动时显示 Logo。
        /// </summary>
        [ConfigItem<bool>("UiLauncherLogo", true, ConfigSource.Local)] public partial bool ShowStartupLogo { get; set; }

        /// <summary>
        /// 锁定窗口大小。
        /// </summary>
        [ConfigItem<bool>("UiLockWindowSize", false)] public partial bool LockWindowSize { get; set; }

        /// <summary>
        /// 在启动游戏时显示你知道吗。
        /// </summary>
        [ConfigItem<bool>("UiShowLaunchingHint", true, ConfigSource.Local)] public partial bool ShowLaunchingHint { get; set; }

        /// <summary>
        /// 窗口标题类型。
        /// </summary>
        [ConfigItem<int>("UiLogoType", 1, ConfigSource.Local)] public partial int WindowTitleType { get; set; }

        /// <summary>
        /// 窗口标题文本。
        /// </summary>
        [ConfigItem<string>("UiLogoText", "", ConfigSource.Local)] public partial string LogoCustomText { get; set; }

        /// <summary>
        /// 工具栏居左。
        /// </summary>
        [ConfigItem<bool>("UiLogoLeft", false, ConfigSource.Local)] public partial bool TopBarLeftAlign { get; set; }

        /// <summary>
        /// 动画帧率上限。
        /// </summary>
        [ConfigItem<int>("UiAniFPS", 59)] public partial int AnimationFpsLimit { get; set; }

        /// <summary>
        /// 全局字体。
        /// </summary>
        [ConfigItem<string>("UiFont", "", ConfigSource.Local)] public partial string Font { get; set; }

        /// <summary>
        /// MOTD 字体。
        /// </summary>
        [ConfigItem<string>("UiMotdFont", "", ConfigSource.Local)] public partial string MotdFont { get; set; }

        /// <summary>
        /// 详细实例分类。
        /// </summary>
        [ConfigItem<bool>("DetailedInstanceClassification", false, ConfigSource.Local)] public partial bool DetailedInstanceClassification {  get; set; }

        /// <summary>
        /// 界面主题配置。
        /// </summary>
        [ConfigGroup("Theme")] partial class ThemeConfigGroup
        {
            /// <summary>
            /// 配色主题模式。
            /// </summary>
            [ConfigItem<int>("UiDarkMode", 2)] public partial int ColorMode { get; set; }

            /// <summary>
            /// 暗色配色主题。
            /// </summary>
            [ConfigItem<int>("UiDarkColor", 1)] public partial int DarkColor { get; set; }

            /// <summary>
            /// 亮色配色主题。
            /// </summary>
            [ConfigItem<int>("UiLightColor", 1)] public partial int LightColor { get; set; }

            /// <summary>
            /// 窗口透明度。
            /// </summary>
            [ConfigItem<int>("UiLauncherTransparent", 600, ConfigSource.Local)] public partial int WindowOpacity { get; set; }

            /// <summary>
            /// 自定义主题：色相 (H)。
            /// </summary>
            [ConfigItem<int>("UiLauncherHue", 180, ConfigSource.Local)] public partial int WindowHue { get; set; }

            /// <summary>
            /// 自定义主题：饱和度 (S)。
            /// </summary>
            [ConfigItem<int>("UiLauncherSat", 80, ConfigSource.Local)] public partial int WindowSat { get; set; }

            /// <summary>
            /// 自定义主题：明度 (L)。
            /// </summary>
            [ConfigItem<int>("UiLauncherLight", 20, ConfigSource.Local)] public partial int WindowLight { get; set; }

            /// <summary>
            /// 自定义主题：色相渐变。
            /// </summary>
            [ConfigItem<int>("UiLauncherDelta", 90, ConfigSource.Local)] public partial int WindowDelta { get; set; }

            /// <summary>
            /// 传说中的主题选择，但是没卵用。
            /// </summary>
            [ConfigItem<int>("UiLauncherTheme", 0, ConfigSource.Local)] public partial int ThemeSelected { get; set; }

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
        /// 背景内容。
        /// </summary>
        [ConfigGroup("Background")] partial class BackgroundConfigGroup
        {
            /// <summary>
            /// 彩色底部填充。
            /// </summary>
            [ConfigItem<bool>("UiBackgroundColorful", true, ConfigSource.Local)] public partial bool BackgroundColorful { get; set; }

            /// <summary>
            /// 透明度。
            /// </summary>
            [ConfigItem<int>("UiBackgroundOpacity", 1000, ConfigSource.Local)] public partial int WallpaperOpacity { get; set; }

            /// <summary>
            /// 旋转。
            /// </summary>
            [ConfigItem<int>("UiBackgroundCarousel", 1000, ConfigSource.Local)] public partial int WallpaperCarousel { get; set; }

            /// <summary>
            /// 模糊遮罩。
            /// </summary>
            [ConfigItem<int>("UiBackgroundBlur", 0, ConfigSource.Local)] public partial int WallpaperBlurRadius { get; set; }

            /// <summary>
            /// 内容裁剪模式。
            /// </summary>
            [ConfigItem<int>("UiBackgroundSuit", 0, ConfigSource.Local)] public partial int WallpaperSuitMode { get; set; }

            /// <summary>
            /// 视频自动暂停。
            /// </summary>
            [ConfigItem<bool>("UiAutoPauseVideo", true, ConfigSource.Local)] public partial bool AutoPauseVideo { get; set; }
        }

        /// <summary>
        /// 高级材质。
        /// </summary>
        [ConfigGroup("Blur")] partial class BlurConfigGroup
        {
            /// <summary>
            /// 是否启用。
            /// </summary>
            [ConfigItem<bool>("UiBlur", false, ConfigSource.Local)] public partial bool IsEnabled { get; set; }

            /// <summary>
            /// 模糊半径。
            /// </summary>
            [ConfigItem<int>("UiBlurValue", 16, ConfigSource.Local)] public partial int Radius { get; set; }

            /// <summary>
            /// 采样率。
            /// </summary>
            [ConfigItem<int>("UiBlurSamplingRate", 70, ConfigSource.Local)] public partial int SamplingRate { get; set; }

            /// <summary>
            /// 模糊方法。
            /// </summary>
            [ConfigItem<int>("UiBlurType", 0, ConfigSource.Local)] public partial int KernelType { get; set; }
        }

        /// <summary>
        /// 自定义主页。
        /// </summary>
        [ConfigGroup("Homepage")] partial class HomepageConfigGroup
        {
            /// <summary>
            /// 主页来源类型。
            /// </summary>
            [ConfigItem<int>("UiCustomType", 0, ConfigSource.Local)] public partial int Type { get; set; }

            /// <summary>
            /// 预设选项。
            /// </summary>
            [ConfigItem<int>("UiCustomPreset", 13, ConfigSource.Local)] public partial int SelectedPreset { get; set; }

            /// <summary>
            /// 自定义 URL。
            /// </summary>
            [ConfigItem<string>("UiCustomNet", "", ConfigSource.Local)] public partial string CustomUrl { get; set; }
        }

        /// <summary>
        /// 背景音乐。
        /// </summary>
        [ConfigGroup("Music")] partial class MusicConfigGroup
        {
            /// <summary>
            /// 音量。
            /// </summary>
            [ConfigItem<int>("UiMusicVolume", 500, ConfigSource.Local)] public partial int Volume { get; set; }

            /// <summary>
            /// 启动游戏后自动暂停。
            /// </summary>
            [ConfigItem<bool>("UiMusicStop", false, ConfigSource.Local)] public partial bool StopInGame { get; set; }

            /// <summary>
            /// 启动游戏后自动开始播放。
            /// </summary>
            [ConfigItem<bool>("UiMusicStart", false, ConfigSource.Local)] public partial bool StartInGame { get; set; }

            /// <summary>
            /// 自动开始播放。
            /// </summary>
            [ConfigItem<bool>("UiMusicAuto", true, ConfigSource.Local)] public partial bool StartOnStartup { get; set; }

            /// <summary>
            /// 随机播放。
            /// </summary>
            [ConfigItem<bool>("UiMusicRandom", true, ConfigSource.Local)] public partial bool ShufflePlayback { get; set; }

            /// <summary>
            /// 启用 SMTC。
            /// </summary>
            [ConfigItem<bool>("UiMusicSMTC", true, ConfigSource.Local)] public partial bool EnableSMTC { get; set; }
        }

        /// <summary>
        /// 功能隐藏。
        /// </summary>
        [ConfigGroup("Hide")]
        partial class HideConfigGroup
        {
            // 主页面
            [ConfigItem<bool>("UiHiddenPageDownload", false, ConfigSource.Local)] public partial bool PageDownload { get; set; }
            [ConfigItem<bool>("UiHiddenPageSetup", false, ConfigSource.Local)] public partial bool PageSetup { get; set; }
            [ConfigItem<bool>("UiHiddenPageTools", false, ConfigSource.Local)] public partial bool PageTools { get; set; }

            // 子页面 设置
            [ConfigItem<bool>("UiHiddenSetupLaunch", false, ConfigSource.Local)] public partial bool SetupLaunch { get; set; }
            [ConfigItem<bool>("UiHiddenSetupUi", false, ConfigSource.Local)] public partial bool SetupUi { get; set; }
            [ConfigItem<bool>("UiHiddenSetupSystem", false, ConfigSource.Local)] public partial bool SetupSystem { get; set; }
            [ConfigItem<bool>("UiHiddenSetupUpdate", false, ConfigSource.Local)] public partial bool SetupUpdate { get; set; }
            [ConfigItem<bool>("UiHiddenSetupGameLink", false, ConfigSource.Local)] public partial bool SetupGameLink { get; set; } // 新增
            [ConfigItem<bool>("UiHiddenSetupAbout", false, ConfigSource.Local)] public partial bool SetupAbout { get; set; } // 修正名称
            [ConfigItem<bool>("UiHiddenSetupFeedback", false, ConfigSource.Local)] public partial bool SetupFeedback { get; set; } // 修正名称
            [ConfigItem<bool>("UiHiddenSetupLog", false, ConfigSource.Local)] public partial bool SetupLog { get; set; } // 修正名称

            // 子页面 工具
            [ConfigItem<bool>("UiHiddenToolsGameLink", false, ConfigSource.Local)] public partial bool ToolsGameLink { get; set; } // 新增
            [ConfigItem<bool>("UiHiddenToolsHelp", false, ConfigSource.Local)] public partial bool ToolsHelp { get; set; } // 新增
            [ConfigItem<bool>("UiHiddenToolsTest", false, ConfigSource.Local)] public partial bool ToolsTest { get; set; } // 新增

            // 子页面 实例设置
            [ConfigItem<bool>("UiHiddenVersionEdit", false, ConfigSource.Local)] public partial bool InstanceEdit { get; set; }
            [ConfigItem<bool>("UiHiddenVersionExport", false, ConfigSource.Local)] public partial bool InstanceExport { get; set; }
            [ConfigItem<bool>("UiHiddenVersionSave", false, ConfigSource.Local)] public partial bool InstanceSave { get; set; }
            [ConfigItem<bool>("UiHiddenVersionScreenshot", false, ConfigSource.Local)] public partial bool InstanceScreenshot { get; set; }
            [ConfigItem<bool>("UiHiddenVersionMod", false, ConfigSource.Local)] public partial bool InstanceMod { get; set; }
            [ConfigItem<bool>("UiHiddenVersionResourcePack", false, ConfigSource.Local)] public partial bool InstanceResourcePack { get; set; }
            [ConfigItem<bool>("UiHiddenVersionShader", false, ConfigSource.Local)] public partial bool InstanceShader { get; set; }
            [ConfigItem<bool>("UiHiddenVersionSchematic", false, ConfigSource.Local)] public partial bool InstanceSchematic { get; set; }
            [ConfigItem<bool>("UiHiddenVersionServer", false, ConfigSource.Local)] public partial bool InstanceServer { get; set; }

            // 特定功能
            [ConfigItem<bool>("UiHiddenFunctionSelect", false, ConfigSource.Local)] public partial bool FunctionSelect { get; set; }
            [ConfigItem<bool>("UiHiddenFunctionModUpdate", false, ConfigSource.Local)] public partial bool FunctionModUpdate { get; set; }
            [ConfigItem<bool>("UiHiddenFunctionHidden", false, ConfigSource.Local)] public partial bool FunctionHidden { get; set; }
        }
    }

    /// <summary>
    /// 启动配置。
    /// </summary>
    [ConfigGroup("Launch")] partial class LaunchConfigGroup
    {
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
        /// 内存分配模式。
        /// </summary>
        [ConfigItem<int>("LaunchRamType", 0, ConfigSource.Local)] public partial int MemoryAllocationMode { get; set; }

        /// <summary>
        /// 自定义内存分配大小。
        /// </summary>
        [ConfigItem<int>("LaunchRamCustom", 15, ConfigSource.Local)] public partial int CustomMemorySize { get; set; }

        /// <summary>
        /// 优先 IP 协议栈。
        /// </summary>
        [ConfigItem<int>("LaunchPreferredIpStack", 1)] public partial int PreferredIpStack { get; set; }

        /// <summary>
        /// 启动前优化内存。
        /// </summary>
        [ConfigItem<bool>("LaunchArgumentRam", false)] public partial bool OptimizeMemory { get; set; }

        /// <summary>
        /// 附加 JVM 参数。
        /// </summary>
        [ConfigItem<string>("LaunchAdvanceJvm", "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true", ConfigSource.Local)] public partial string JvmArgs { get; set; }

        /// <summary>
        /// 附加游戏参数。
        /// </summary>
        [ConfigItem<string>("LaunchAdvanceGame", "", ConfigSource.Local)] public partial string GameArgs { get; set; }

        /// <summary>
        /// 预启动指令。
        /// </summary>
        [ConfigItem<string>("LaunchAdvanceRun", "", ConfigSource.Local)] public partial string PreLaunchCommand { get; set; }

        /// <summary>
        /// 是否等待预启动指令完成。
        /// </summary>
        [ConfigItem<bool>("LaunchAdvanceRunWait", true, ConfigSource.Local)] public partial bool PreLaunchCommandWait { get; set; }

        /// <summary>
        /// 禁用 Java Launch Wrapper。
        /// </summary>
        [ConfigItem<bool>("LaunchAdvanceDisableJLW", true, ConfigSource.Local)] public partial bool DisableJlw { get; set; }

        /// <summary>
        /// 禁用 Retro Wrapper
        /// </summary>
        [ConfigItem<bool>("LaunchAdvanceDisableRW", false, ConfigSource.Local)] public partial bool DisableRw { get; set; }

        /// <summary>
        /// 强制使用高性能显卡。
        /// </summary>
        [ConfigItem<bool>("LaunchAdvanceGraphicCard", true)] public partial bool SetGpuPreference { get; set; }

        /// <summary>
        /// 使用 java 而不是 javaw。
        /// </summary>
        [ConfigItem<bool>("LaunchAdvanceNoJavaw", false)] public partial bool NoJavaw { get; set; }

        /// <summary>
        /// 渲染器。
        /// </summary>
        [ConfigItem<int>("LaunchAdvanceRenderer", 0 ,ConfigSource.Local)] public partial int Renderer { get; set; }

        /// <summary>
        /// 游戏窗口标题。
        /// </summary>
        [ConfigItem<string>("LaunchArgumentTitle", "", ConfigSource.Local)] public partial string Title { get; set; }

        /// <summary>
        /// 自定义左下角版本信息。
        /// </summary>
        [ConfigItem<string>("LaunchArgumentInfo", "PCLCE", ConfigSource.Local)] public partial string TypeInfo { get; set; }

        /// <summary>
        /// 选择的默认 Java 实例。
        /// </summary>
        [ConfigItem<string>("LaunchArgumentJavaSelect", "")] public partial string SelectedJava { get; set; }

        /// <summary>
        /// 已知所有 Java 实例。
        /// </summary>
        [ConfigItem<string>("LaunchArgumentJavaUser", "[]")] public partial string JavaList { get; set; }

        /// <summary>
        /// 版本隔离 V1。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentIndie", 0, ConfigSource.Local)] public partial int IndieSolutionV1 { get; set; }

        /// <summary>
        /// 版本隔离 V2。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentIndieV2", 4, ConfigSource.Local)] public partial int IndieSolutionV2 { get; set; }

        /// <summary>
        /// 游戏启动后启动器可见性。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentVisible", 5)] public partial int LauncherVisibility { get; set; }

        /// <summary>
        /// 游戏进程优先级。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentPriority", 1)] public partial int ProcessPriority { get; set; }

        /// <summary>
        /// 游戏窗口宽度。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentWindowWidth", 854, ConfigSource.Local)] public partial int GameWindowWidth { get; set; }

        /// <summary>
        /// 游戏窗口高度。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentWindowHeight", 480, ConfigSource.Local)] public partial int GameWindowHeight { get; set; }

        /// <summary>
        /// 游戏窗口模式 (正常/最大化/全屏)。
        /// </summary>
        [ConfigItem<int>("LaunchArgumentWindowType", 1, ConfigSource.Local)] public partial int GameWindowMode { get; set; }

        /// <summary>
        /// 正版登录方式。
        /// </summary>
        [ConfigItem<int>("LoginMsAuthType", 1)] public partial int LoginMsAuthType { get; set; }
    }

    /// <summary>
    /// 实例独立配置。<br/>
    /// 懒得写注释了，自己理解吧。
    /// </summary>
    [ConfigGroup("Instance")] partial class InstanceConfigGroup
    {
        [ConfigItem<string>("VersionAdvanceJvm", "", ConfigSource.GameInstance)] public partial ArgConfig<string> JvmArgs { get; }
        [ConfigItem<string>("VersionAdvanceGame", "", ConfigSource.GameInstance)] public partial ArgConfig<string> GameArgs { get; }
        [ConfigItem<int>("VersionAdvanceRenderer", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> Renderer { get; }
        [ConfigItem<int>("VersionAdvanceAssets", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> AssetVerifySolutionV1 { get; }
        [ConfigItem<bool>("VersionAdvanceAssetsV2", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> DisableAssetVerifyV2 { get; }
        [ConfigItem<bool>("VersionAdvanceJava", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> IgnoreJavaCompatibility { get; }
        [ConfigItem<bool>("VersionAdvanceDisableJlw", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> DisableJlwObsolete { get; }
        [ConfigItem<string>("VersionAdvanceRun", "", ConfigSource.GameInstance)] public partial ArgConfig<string> PreLaunchCommand { get; }
        [ConfigItem<string>("VersionAdvanceClasspathHead", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ClasspathHead { get; }
        [ConfigItem<bool>("VersionAdvanceRunWait", true, ConfigSource.GameInstance)] public partial ArgConfig<bool> PreLaunchCommandWait { get; }
        [ConfigItem<bool>("VersionAdvanceDisableJLW", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> DisableJlw { get; }
        [ConfigItem<bool>("VersionAdvanceUseProxyV2", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> UseProxy { get; }
        [ConfigItem<bool>("VersionAdvanceDisableRW", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> DisableRw { get; }
        [ConfigItem<bool>("VersionUseDebugLog4j2Config", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> UseDebugLof4j2Config { get; }
        [ConfigItem<int>("VersionRamType", 2, ConfigSource.GameInstance)] public partial ArgConfig<int> MemorySolution { get; }
        [ConfigItem<int>("VersionRamCustom", 15, ConfigSource.GameInstance)] public partial ArgConfig<int> CustomMemorySize { get; }
        [ConfigItem<int>("VersionRamOptimize", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> OptimizeMemoryResolution { get; }
        [ConfigItem<string>("VersionArgumentTitle", "", ConfigSource.GameInstance)] public partial ArgConfig<string> Title { get; }
        [ConfigItem<bool>("VersionArgumentTitleEmpty", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> UseGlobalTitle { get; }
        [ConfigItem<string>("VersionArgumentInfo", "", ConfigSource.GameInstance)] public partial ArgConfig<string> TypeInfo { get; }
        [ConfigItem<int>("VersionArgumentIndie", -1, ConfigSource.GameInstance)] public partial ArgConfig<int> IndieV1 { get; }
        [ConfigItem<bool>("VersionArgumentIndieV2", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> IndieV2 { get; }
        [ConfigItem<string>("VersionArgumentJavaSelect", "使用全局设置", ConfigSource.GameInstance)] public partial ArgConfig<string> SelectedJava { get; }
        [ConfigItem<string>("VersionServerEnter", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ServerToEnter { get; }
        [ConfigItem<int>("VersionServerLoginRequire", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> LoginRequirementSolution { get; }
        [ConfigItem<string>("VersionServerAuthRegister", "", ConfigSource.GameInstance)] public partial ArgConfig<string> AuthRegisterAddress { get; }
        [ConfigItem<string>("VersionServerAuthName", "", ConfigSource.GameInstance)] public partial ArgConfig<string> AuthServerDisplayName { get; }
        [ConfigItem<string>("VersionServerAuthServer", "", ConfigSource.GameInstance)] public partial ArgConfig<string> AuthServerAddress { get; }
        [ConfigItem<bool>("VersionServerLoginLock", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> AuthTypeLucked { get; }
        [ConfigItem<int>("VersionLaunchCount", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> LaunchCount { get; }
        [ConfigItem<bool>("IsStar", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> Starred { get; }
        [ConfigItem<int>("DisplayType", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> CardType { get; }
        [ConfigItem<string>("Logo", "", ConfigSource.GameInstance)] public partial ArgConfig<string> LogoPath { get; }
        [ConfigItem<bool>("LogoCustom", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> IsLogoCustom { get; }
        [ConfigItem<string>("CustomInfo", "", ConfigSource.GameInstance)] public partial ArgConfig<string> CustomInfo { get; }
        [ConfigItem<string>("Info", "", ConfigSource.GameInstance)] public partial ArgConfig<string> Info { get; }
        [ConfigItem<string>("ReleaseTime", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ReleaseTime { get; }
        [ConfigItem<int>("State", 0, ConfigSource.GameInstance)] public partial ArgConfig<int> State { get; }
        [ConfigItem<string>("VersionFabric", "", ConfigSource.GameInstance)] public partial ArgConfig<string> FabricVersion { get; }
        [ConfigItem<string>("VersionLegacyFabric", "", ConfigSource.GameInstance)] public partial ArgConfig<string> LegacyFabricVersion { get; }
        [ConfigItem<string>("VersionQuilt", "", ConfigSource.GameInstance)] public partial ArgConfig<string> QuiltVersion { get; }
        [ConfigItem<string>("VersionLabyMod", "", ConfigSource.GameInstance)] public partial ArgConfig<string> LabyModVersion { get; }
        [ConfigItem<string>("VersionOptiFine", "", ConfigSource.GameInstance)] public partial ArgConfig<string> OptiFineVersion { get; }
        [ConfigItem<bool>("VersionLiteLoader", false, ConfigSource.GameInstance)] public partial ArgConfig<bool> HasLiteLoader { get; }
        [ConfigItem<string>("VersionForge", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ForgeVersion { get; }
        [ConfigItem<string>("VersionNeoForge", "", ConfigSource.GameInstance)] public partial ArgConfig<string> NeoForgeVersion { get; }
        [ConfigItem<string>("VersionCleanroom", "", ConfigSource.GameInstance)] public partial ArgConfig<string> CleanroomVersion { get; }
        [ConfigItem<string>("VersionVanillaName", "Unknown", ConfigSource.GameInstance)] public partial ArgConfig<string> VanillaVersionName { get; }
        [ConfigItem<string>("VersionVanilla", "0.0.0", ConfigSource.GameInstance)] public partial ArgConfig<string> VanillaVersion { get; }
        [ConfigItem<string>("VersionModpackVersion", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ModpackVersion { get; }
        [ConfigItem<string>("VersionModpackSource", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ModpackSource { get; }
        [ConfigItem<string>("VersionModpackId", "", ConfigSource.GameInstance)] public partial ArgConfig<string> ModpackId { get; }
    }
}

using System.Text.RegularExpressions;

namespace PCL.Core.Utils;

/// <summary>
/// 基于代码生成优化的正则表达式实例。
/// </summary>
public static partial class RegexPatterns
{
    /// <summary>
    /// 陶瓦联机 ID。
    /// </summary>
    public static readonly Regex TerracottaId = _TerracottaId();
    [GeneratedRegex("([0-9A-Z]{5}-){4}[0-9A-Z]{5}", RegexOptions.IgnoreCase)]
    private static partial Regex _TerracottaId();

    /// <summary>
    /// 换行符，包括 <c>\r\n</c> <c>\n</c> <c>\r</c> 三种。
    /// </summary>
    public static readonly Regex NewLine = _NewLine();
    [GeneratedRegex(@"\r\n|\n|\r")]
    private static partial Regex _NewLine();

    /// <summary>
    /// Semantic Versioning (SemVer) 规范的版本号，包含可选的 v 前缀。
    /// </summary>
    public static readonly Regex SemVer = _SemVer();
    private const string PatternSemVer =
        @"^v?(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)" +
        @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?" +
        @"(?:\+(?<build>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";
    [GeneratedRegex(PatternSemVer, RegexOptions.ExplicitCapture)]
    private static partial Regex _SemVer();

    /// <summary>
    /// 简单匹配 HTTP(S) URI，若需严格检查请使用 <see cref="FullHttpUri"/>。
    /// </summary>
    public static readonly Regex HttpUri = _HttpUri();
    private const string PatternHttpUri = @"^https?://(?:\[[^\]\s]+\]|[^/\s?#:]+)(?::\d{1,5})?(?:/[^\s?#]*)?(?:\?[^\s#]*)?(?:#\S*)?$";
    [GeneratedRegex(PatternHttpUri, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _HttpUri();

    /// <summary>
    /// 包含完整规则的 HTTP(S) URI，含有 <c>scheme</c> <c>host</c> <c>ipv6</c>
    /// <c>port</c> <c>path</c> <c>query</c> <c>fragment</c> 分组。
    /// </summary>
    public static readonly Regex FullHttpUri = _FullHttpUri();
    private const string PatternFullHttpUri =
        @"^(?<scheme>https?)://(?<host>localhost|(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3})" +
        @"|\[(?<ipv6>[0-9A-Fa-f:.]+)\]|(?:(?:[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?\.)+(?:[A-Za-z]{2,63}|xn--[" +
        @"A-Za-z0-9\-]{2,59})))(?::(?<port>6553[0-5]|655[0-2]\d|65[0-4]\d{2}|6[0-4]\d{3}|[1-5]\d{4}|[1-9]\d{0,3}))?" +
        @"(?<path>/[^\s?#]*)?(?:\?(?<query>[^\s#]*))?(?:#(?<fragment>[^\s]*))?$";
    [GeneratedRegex(PatternFullHttpUri, RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _FullHttpUri();

    /// <summary>
    /// LastPending(_Xxx).log 路径。
    /// </summary>
    public static readonly Regex LastPendingLogPath = _LastPendingLogPath();
    [GeneratedRegex(@"\\LastPending[_]?[^\\]*\.log$", RegexOptions.IgnoreCase)]
    private static partial Regex _LastPendingLogPath();

    /// <summary>
    /// Mod Loader 不兼容的错误提示。
    /// </summary>
    public static readonly Regex IncompatibleModLoaderErrorHint = _IncompatibleModLoaderErrorHint();
    [GeneratedRegex(@"(incompatible[\s\S]+'Fabric Loader' \(fabricloader\)|Mod ID: '(?:neo)?forge', Requested by '([^']+)')")]
    private static partial Regex _IncompatibleModLoaderErrorHint();

    /// <summary>
    /// Minecraft 颜色代码，为 Hex 颜色代码，格式为 <c>#RRGGBB</c>。
    /// </summary>
    public static readonly Regex HexColor = _HexColor();
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex _HexColor();
    
    /// <summary>
    /// A compiled regular expression for matching Minecraft MOTD formatting codes.
    /// Matches legacy color/format codes (e.g., §a, §b, §k) and hexadecimal color codes (e.g., #FF0000).
    /// </summary>
    public static readonly Regex MotdCode = _MotdCode();
    [GeneratedRegex("(§[0-9a-fk-oAr]|#[0-9A-Fa-f]{6})")]
    private static partial Regex _MotdCode();

    public static readonly Regex BroadcastMotd = _BroadcastMotd();
    [GeneratedRegex(@"\[MOTD\](.*?)\[/MOTD\]", RegexOptions.Compiled)]
    private static partial Regex _BroadcastMotd();

    public static readonly Regex BroadcastAd = _BroadcastAd();
    [GeneratedRegex(@"\[AD\](.*?)\[/AD\]", RegexOptions.Compiled)]
    private static partial Regex _BroadcastAd();

    /// <summary>
    /// 匹配 Minecraft 正常版本号，如 1.20.4、1.19.3 等。
    /// </summary>
    public static readonly Regex McNormalVersion = _McNormalVersion();
    [GeneratedRegex(@"^\d+\.\d+\.\d+$|^\d+\.\d+$")]
    private static partial Regex _McNormalVersion();

    /// <summary>
    /// 匹配 Minecraft 快照版本号，如 24w14a 等。
    /// </summary>
    public static readonly Regex McSnapshotVersion = _McSnapshotVersion();
    [GeneratedRegex(@"(\d+)w(\d+)([a-z]?)")]
    private static partial Regex _McSnapshotVersion();

    /// <summary>
    /// 匹配 Minecraft Indev 版本号，如 in-20091231-2、in-20100130 等。
    /// </summary>
    public static readonly Regex McIndevVersion = _McIndevVersion();
    [GeneratedRegex(@"^in-(\d{8})(-(\d+))?$")]
    private static partial Regex _McIndevVersion();

    /// <summary>
    /// 匹配 Minecraft Infdev 版本号，如 inf-20100611 等。
    /// </summary>
    public static readonly Regex McInfdevVersion = _McInfdevVersion();
    [GeneratedRegex(@"^inf-(\d{8})(-(\d+))?$")]
    private static partial Regex _McInfdevVersion();

    /// <summary>
    /// 匹配 accessToken 内容。
    /// </summary>
    public static readonly Regex AccessToken = _AccessToken();
    [GeneratedRegex("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})")]
    private static partial Regex _AccessToken();

    /// <summary>
    /// 使用 IsMatch 检查是否存在中文字符
    /// </summary>
    public static readonly Regex HasChineseChar = _HasChineseChar();
    [GeneratedRegex("[\u4e00-\u9fbb]")]
    private static partial Regex _HasChineseChar();

    /// <summary>
    /// 用 Replace 替换英文中的分隔特征
    /// </summary>
    public static readonly Regex EnglishSpacedKeywords = _EnglishSpacedKeywords();
    [GeneratedRegex("([A-Z]+|[a-z]+?)(?=[A-Z]+[a-z]+[a-z ]*)")]
    private static partial Regex _EnglishSpacedKeywords();
    
    /// <summary>
    /// NTFS 8.3 文件名格式
    /// </summary>
    public static readonly Regex Ntfs83FileName = _Ntfs83FileName();
    [GeneratedRegex(@".{2,}~\d")]
    private static partial Regex _Ntfs83FileName();
    
    public static readonly Regex UncPath = _UncPath();
    [GeneratedRegex("""^\\\\[^\\/:*?"<>|]+\\[^\\/:*?"<>|]+(\\[^\\/:*?"<>|]+)*\\?$""")]
    private static partial Regex _UncPath();

    #region Minecraft 实例解析

    public static readonly Regex OptiFineVersion = _OptiFineVersion();
    [GeneratedRegex(@"(?<=HD_U_)[^"":/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _OptiFineVersion();

    public static readonly Regex OptiFineLibVersion = _OptiFineLibVersion();
    [GeneratedRegex(@"(?<=HD_U_)[^"":/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _OptiFineLibVersion();

    public static readonly Regex LegacyFabricVersion = _LegacyFabricVersion();
    [GeneratedRegex(@"(?<=(net.fabricmc:fabric-loader:))[0-9\.]+(\+build.[0-9]+)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _LegacyFabricVersion();

    public static readonly Regex FabricVersion = _FabricVersion();
    [GeneratedRegex(@"(?<=(net.fabricmc:fabric-loader:))[0-9\.]+(\+build.[0-9]+)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _FabricVersion();

    public static readonly Regex QuiltVersion = _QuiltVersion();
    [GeneratedRegex(@"(?<=(org.quiltmc:quilt-loader:))[0-9\.]+(\+build.[0-9]+)?((-beta.)[0-9]([0-9]?))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _QuiltVersion();

    public static readonly Regex CleanroomVersion = _CleanroomVersion();
    [GeneratedRegex(@"(?<=(com.cleanroommc:cleanroom:))[0-9\.]+(\+build.[0-9]+)?(-alpha)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _CleanroomVersion();

    /// <summary>
    /// 匹配 Forge 主版本号（位于 "forge:X.Y.Z-" 之后）。
    /// </summary>
    public static readonly Regex ForgeMainVersion = _ForgeMainVersion();
    [GeneratedRegex(@"(?<=forge:[0-9\.]+(_pre[0-9]*)?\-)[0-9\.]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _ForgeMainVersion();

    /// <summary>
    /// 匹配 Forge Maven 坐标中的版本号（net.minecraftforge:minecraftforge:X.Y.Z）。
    /// </summary>
    public static readonly Regex ForgeLibVersion = _ForgeLibVersion();
    [GeneratedRegex(@"(?<=net\.minecraftforge:(?:forge|fmlloader):[0-9.]+-)[0-9a-zA-Z._+-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _ForgeLibVersion();

    /// <summary>
    /// 匹配 NeoForge 版本号（从 JSON 参数中提取，如 "--fml.neoForgeVersion", "20.6.119-beta"）。
    /// </summary>
    public static readonly Regex NeoForgeVersion = _NeoForgeVersion();
    [GeneratedRegex(@"(?<=orgeVersion"",[^""]*?"")[^""]+(?="",)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _NeoForgeVersion();

    public static readonly Regex FabricLikeLibVersion = _FabricLikeLibVersion();
    [GeneratedRegex(@"(?<=((fabricmc)|(quiltmc)|(legacyfabric)):intermediary:)[^""]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _FabricLikeLibVersion();

    public static readonly Regex LabyModVersion = _LabyModVersion();
    [GeneratedRegex(@"(?<=-Dnet.labymod.running-version=)1.[0-9+.]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _LabyModVersion();

    public static readonly Regex MinecraftJsonVersion = _MinecraftJsonVersion();
    [GeneratedRegex(@"(([1-9][0-9]w[0-9]{2}[a-g])|((1|[2-9][0-9])\.[0-9]+(\.[0-9]+)?(-(pre|rc|snapshot-?)[1-9]*| Pre-Release( [1-9])?)?))(_unobfuscated)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _MinecraftJsonVersion();

    public static readonly Regex MinecraftDownloadUrlVersion = _MinecraftDownloadUrlVersion();
    [GeneratedRegex(@"(?<=launcher.mojang.com/mc/game/)[^/]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _MinecraftDownloadUrlVersion();

    public static readonly Regex CatchLwjglInLib = _CatchLwjglInLib();
    [GeneratedRegex(@"(?<=org.lwjgl:)lwjgl(-[a-z._.\-.0-9]*)(?=(:[0-9].[0-9].[0-9](-[a-z.0-9._.\-]*)?:([a-z._.\-.0-9]*)?))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _CatchLwjglInLib();

    #endregion

    #region 外部组件

    public static readonly Regex ModIdMatch = _ModIdMatch();
    [GeneratedRegex(@"[0-9a-zA-Z_-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _ModIdMatch();
    #endregion
}

using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     某个 Minecraft 实例的版本名、附加组件信息。
/// </summary>
public class McInstanceInfo
{
    public const string UnknownVanillaName = "Unknown";

    /// <summary>
    ///     Cleanroom 版本号，如 0.2.4-alpha。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? Cleanroom;

    /// <summary>
    ///     Fabric 版本号，如 0.7.2.175。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? Fabric;

    /// <summary>
    ///     Forge 版本号，如 31.1.2、14.23.5.2847。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? Forge;

    // Cleanroom

    /// <summary>
    ///     该实例是否安装了 Cleanroom。
    /// </summary>
    public bool HasCleanroom;

    // Fabric

    /// <summary>
    ///     该实例是否安装了 Fabric。
    /// </summary>
    public bool HasFabric;

    // Forge

    /// <summary>
    ///     该实例是否安装了 Forge。
    /// </summary>
    public bool HasForge;

    // LabyMod

    /// <summary>
    ///     该实例是否安装了 LabyMod。
    /// </summary>
    public bool HasLabyMod;

    // LegacyFabric

    /// <summary>
    ///     该实例是否安装了 Fabric。
    /// </summary>
    public bool HasLegacyFabric;

    // LiteLoader

    /// <summary>
    ///     该实例是否安装了 LiteLoader。
    /// </summary>
    public bool HasLiteLoader;

    // NeoForge

    /// <summary>
    ///     该实例是否安装了 NeoForge。
    /// </summary>
    public bool HasNeoForge;

    // OptiFine

    /// <summary>
    ///     该实例是否通过 JSON 安装了 OptiFine。
    /// </summary>
    public bool HasOptiFine;


    // Quilt

    /// <summary>
    ///     该实例是否安装了 Quilt。
    /// </summary>
    public bool HasQuilt;

    /// <summary>
    ///     LabyMod 版本号，如 4.2.59。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? LabyMod;

    /// <summary>
    ///     Legacy Fabric 版本号，如 0.7.2.175。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? LegacyFabric;

    /// <summary>
    ///     NeoForge 版本号，如 21.0.2-beta、47.1.79。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? NeoForge;

    /// <summary>
    ///     OptiFine 版本号，如 C8、C9_pre10。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? OptiFine;

    /// <summary>
    ///     Quilt 版本号，如 0.26.1-beta.1、0.26.0。未安装或无法确定时为 <see langword="null" />。
    /// </summary>
    public string? Quilt;

    /// <summary>
    ///     指示原版版本号是否可靠（不是通过猜测获取）。
    /// </summary>
    public bool Reliable = true;

    // 原版

    /// <summary>
    ///     原版版本名。
    ///     如 26.1，26.1-snapshot-1，1.12.2，16w01a。
    /// </summary>
    public string VanillaName;

    /// <summary>
    ///     可比较的三段式原版版本号。
    ///     对老版本格式，例如 1.20.3，会被转换为 20.0.3。
    ///     若没有版本号，例如旧快照，则为 9999.0.0。
    /// </summary>
    public Version vanilla;

    /// <summary>
    ///     原版版本号是否有效。
    /// </summary>
    public bool Valid => vanilla.Major < 1000;

    /// <summary>
    ///     可供比较的原版 Drop 序数。
    ///     例如 26.3.2 为 263，1.21.5 为 210。
    ///     若没有版本号，例如旧快照，则直接指定为 209。
    /// </summary>
    public int Drop => Valid ? vanilla.Major * 10 + vanilla.Minor : 209;

    /// <summary>
    ///     可供比较的 OptiFine 版本序数。
    /// </summary>
    public int OptiFineCode
    {
        get
        {
            if (string.IsNullOrEmpty(OptiFine))
                return 0;
            // 合法 OptiFine 版本形如 C8、C9_pre10、G5；
            // 旧版本缓存可能保存本地化的「未知版本」文本，不能把首字符当作版本字母参与排序。
            if (OptiFine.Length < 2
                || !char.IsAsciiLetter(OptiFine[0])
                || !char.IsAsciiDigit(OptiFine[1]))
                return 0;
            // 字母编号，如 G2 中的 G（7）
            var result = char.ToUpperInvariant(OptiFine.First()) - 'A' + 1;
            // 末尾数字，如 C5 beta4 中的 5
            result *= 100;
            result = (int)Math.Round(result + LauncherText.Val(OptiFine[1..].RegexSeek("[0-9]+")));
            // 测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
            result *= 100;
            if (OptiFine.ContainsF("pre", true))
                result += 50;
            if (OptiFine.ContainsF("pre", true) || OptiFine.ContainsF("beta", true))
            {
                var lastChar = OptiFine[^1..];
                if (LauncherText.Val(lastChar) == 0d && lastChar != "0")
                    result += 1; // 为 pre 或 beta 结尾，视作 1
                else
                    result =
                        (int)Math.Round(
                            result
                            + LauncherText.Val(
                                OptiFine
                                    .ToLower()
                                    .RegexSeek("(?<=((pre)|(beta)))[0-9]+")));
            }
            else
            {
                result += 99;
            }

            return result;
        }
    }

    // Forgelike

    /// <summary>
    ///     该版本是否安装了 Forgelike 加载器。
    /// </summary>
    public bool HasForgelike => HasForge || HasNeoForge || HasCleanroom;

    /// <summary>
    ///     可供比较的类 Forge 版本序数。
    /// </summary>
    public int ForgelikeCode
    {
        get
        {
            if (!HasForgelike)
                return 0;
            var forgelikeVersion = !string.IsNullOrEmpty(Forge) ? Forge : NeoForge;
            if (string.IsNullOrEmpty(forgelikeVersion))
                return 0;
            var segments = forgelikeVersion.RegexSearch(@"\d+");
            // 旧版本缓存可能保存了本地化的「未知版本」文本，不含数字，不能进入下面的索引逻辑
            if (segments.Count == 0)
                return 0;
            switch (segments.Count)
            {
                case var @case when @case > 4:
                {
                    return (int)Math.Round(
                        LauncherText.Val(segments[0]) * 1000000d
                        + LauncherText.Val(segments[1]) * 10000d
                        + LauncherText.Val(segments[3]));
                }
                case 3:
                {
                    return (int)Math.Round(
                        LauncherText.Val(segments[0]) * 1000000d
                        + LauncherText.Val(segments[1]) * 10000d
                        + LauncherText.Val(segments[2]));
                }
                case 2:
                {
                    return (int)Math.Round(
                        LauncherText.Val(segments[0]) * 1000000d
                        + LauncherText.Val(segments[1]) * 10000d);
                }

                default:
                {
                    return (int)Math.Round(LauncherText.Val(segments[0]) * 1000000d);
                }
            }
        }
    }

    // Fabriclike

    /// <summary>
    ///     该版本是否安装了 Fabriclike 加载器。
    /// </summary>
    public bool HasFabriclike => HasFabric || HasQuilt || HasLegacyFabric;

    // API

    /// <summary>
    ///     生成对此实例信息的用户友好的描述性字符串。
    /// </summary>
    public override string ToString()
    {
        var toStringRet = "";
        if (HasForge)
            toStringRet += ", Forge" + FormatVersion(Forge);
        if (HasNeoForge)
            toStringRet += ", NeoForge" + FormatVersion(NeoForge);
        if (HasCleanroom)
            toStringRet += ", Cleanroom" + FormatVersion(Cleanroom);
        if (HasFabric)
            toStringRet += ", Fabric" + FormatVersion(Fabric);
        if (HasLegacyFabric)
            toStringRet += ", LegacyFabric" + FormatVersion(LegacyFabric);
        if (HasQuilt)
            toStringRet += ", Quilt" + FormatVersion(Quilt);
        if (HasLabyMod)
            toStringRet += ", LabyMod" + FormatVersion(LabyMod);
        if (HasOptiFine)
            toStringRet += ", OptiFine" + FormatVersion(OptiFine);
        if (HasLiteLoader)
            toStringRet += ", LiteLoader";
        if (string.IsNullOrEmpty(toStringRet))
            return Lang.Text("Minecraft.Version.Vanilla") + " " + VanillaName;

        return VanillaName + toStringRet;
    }

    // Helpers

    /// <summary>
    ///     将加载器版本号格式化为描述文本中的一段，例如 <c>" 47.2.0"</c>。
    ///     版本号未知时返回空字符串，便于调用方直接拼接。
    /// </summary>
    /// <param name="version">加载器版本号，可能为 <see langword="null" /> 或空。</param>
    /// <param name="normalize">是否把版本号中的 <c>-</c> 与 <c>_</c> 替换为空格，用于 OptiFine 这类版本号。</param>
    public static string FormatVersion(string? version, bool normalize = false)
    {
        if (string.IsNullOrEmpty(version)) return "";
        return " " + (normalize ? version.Replace("-", " ").Replace("_", " ") : version);
    }

    /// <summary>
    ///     版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
    /// </summary>
    public static bool IsFormatFit(string version)
    {
        if (version is null)
            return false;
        if (version.RegexCheck(@"^1\.\d"))
            return true;
        if (LauncherText.Val(version.RegexSeek(@"^[2-9]\d\.\d+")) > 25d)
            return true;
        return false;
    }

    /// <summary>
    ///     尝试将版本字符串转换为 Drop 序数。
    ///     若无法转换则返回 -1。
    /// </summary>
    public static int VersionToDrop(string? version, bool allowSnapshot = false)
    {
        if (string.IsNullOrEmpty(version))
            return -1;

        var lower = version.ToLowerInvariant();

        if (lower.StartsWith("1.rv")) return 90; // 1.rv-pre1,约1.9
        if (lower.StartsWith("3d shareware")) return 140; // 3d shareware v1.34,约1.14

        if (!allowSnapshot && lower.Contains('-'))
            return -1;

        var baseVer = lower.BeforeFirst("-");
        var segments = baseVer.Split('.');

        //XXwYY的快照
        // 按年份估算
        if (allowSnapshot
            && segments.Length == 1
            && segments[0].Length >= 3
            && segments[0][2] == 'w'
            && char.IsDigit(segments[0][0])
            && char.IsDigit(segments[0][1]))
        {
            var year = int.Parse(segments[0][..2]);
            if (year <= 16) return Math.Max(year * 10 - 70, 20);
            if (year == 17) return 120;
            return Math.Min(130 + (year - 18) * 10, 210);
        }

        if (segments.Length < 2)
            return -1;
        var major = (int)Math.Round(LauncherText.Val(segments[0]));
        var minor = (int)Math.Round(LauncherText.Val(segments[1]));
        if (major == 1) return minor * 10;
        if (major >= 25) return major * 10 + minor;
        if (major == 2) return 50; //2.0愚人节

        return -1;
    }

    /// <summary>
    ///     获取原版版本名用于展示；无法识别时返回当前语言的「未知版本」。
    /// </summary>
    /// <param name="vanillaName">原版版本名。</param>
    public static string GetVanillaNameDisplay(string? vanillaName)
    {
        return string.IsNullOrEmpty(vanillaName) || vanillaName == UnknownVanillaName
            ? Lang.Text("Minecraft.Version.Unknown")
            : vanillaName;
    }

    /// <summary>
    ///     将 Drop 序数转换为版本字符串。
    /// </summary>
    public static string DropToVersion(int drop)
    {
        if (drop >= 250) return $"{drop / 10}.{drop % 10}";

        return $"1.{drop / 10}";
    }
}
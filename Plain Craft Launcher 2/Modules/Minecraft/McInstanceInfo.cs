using PCL.Core.App.Localization;

namespace PCL;

    /// <summary>
    ///     某个 Minecraft 实例的版本名、附加组件信息。
    /// </summary>
    public class McInstanceInfo
    {
        /// <summary>
        ///     Cleanroom 版本号，如 0.2.4-alpha。
        /// </summary>
        public string Cleanroom = "";

        /// <summary>
        ///     Fabric 版本号，如 0.7.2.175。
        /// </summary>
        public string Fabric = "";

        /// <summary>
        ///     Forge 版本号，如 31.1.2、14.23.5.2847。
        /// </summary>
        public string Forge = "";

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
        ///     LabyMod 版本号，如 4.2.59。
        /// </summary>
        public string LabyMod = "";

        /// <summary>
        ///     Fabric 版本号，如 0.7.2.175。
        /// </summary>
        public string LegacyFabric = "";

        /// <summary>
        ///     NeoForge 版本号，如 21.0.2-beta、47.1.79。
        /// </summary>
        public string NeoForge = "";

        /// <summary>
        ///     OptiFine 版本号，如 C8、C9_pre10。
        /// </summary>
        public string OptiFine = "";

        /// <summary>
        ///     Quilt 版本号，如 0.26.1-beta.1、0.26.0。
        /// </summary>
        public string Quilt = "";

        /// <summary>
        ///     指示原版版本号是否可靠（不是通过猜测获取）。
        /// </summary>
        public bool Reliable = true;

        /// <summary>
        ///     可比较的三段式原版版本号。
        ///     对老版本格式，例如 1.20.3，会被转换为 20.0.3。
        ///     若没有版本号，例如旧快照，则为 9999.0.0。
        /// </summary>
        public Version vanilla;

        // 原版

        /// <summary>
        ///     原版版本名。
        ///     如 26.1，26.1-snapshot-1，1.12.2，16w01a。
        /// </summary>
        public string VanillaName;

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
                if (string.IsNullOrEmpty(OptiFine) || OptiFine == Lang.Text("Minecraft.Version.Unknown"))
                    return 0;
                // 字母编号，如 G2 中的 G（7）
                var result = char.ToUpperInvariant(OptiFine.First()) - 'A' + 1;
                // 末尾数字，如 C5 beta4 中的 5
                result *= 100;
                result = (int)Math.Round(result +
                                         ModBase.Val(OptiFine[1..].RegexSeek("[0-9]+")));
                // 测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
                result *= 100;
                if (OptiFine.ContainsF("pre", true))
                    result += 50;
                if (OptiFine.ContainsF("pre", true) || OptiFine.ContainsF("beta", true))
                {
                    var lastChar = OptiFine[^1..];
                    if (ModBase.Val(lastChar) == 0d && lastChar != "0")
                        result += 1; // 为 pre 或 beta 结尾，视作 1
                    else
                        result =
                            (int)Math.Round(result +
                                            ModBase.Val(OptiFine.ToLower().RegexSeek("(?<=((pre)|(beta)))[0-9]+")));
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
                if ((string.IsNullOrEmpty(Forge) || Forge == Lang.Text("Minecraft.Version.Unknown")) &&
                    (string.IsNullOrEmpty(NeoForge) || NeoForge == Lang.Text("Minecraft.Version.Unknown")))
                    return 0;
                var segments = (HasForge ? Forge : NeoForge).RegexSearch(@"\d+");
                switch (segments.Count)
                {
                    case var @case when @case > 4:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d +
                                               ModBase.Val(segments[3]));
                    }
                    case 3:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d +
                                               ModBase.Val(segments[2]));
                    }
                    case 2:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d + ModBase.Val(segments[1]) * 10000d);
                    }

                    default:
                    {
                        return (int)Math.Round(ModBase.Val(segments[0]) * 1000000d);
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
            string toStringRet = default;
            toStringRet = "";
            if (HasForge)
                toStringRet += ", Forge" + (Forge == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Forge);
            if (HasNeoForge)
                toStringRet += ", NeoForge" +
                               (NeoForge == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + NeoForge);
            if (HasCleanroom)
                toStringRet += ", Cleanroom" +
                               (Cleanroom == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Cleanroom);
            if (HasFabric)
                toStringRet += ", Fabric" + (Fabric == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Fabric);
            if (HasLegacyFabric)
                toStringRet += ", LegacyFabric" +
                               (LegacyFabric == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + LegacyFabric);
            if (HasQuilt)
                toStringRet += ", Quilt" + (Quilt == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + Quilt);
            if (HasLabyMod)
                toStringRet += ", LabyMod" + (LabyMod == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + LabyMod);
            if (HasOptiFine)
                toStringRet += ", OptiFine" +
                               (OptiFine == Lang.Text("Minecraft.Version.Unknown") ? "" : " " + OptiFine);
            if (HasLiteLoader)
                toStringRet += ", LiteLoader";
            if (string.IsNullOrEmpty(toStringRet)) return Lang.Text("Minecraft.Version.Vanilla") + " " + VanillaName;

            return VanillaName + toStringRet;
        }

        // Helpers

        /// <summary>
        ///     版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
        /// </summary>
        public static bool IsFormatFit(string version)
        {
            if (version is null)
                return false;
            if (version.RegexCheck(@"^1\.\d"))
                return true;
            if (ModBase.Val(version.RegexSeek(@"^[2-9]\d\.\d+")) > 25d)
                return true;
            return false;
        }

        /// <summary>
        ///     尝试将版本字符串转换为 Drop 序数。
        ///     若无法转换则返回 0。
        /// </summary>
        public static int VersionToDrop(string? version, bool allowSnapshot = false)
        {
            if (!allowSnapshot && version.Contains("-"))
                return 0;
            if (version is null)
                return 0;
            var segments = version.BeforeFirst("-").Split(".");
            if (segments.Length < 2)
                return 0;
            var major = (int)Math.Round(ModBase.Val(segments[0]));
            var minor = (int)Math.Round(ModBase.Val(segments[1]));
            if (major == 1) return minor * 10;

            if (major < 25) return 0;

            return major * 10 + minor;
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
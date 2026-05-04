using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Network;


namespace PCL;

public static partial class ModLaunch
{
    #region Java 处理

    public static JavaEntry McLaunchJavaSelected;

    private static void McLaunchJava(ModLoader.LoaderTask<int, int> task)
    {
        var minVer = new Version(0, 0, 0, 0);
        var maxVer = new Version(999, 999, 999, 999);

        // MC 大版本检测
        if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
             ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2024, 4, 2)) ||
            (ModMinecraft.McInstanceSelected.Info.Valid &&
             ModMinecraft.McInstanceSelected.Info.Vanilla >= new Version(20, 0, 5)))
        {
            // 1.20.5+ (24w14a+)：至少 Java 21
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] MC 1.20.5+ (24w14a+) 要求至少 Java 21");
            minVer = new Version(21, 0, 0, 0);
        }
        else if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2021, 11, 16)) ||
                 (ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18))
        {
            // 1.18 pre2+：至少 Java 17
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] MC 1.18 pre2+ 要求至少 Java 17");
            minVer = new Version(17, 0, 0, 0);
        }
        else if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2021, 5, 11)) ||
                 (ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 17))
        {
            // 1.17+ (21w19a+)：至少 Java 16
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] MC 1.17+ (21w19a+) 要求至少 Java 16");
            minVer = new Version(16, 0, 0, 0);
        }
        else if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017) // Minecraft 1.12 与 1.11 的分界线正好是 2017 年，太棒了
        {
            // 1.12+：至少 Java 8
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] MC 1.12+ 要求至少 Java 8");
            minVer = new Version(1, 8, 0, 0);
        }
        else if (ModMinecraft.McInstanceSelected.ReleaseTime <= new DateTime(2013, 5, 1) &&
                 ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2001) // 避免某些版本写个 1960 年
        {
            // 1.5.2-：最高 Java 8
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] MC 1.5.2- 要求最高 Java 12");
            maxVer = new Version(1, 8, 999, 999);
        }

        // 原版 26+：获取 Mojang 要求的 Java 版本
        string recommendedComponent = null;
        var recommendedCode =
            ModMinecraft.McInstanceSelected.JsonObject?["javaVersion"]?["majorVersion"]?.ToObject<int>() ??
            ModMinecraft.McInstanceSelected.JsonVersion?["java_version"]?.ToObject<int>() ?? 0;
        if (recommendedCode >= 22)
        {
            McLaunchLog("Mojang 要求至少使用 Java " + recommendedCode);
            minVer = new Version(1, recommendedCode, 0, 0);
            recommendedComponent =
                ModMinecraft.McInstanceSelected.JsonObject?["javaVersion"]?["component"]?.ToString() ??
                ModMinecraft.McInstanceSelected.JsonVersion?["java_component"]?.ToString();
            if (string.IsNullOrEmpty(recommendedComponent))
                recommendedComponent = null;
        }

        // OptiFine 检测
        if (ModMinecraft.McInstanceSelected.Info.HasOptiFine && ModMinecraft.McInstanceSelected.Info.Valid) // 不管非标准版本
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 7)
            {
                // <1.7：至多 Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 8 &&
                     ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 12)
            {
                // 1.8 - 1.11：必须恰好 Java 8
                minVer = new Version(1, 8, 0, 0);
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major == 12)
            {
                // 1.12：最高 Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
        }

        // Forge 检测
        if (ModMinecraft.McInstanceSelected.Info.HasForge)
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla >= new Version(6, 0, 1) &&
                ModMinecraft.McInstanceSelected.Info.Vanilla <= new Version(7, 0, 2))
            {
                // 1.6.1 - 1.7.2：必须 Java 7
                minVer = new Version(1, 7, 0, 0) > minVer ? new Version(1, 7, 0, 0) : minVer;
                maxVer = new Version(1, 7, 999, 999) < maxVer ? new Version(1, 7, 999, 999) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 12 ||
                     !ModMinecraft.McInstanceSelected.Info.Valid) // 非标准版本
            {
                // <=1.12：Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 14)
            {
                // 1.13 - 1.14：Java 8 - 10
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
                maxVer = new Version(1, 10, 999, 999) < maxVer ? new Version(1, 10, 999, 999) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major == 15)
            {
                // 1.15：Java 8 - 15
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
                maxVer = new Version(1, 15, 999, 999) < maxVer ? new Version(1, 15, 999, 999) : maxVer;
            }
            else if (ModMinecraft.CompareVersionGe(ModMinecraft.McInstanceSelected.Info.Forge, "34.0.0") &&
                     ModMinecraft.CompareVersionGe("36.2.25", ModMinecraft.McInstanceSelected.Info.Forge))
            {
                // 1.16，Forge 34.X ~ 36.2.25：最高 Java 8u321
                maxVer = new Version(1, 8, 0, 320) < maxVer ? new Version(1, 8, 0, 321) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18 &&
                     ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 19 &&
                     ModMinecraft.McInstanceSelected.Info.HasOptiFine) // #305
            {
                // 1.18：若安装了 OptiFine，最高 Java 18
                maxVer = new Version(1, 18, 999, 999) < maxVer ? new Version(1, 18, 999, 999) : maxVer;
            }
        }

        // Cleanroom 检测
        if (ModMinecraft.McInstanceSelected.Info.HasCleanroom)
        {
            if (!Version.TryParse(ModMinecraft.McInstanceSelected.Info.Cleanroom.Split('-')[0], out Version cleanroomVersion))
                throw new FormatException("无法解析 Cleanroom 版本号：" + ModMinecraft.McInstanceSelected.Info.Cleanroom);
            if (cleanroomVersion < new Version(0, 5, 0, 0))
            {
                if (ModBase.ModeDebug) ModBase.Log("[Launch] [Debug] Cleanroom 版本低于 0.5，要求至少 Java 21");
                minVer = new Version(21, 0, 0, 0) > minVer ? new Version(21, 0, 0, 0) : minVer;
            }
            else
            {
                if (ModBase.ModeDebug) ModBase.Log("[Launch] [Debug] Cleanroom 版本高于 0.5，要求至少 Java 25");
                minVer = new Version(25, 0, 0, 0) > minVer ? new Version(25, 0, 0, 0) : minVer;
            }
        }

        // Fabric 检测
        if (ModMinecraft.McInstanceSelected.Info.HasFabric && ModMinecraft.McInstanceSelected.Info.Valid) // 不管非标准版本
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 15 &&
                ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 16)
                // 1.15 - 1.16：Java 8+
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18)
                // 1.18+：Java 17+
                minVer = new Version(1, 17, 0, 0) > minVer ? new Version(1, 17, 0, 0) : minVer;
        }

        // LiteLoader 检测
        if (ModMinecraft.McInstanceSelected.Info.HasLiteLoader && ModMinecraft.McInstanceSelected.Info.Valid)
        {
            // 最高 Java 8
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] LiteLoader 要求最高 Java 8");
            maxVer = new Version(8, 999, 999, 999) < maxVer ? new Version(8, 999, 999, 999) : maxVer;
        }

        // LabyMod 检测
        if (ModMinecraft.McInstanceSelected.Info.HasLabyMod)
        {
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] LabyMod 要求至少 Java 21");
            minVer = new Version(21, 0, 0, 0) > minVer ? new Version(21, 0, 0, 0) : minVer;
            maxVer = new Version(999, 999, 999, 999);
        }

        // JSON 中要求的版本
        if (ModMinecraft.McInstanceSelected.JsonObject["javaVersion"] is not null)
        {
            var majorVersion = MigrationHelpers.Val(ModMinecraft.McInstanceSelected.JsonObject["javaVersion"]["majorVersion"]);
            if (LauncherLogger.ModeDebug)
                LauncherLogger.Log("[Launch] [Debug] JSON 中参数要求至少 Java " + majorVersion);
            if (majorVersion <= 8d)
                minVer = new Version(1, (int)Math.Round(majorVersion), 0, 0) > minVer
                    ? new Version(1, (int)Math.Round(majorVersion), 0, 0)
                    : minVer;
            else
                minVer = new Version((int)Math.Round(majorVersion), 0, 0, 0) > minVer
                    ? new Version((int)Math.Round(majorVersion), 0, 0, 0)
                    : minVer;

            if (maxVer < minVer)
                maxVer = new Version(999, 999, 999, 999);
        }

        lock (ModJava.JavaLock)
        {
            // 选择 Java
            McLaunchLog("Java 版本需求：最低 " + minVer + "，最高 " + maxVer);
            McLaunchJavaSelected = ModJava.JavaSelect("$$", minVer, maxVer, ModMinecraft.McInstanceSelected);
            if (task.IsAborted)
                return;
            if (McLaunchJavaSelected is not null)
            {
                McLaunchLog("选择的 Java：" + McLaunchJavaSelected.ToString);
                return;
            }

            // 无合适的 Java
            if (task.IsAborted)
                return; // 中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            McLaunchLog("无合适的 Java，需要确认是否自动下载");
            string javaCode;
            if (minVer >= new Version(1, 9))
            {
                javaCode = minVer.Major.ToString();
            }
            else if (maxVer < new Version(1, 8))
            {
                if (ModMinecraft.McInstanceSelected.Info.HasForge)
                    ModMain.MyMsgBox(
                        $"你需要先安装 LegacyJavaFixer Mod，或安装 Java 7 才能启动该版本。{"\r\n"}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                        "未找到 Java");
                else
                    ModMain.MyMsgBox(
                        $"你需要安装 Java 7 才能启动该版本。{"\r\n"}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                        "未找到 Java");
                throw new Exception("$$");
            }
            else if (minVer > new Version(1, 8, 0, 140) && maxVer < new Version(1, 8, 0, 321))
            {
                ModMain.MyMsgBox(
                    $"你需要安装 Java 8u141 ~ 8u320 才能启动该版本。{"\r\n"}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                    "未找到 Java");
                throw new Exception("$$");
            }
            else if (minVer > new Version(1, 8, 0, 140))
            {
                ModMain.MyMsgBox(
                    $"你需要安装 Java 8u141 或更高版本的 Java 8 才能启动该版本。{"\r\n"}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                    "未找到 Java");
                throw new Exception("$$");
            }
            else
            {
                javaCode = 8.ToString();
            }

            if (!ModJava.JavaDownloadConfirm($"Java {javaCode}"))
                throw new Exception("$$");
            // 开始自动下载
            var javaLoader = ModJava.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(recommendedComponent ?? javaCode, true); // 在 Java 22+ 时优先使用 Mojang 提供的 Component 字段
                while (javaLoader.State == LoadState.Loading && !task.IsAborted)
                {
                    task.Progress = javaLoader.Progress;
                    Thread.Sleep(10);
                }
            }
            finally
            {
                javaLoader.Abort(); // 确保取消时中止 Java 下载
            }

            // 检查下载结果
            McLaunchJavaSelected = ModJava.JavaSelect("$$", minVer, maxVer, ModMinecraft.McInstanceSelected);
            if (task.IsAborted)
                return;
            if (McLaunchJavaSelected is not null)
            {
                McLaunchLog("选择的 Java：" + McLaunchJavaSelected);
            }
            else
            {
                ModMain.Hint("没有可用的 Java，已取消启动！", ModMain.HintType.Critical);
                throw new Exception("$$");
            }
        }
    }

    #endregion

    #region 启动参数
    
    internal static void SecretLaunchJvmArgs(ref List<string> DataList)
    {
        var DataJvmCustom =
            Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", ModMinecraft.McInstanceSelected));
        DataList.Insert(0,
            Conversions.ToString(string.IsNullOrEmpty(DataJvmCustom)
                ? Config.Launch.JvmArgs
                : DataJvmCustom)); // 可变 JVM 参数
        switch (Config.Launch.PreferredIpStack)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
            {
                DataList.Add("-Djava.net.preferIPv4Stack=true");
                DataList.Add("-Djava.net.preferIPv4Addresses=true");
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
            {
                DataList.Add("-Djava.net.preferIPv6Stack=true");
                DataList.Add("-Djava.net.preferIPv6Addresses=true");
                break;
            }
        }

        double availableGb = KernelInterop.GetAvailablePhysicalMemoryBytes() / 1073741824.0;
        ModLaunch.McLaunchLog($"当前剩余内存：{availableGb:N1}G");
        double totalRamMb = PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected) * 1024d;
        DataList.Add($"-Xmn{Math.Floor(totalRamMb * 0.15)}m");
        DataList.Add($"-Xmx{Math.Floor(totalRamMb)}m");
        if (!DataList.Any(d => d.Contains("-Dlog4j2.formatMsgNoLookups=true")))
            DataList.Add("-Dlog4j2.formatMsgNoLookups=true");
    }

    public partial class LaunchArgument
    {
        private readonly List<string> _features = new();

        public LaunchArgument(ModMinecraft.McInstance Minecraft)
        {
            var curArgu = string.Empty;
            if (Minecraft.IsOldJson)
                _features = Minecraft.JsonObject["minecraftArguments"].ToString().Split(' ').ToList();
            else
                foreach (var item in Minecraft.JsonObject["arguments"]["game"])
                    if (item.Type == JTokenType.String)
                        _features.Add(item.ToString());
                    else if (item.Type == JTokenType.Object)
                        _features.AddRange(item["value"].Select(x => x.ToString()));
        }

        public object HasArguments(string key)
        {
            return _features.Contains(key);
        }
    }

    private static string McLaunchArgument;

    /// <summary>
    ///     释放 Java Wrapper 并返回完整文件路径。
    /// </summary>
    public static string ExtractJavaWrapper()
    {
        var WrapperPath = LauncherPaths.PureAsciiDirectory + "JavaWrapper.jar";
        LauncherLogger.Log("[Java] 选定的 Java Wrapper 路径：" + WrapperPath);
        lock (ExtractJavaWrapperLock) // 避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
        {
            try
            {
                WriteJavaWrapper(WrapperPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(WrapperPath))
                {
                    // 因为未知原因 Java Wrapper 可能变为只读文件（#4243）
                    LauncherLogger.Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", LauncherLogger.LogLevel.Developer);
                    try
                    {
                        File.Delete(WrapperPath);
                        WriteJavaWrapper(WrapperPath);
                    }
                    catch (Exception ex2)
                    {
                        LauncherLogger.Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", LauncherLogger.LogLevel.Developer);
                        WrapperPath = LauncherPaths.PureAsciiDirectory + "JavaWrapper2.jar";
                        try
                        {
                            WriteJavaWrapper(WrapperPath);
                        }
                        catch (Exception ex3)
                        {
                            throw new FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException("释放 Java Wrapper 失败", ex);
                }
            }
        }

        return WrapperPath;
    }

    private static readonly object ExtractJavaWrapperLock = new();

    private static void WriteJavaWrapper(string Path)
    {
        LauncherFileSystem.WriteFile(Path, LauncherPaths.GetResourceStream("Resources/java-wrapper.jar"));
    }

    /// <summary>
    ///     释放 linkd 并返回完整文件路径。
    /// </summary>
    public static string ExtractLinkD()
    {
        var LinkDPath = LauncherPaths.PureAsciiDirectory + "linkd.exe";
        lock (ExtractLinkDLock) // 避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
        {
            try
            {
                WriteLinkD(LinkDPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(LinkDPath))
                {
                    LauncherLogger.Log(ex, "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成", LauncherLogger.LogLevel.Developer);
                    try
                    {
                        File.Delete(LinkDPath);
                        WriteLinkD(LinkDPath);
                    }
                    catch (Exception ex2)
                    {
                        throw new FileNotFoundException("释放 linkd 失败", ex2);
                    }
                }
                else
                {
                    throw new FileNotFoundException("释放 linkd 失败", ex);
                }
            }
        }

        return LinkDPath;
    }

    private static readonly object ExtractLinkDLock = new();

    private static void WriteLinkD(string Path)
    {
        LauncherFileSystem.WriteFile(Path, LauncherPaths.GetResourceStream("Resources/linkd.exe"));
    }

    /// <summary>
    ///     判断是否使用 RetroWrapper。
    ///     TODO: 在更换为 Drop 比较版本号后可能不准确，需要测试确认。
    /// </summary>
    private static bool McLaunchNeedsRetroWrapper(ModMinecraft.McInstance Mc)
    {
        return Conversions.ToBoolean((Mc.ReleaseTime >= new DateTime(2013, 6, 25) && Mc.Info.Drop == 99) ||
                                     (Mc.Info.Drop < 60 && Mc.Info.Drop != 99 &&
                                      !(bool)Config.Launch.DisableRw &&
                                      !(bool)LauncherEnvironment.Setup.Get("VersionAdvanceDisableRW", Mc))); // <1.6
    }

    /// <summary>
    /// 获取实例所依赖的 LWJGL 版本
    /// </summary>
    private static string McLaunchGetLwjglVersion(ModMinecraft.McInstance mc)
    {
        foreach (ModMinecraft.McLibToken library in ModMinecraft.McLibListGet(mc, false))
        {
            if (string.IsNullOrWhiteSpace(library.OriginalName))
                continue;

            string[] parts = library.OriginalName.Split(':');
            if (parts.Length >= 3 &&
                parts[0].Equals("org.lwjgl", StringComparison.OrdinalIgnoreCase) &&
                parts[1].Equals("lwjgl", StringComparison.OrdinalIgnoreCase))
            {
                return parts[2];
            }
        }

        return null;
    }

    /// <summary>
    /// 判断是否启用了针对 Minecraft 26.1 的性能问题补丁
    /// </summary>
    private static bool McLaunchUsesLwjglUnsafeAgent(ModMinecraft.McInstance mc)
    {
        if (McLaunchGetLwjglVersion(mc) == "3.4.1")
        {
            bool globalDisabled = Config.Launch.DisableLwjglUnsafeAgent;
            bool instanceDisabled = Config.Instance.DisableLwjglUnsafeAgent[mc];

            return !globalDisabled && !instanceDisabled;
        }
        else
        {
            return false;
        }
    }

    // 主方法，合并 Jvm、Game、Replace 三部分的参数数据
    private static void McLaunchArgumentMain(ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> Loader)
    {
        McLaunchLog("开始获取 Minecraft 启动参数");
        // 获取基准字符串与参数信息
        string Arguments;
        if (ModMinecraft.McInstanceSelected.JsonObject["arguments"] is not null &&
            ModMinecraft.McInstanceSelected.JsonObject["arguments"]["jvm"] is not null)
        {
            McLaunchLog("获取新版 JVM 参数");
            Arguments = McLaunchArgumentsJvmNew(ModMinecraft.McInstanceSelected);
            McLaunchLog("新版 JVM 参数获取成功：");
            McLaunchLog(Arguments);
        }
        else
        {
            McLaunchLog("获取旧版 JVM 参数");
            Arguments = McLaunchArgumentsJvmOld(ModMinecraft.McInstanceSelected);
            McLaunchLog("旧版 JVM 参数获取成功：");
            McLaunchLog(Arguments);
        }

        if (!string.IsNullOrEmpty(
                (string)ModMinecraft.McInstanceSelected.JsonObject["minecraftArguments"])) // 有的实例 JSON 中是空字符串
        {
            McLaunchLog("获取旧版 Game 参数");
            Arguments += " " + McLaunchArgumentsGameOld(ModMinecraft.McInstanceSelected);
            McLaunchLog("旧版 Game 参数获取成功");
        }

        if (ModMinecraft.McInstanceSelected.JsonObject["arguments"] is not null &&
            ModMinecraft.McInstanceSelected.JsonObject["arguments"]["game"] is not null)
        {
            McLaunchLog("获取新版 Game 参数");
            Arguments += " " + McLaunchArgumentsGameNew(ModMinecraft.McInstanceSelected);
            McLaunchLog("新版 Game 参数获取成功");
        }

        // 编码参数（#4700、#5892、#5909）
        if (McLaunchJavaSelected.Installation.MajorVersion > 8)
        {
            if (!Arguments.Contains("-Dstdout.encoding="))
                Arguments = "-Dstdout.encoding=UTF-8 " + Arguments;
            if (!Arguments.Contains("-Dstderr.encoding="))
                Arguments = "-Dstderr.encoding=UTF-8 " + Arguments;
        }

        if (McLaunchJavaSelected.Installation.MajorVersion >= 18)
            if (!Arguments.Contains("-Dfile.encoding="))
                Arguments = "-Dfile.encoding=COMPAT " + Arguments;
        // MJSB
        Arguments = Arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"");
        // 全屏
        if (Conversions.ToBoolean(
                Operators.ConditionalCompareObjectEqual(Config.Launch.GameWindowMode, 0, false)))
            Arguments += " --fullscreen";
        // 由 Option 传入的额外参数
        foreach (var Arg in CurrentLaunchOptions.ExtraArgs)
            Arguments += " " + Arg.Trim();
        // 自定义参数
        var ArgumentGame =
            Conversions.ToString(LauncherEnvironment.Setup.Get("VersionAdvanceGame", ModMinecraft.McInstanceSelected));
        Arguments = Conversions.ToString(Arguments + Operators.ConcatenateObject(" ",
            string.IsNullOrEmpty(ArgumentGame) ? Config.Launch.GameArgs : ArgumentGame));
        // 替换参数
        var ReplaceArguments = McLaunchArgumentsReplace(ModMinecraft.McInstanceSelected, ref Loader);
        if (string.IsNullOrWhiteSpace(ReplaceArguments["${version_type}"]))
        {
            // 若自定义信息为空，则去掉该部分
            Arguments = Arguments.Replace(" --versionType ${version_type}", "");
            ReplaceArguments["${version_type}"] = "\"\"";
        }

        var FinalArguments = "";
        foreach (var ArgumentRaw in Arguments.Split(" "))
        {
            var Argument = ArgumentRaw;
            foreach (var Entry in ReplaceArguments)
                Argument = Argument.Replace(Entry.Key, Entry.Value);
            if ((Argument.Contains(" ") || Argument.Contains(@":\")) && !Argument.EndsWithF("\""))
                Argument = $"\"{Argument}\"";
            FinalArguments += Argument + " ";
        }

        FinalArguments = FinalArguments.TrimEnd();
        // 进存档
        var WorldName = CurrentLaunchOptions.WorldName;
        if (WorldName is not null) FinalArguments += $" --quickPlaySingleplayer \"{WorldName}\"";
        // 进服
        var Server = Conversions.ToString(string.IsNullOrEmpty(CurrentLaunchOptions.ServerIp)
            ? LauncherEnvironment.Setup.Get("VersionServerEnter", ModMinecraft.McInstanceSelected)
            : CurrentLaunchOptions.ServerIp);
        if (string.IsNullOrWhiteSpace(WorldName) && !string.IsNullOrWhiteSpace(Server))
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime > new DateTime(2023, 4, 4))
            {
                // QuickPlay
                FinalArguments += $" --quickPlayMultiplayer \"{Server}\"";
            }
            else
            {
                // 老版本
                if (Server.Contains(":"))
                    // 包含端口号
                    FinalArguments += " --server " + Server.Split(":")[0] + " --port " + Server.Split(":")[1];
                else
                    // 不包含端口号
                    FinalArguments += " --server " + Server + " --port 25565";
                if (ModMinecraft.McInstanceSelected.Info.HasOptiFine)
                    ModMain.Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", ModMain.HintType.Critical);
            }
        }

        // 输出
        McLaunchLog("Minecraft 启动参数：");
        McLaunchLog(FinalArguments);
        McLaunchArgument = FinalArguments;
    }

    // Jvm 部分（第一段）
    private static string McLaunchArgumentsJvmOld(ModMinecraft.McInstance instance)
    {
        // 存储以空格为间隔的启动参数列表
        var DataList = new List<string>();

        // 输出固定参数
        DataList.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        var ArgumentJvm = Conversions.ToString(LauncherEnvironment.Setup.Get("VersionAdvanceJvm", ModMinecraft.McInstanceSelected));
        if (string.IsNullOrEmpty(ArgumentJvm))
            ArgumentJvm = Conversions.ToString(Config.Launch.JvmArgs);
        if (!ArgumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true"))
            ArgumentJvm += " -Dlog4j2.formatMsgNoLookups=true";
        ArgumentJvm = ArgumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", ""); // #3511 的清理
        DataList.Insert(0, ArgumentJvm); // 可变 JVM 参数
        DataList.Add("-Xmn" +
                     Math.Floor(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                         !McLaunchJavaSelected.Installation.Is64Bit) * 1024d * 0.15d) + "m");
        DataList.Add("-Xmx" +
                     Math.Floor(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                         !McLaunchJavaSelected.Installation.Is64Bit) * 1024d) + "m");
        DataList.Add("\"-Djava.library.path=" + GetNativesFolder() + "\"");
        DataList.Add("-cp ${classpath}"); // 把支持库添加进启动参数表

        // Authlib-Injector
        if (McLoginLoader.Output.Type == "Auth")
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 6)
                DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT"); // 信任系统根证书（Meloong-Git/#5252）
            var Server = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "");
            try
            {
                var Response = Requester.FetchString(Server);
                DataList.Insert(0,
                    "-javaagent:\"" + LauncherPaths.PureAsciiDirectory + "authlib-injector.jar\"=" + Server +
                    " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
            }
            catch (WebException ex)
            {
                throw new Exception(
                    $"无法连接到第三方登录服务器（{Server ?? null}）{"\r\n"}详细信息：" + ex.InnerException, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"无法连接到第三方登录服务器（{Server ?? null}）", ex);
            }
        }

        // LWJGL Unsafe Agent
        if (McLaunchUsesLwjglUnsafeAgent(ModMinecraft.McInstanceSelected))
        {
            DataList.Insert(0, $"-javaagent:\"{LauncherPaths.PureAsciiDirectory}lwjgl-unsafe-agent.jar\"");
        }

        if (Config.Instance.UseDebugLof4j2Config[instance.PathIndie])
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017)
                DataList.Insert(0, "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractDebugLog4j2Config() + "\"");
            else
                DataList.Insert(0,
                    "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() + "\"");
        }

        // 渲染器
        var Renderer = 0;
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(
                LauncherEnvironment.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected), 0, false)))
            Renderer = Conversions.ToInteger(
                Operators.SubtractObject(LauncherEnvironment.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected),
                    1));
        else
            Renderer = Conversions.ToInteger(Config.Launch.Renderer);
        var MesaLoaderWindowsVersion = "25.3.5";
        var MesaLoaderWindowsTargetFile =
            LauncherPaths.PureAsciiDirectory + @"\mesa-loader-windows\" + MesaLoaderWindowsVersion + @"\Loader.jar";

        if (Renderer != 0)
            DataList.Insert(0,
                "-javaagent:\"" + MesaLoaderWindowsTargetFile + "\"=" +
                (Renderer == 1 ? "llvmpipe" : Renderer == 2 ? "d3d12" : "zink"));

        // 设置代理
        if (Config.Instance.UseProxy[instance.PathIndie] && Config.Network.HttpProxy.Type.Equals(2) &&
            !string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
            try
            {
                var ProxyAddress = new Uri(Conversions.ToString(Config.Network.HttpProxy.CustomAddress));
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyHost={ProxyAddress.AbsoluteUri}");
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyPort={ProxyAddress.Port}");
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "添加代理信息到游戏失败，放弃加入", LauncherLogger.LogLevel.Hint);
            }

        // 添加 Java Wrapper 作为主 Jar
        if (Conversions.ToBoolean(MigrationHelpers.IsUtf8CodePage() && !(bool)Config.Launch.DisableJlw &&
                                  !(bool)LauncherEnvironment.Setup.Get("VersionAdvanceDisableJLW",
                                      ModMinecraft.McInstanceSelected)))
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 9)
                DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            DataList.Add("-Doolloo.jlw.tmpdir=\"" + LauncherPaths.PureAsciiDirectory.TrimEnd('\\') + "\"");
            DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
        }

        // 添加 MainClass
        if (instance.JsonObject["mainClass"] is null) throw new Exception("实例 JSON 中没有 mainClass 项！");

        DataList.Add((string)instance.JsonObject["mainClass"]);

        return DataList.Join(" ");
    }

    private static string McLaunchArgumentsJvmNew(ModMinecraft.McInstance instance)
    {
        var DataList = new List<string>();

        // 获取 Json 中的 DataList
        var currentInstance = instance;
        NextInstance: ;

        if (currentInstance.JsonObject["arguments"] is not null &&
            currentInstance.JsonObject["arguments"]["jvm"] is not null)
            foreach (var SubJson in currentInstance.JsonObject["arguments"]["jvm"])
                if (SubJson.Type == JTokenType.String)
                {
                    // 字符串类型
                    DataList.Add(SubJson.ToString());
                }
                // 非字符串类型
                else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                {
                    // 满足准则
                    if (SubJson["value"].Type == JTokenType.String)
                        DataList.Add(SubJson["value"].ToString());
                    else
                        foreach (var value in SubJson["value"])
                            DataList.Add(value.ToString());
                }

        if (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new ModMinecraft.McInstance(currentInstance.InheritInstanceName);
            goto NextInstance;
        }

        // 内存、Log4j 防御参数等
        SecretLaunchJvmArgs(ref DataList);

        // Authlib-Injector
        if (McLoginLoader.Output.Type == "Auth")
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 6)
                DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT"); // 信任系统根证书（Meloong-Git/#5252）
            var Server = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "");
            try
            {
                var Response = Conversions.ToString(ModNet.NetGetCodeByRequestRetry(Server, Encoding.UTF8));
                DataList.Insert(0,
                    "-javaagent:\"" + LauncherPaths.PureAsciiDirectory + "authlib-injector.jar\"=" + Server +
                    " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
            }
            catch (Exception ex)
            {
                throw new Exception("无法连接到第三方登录服务器（" + (Server ?? null) + "）", ex);
            }
        }

        if (Config.Instance.UseDebugLof4j2Config[instance.PathIndie])
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017)
                DataList.Insert(0, "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractDebugLog4j2Config() + "\"");
            else
                DataList.Insert(0,
                    "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() + "\"");
        }

        // 渲染器
        var Renderer = 0;
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(
                LauncherEnvironment.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected), 0, false)))
            Renderer = Conversions.ToInteger(
                Operators.SubtractObject(LauncherEnvironment.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected),
                    1));
        else
            Renderer = Conversions.ToInteger(Config.Launch.Renderer);
        var MesaLoaderWindowsVersion = "25.3.5";
        var MesaLoaderWindowsTargetFile =
            LauncherPaths.PureAsciiDirectory + @"\mesa-loader-windows\" + MesaLoaderWindowsVersion + @"\Loader.jar";

        if (Renderer != 0)
            DataList.Insert(0,
                "-javaagent:\"" + MesaLoaderWindowsTargetFile + "\"=" +
                (Renderer == 1 ? "llvmpipe" : Renderer == 2 ? "d3d12" : "zink"));

        // 设置代理
        if (Config.Instance.UseProxy[instance.PathIndie] && Config.Network.HttpProxy.Type.Equals(2) &&
            !string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
            try
            {
                var ProxyAddress = new Uri(Conversions.ToString(Config.Network.HttpProxy.CustomAddress));
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyHost={ProxyAddress.AbsoluteUri}");
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyPort={ProxyAddress.Port}");
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "添加代理信息到游戏失败，放弃加入", LauncherLogger.LogLevel.Hint);
            }

        // 添加 RetroWrapper 相关参数
        if (McLaunchNeedsRetroWrapper(instance))
            // https://github.com/NeRdTheNed/RetroWrapper/wiki/RetroWrapper-flags
            DataList.Add("-Dretrowrapper.doUpdateCheck=false");
        // 添加 Java Wrapper 作为主 Jar
        if (Conversions.ToBoolean(MigrationHelpers.IsUtf8CodePage() && !(bool)Config.Launch.DisableJlw &&
                                  !(bool)LauncherEnvironment.Setup.Get("VersionAdvanceDisableJLW",
                                      ModMinecraft.McInstanceSelected)))
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 9)
                DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            DataList.Add("-Doolloo.jlw.tmpdir=\"" + LauncherPaths.PureAsciiDirectory.TrimEnd('\\') + "\"");
            DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
        }


        // 将 "-XXX" 与后面 "XXX" 合并到一起
        // 如果不合并，会导致 Forge 1.17 启动无效，它有两个 --add-exports，进一步导致其中一个在后面被去重
        var DeDuplicateDataList = new List<string>();
        for (int i = 0, loopTo = DataList.Count - 1; i <= loopTo; i++)
        {
            var CurrentEntry = DataList[i];
            if (DataList[i].StartsWithF("-"))
                while (i < DataList.Count - 1)
                {
                    if (DataList[i + 1].StartsWithF("-")) break;

                    i += 1;
                    CurrentEntry += " " + DataList[i];
                }

            DeDuplicateDataList.Add(CurrentEntry.Trim().Replace("McEmu= ", "McEmu="));
        }

        // #3511 的清理
        DeDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M");

        // 去重
        var Result = DeDuplicateDataList.Distinct().ToList().Join(" ");

        // 添加 MainClass
        if (instance.JsonObject["mainClass"] is null) throw new Exception("实例 JSON 中没有 mainClass 项！");

        Result += " " + instance.JsonObject["mainClass"];

        return Result;
    }

    // Game 部分（第二段）
    private static string McLaunchArgumentsGameOld(ModMinecraft.McInstance Version)
    {
        var DataList = new List<string>();

        // 添加 RetroWrapper 相关参数
        if (McLaunchNeedsRetroWrapper(Version)) DataList.Add("--tweakClass com.zero.retrowrapper.RetroTweaker");

        // 本地化 Minecraft 启动信息
        var BasicString = Version.JsonObject["minecraftArguments"].ToString();
        if (!BasicString.Contains("--height"))
            BasicString += " --height ${resolution_height} --width ${resolution_width}";
        DataList.Add(BasicString);

        var Result = DataList.Join(" ");

        // 特别改变 OptiFineTweaker
        if ((Version.Info.HasForge || Version.Info.HasLiteLoader) && Version.Info.HasOptiFine)
        {
            // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            if (Result.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
            {
                LauncherLogger.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + Result);
                Result = Result.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "")
                             .Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") +
                         " --tweakClass optifine.OptiFineForgeTweaker";
            }

            if (Result.Contains("--tweakClass optifine.OptiFineTweaker"))
            {
                LauncherLogger.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + Result);
                Result = Result.Replace(" --tweakClass optifine.OptiFineTweaker", "")
                             .Replace("--tweakClass optifine.OptiFineTweaker ", "") +
                         " --tweakClass optifine.OptiFineForgeTweaker";
                try
                {
                    LauncherFileSystem.WriteFile(Version.PathInstance + Version.Name + ".json",
                        LauncherFileSystem.ReadFile(Version.PathInstance + Version.Name + ".json")
                            .Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log(ex, "替换 OptiFineForge TweakClass 失败");
                }
            }
        }

        return Result;
    }

    private static string McLaunchArgumentsGameNew(ModMinecraft.McInstance instance)
    {
        string McLaunchArgumentsGameNewRet = default;
        var dataList = new List<string>();

        // 获取 Json 中的 DataList
        var currentInstance = instance;
        NextInstance: ;

        if (currentInstance.JsonObject["arguments"] is not null &&
            currentInstance.JsonObject["arguments"]["game"] is not null)
            foreach (var SubJson in currentInstance.JsonObject["arguments"]["game"])
                if (SubJson.Type == JTokenType.String)
                {
                    // 字符串类型
                    dataList.Add(SubJson.ToString());
                }
                // 非字符串类型
                else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                {
                    // 满足准则
                    if (SubJson["value"].Type == JTokenType.String)
                        dataList.Add(SubJson["value"].ToString());
                    else
                        foreach (var value in SubJson["value"])
                            dataList.Add(value.ToString());
                }

        if (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new ModMinecraft.McInstance(currentInstance.InheritInstanceName);
            goto NextInstance;
        }

        // 将 "-XXX" 与后面 "XXX" 合并到一起
        // 如果不进行合并 Impact 会启动无效，它有两个 --tweakclass
        var DeDuplicateDataList = new List<string>();
        for (int i = 0, loopTo = dataList.Count - 1; i <= loopTo; i++)
        {
            var CurrentEntry = dataList[i];
            if (dataList[i].StartsWithF("-"))
                while (i < dataList.Count - 1)
                {
                    if (dataList[i + 1].StartsWithF("-")) break;

                    i += 1;
                    CurrentEntry += " " + dataList[i];
                }

            DeDuplicateDataList.Add(CurrentEntry);
        }

        // 去重
        McLaunchArgumentsGameNewRet = DeDuplicateDataList.Distinct().ToList().Join(" ");

        // 特别改变 OptiFineTweaker
        if ((instance.Info.HasForge || instance.Info.HasLiteLoader) && instance.Info.HasOptiFine)
        {
            // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
            {
                LauncherLogger.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                McLaunchArgumentsGameNewRet =
                    McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "")
                        .Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") +
                    " --tweakClass optifine.OptiFineForgeTweaker";
            }

            if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineTweaker"))
            {
                LauncherLogger.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                McLaunchArgumentsGameNewRet =
                    McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineTweaker", "")
                        .Replace("--tweakClass optifine.OptiFineTweaker ", "") +
                    " --tweakClass optifine.OptiFineForgeTweaker";
                try
                {
                    LauncherFileSystem.WriteFile(instance.PathInstance + instance.Name + ".json",
                        LauncherFileSystem.ReadFile(instance.PathInstance + instance.Name + ".json")
                            .Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                }
                catch (Exception ex)
                {
                    LauncherLogger.Log(ex, "替换 OptiFineForge TweakClass 失败");
                }
            }
        }

        return McLaunchArgumentsGameNewRet;
    }

    // 替换 Arguments
    private static Dictionary<string, string> McLaunchArgumentsReplace(ModMinecraft.McInstance instance,
        ref ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> loader)
    {
        var GameArguments = new Dictionary<string, string>();

        // 基础参数
        GameArguments.Add("${classpath_separator}", ";");
        GameArguments.Add("${natives_directory}", LauncherPaths.ShortenPath(GetNativesFolder()));
        GameArguments.Add("${library_directory}", LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected + "libraries"));
        GameArguments.Add("${libraries_directory}", LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected + "libraries"));
        GameArguments.Add("${launcher_name}", "PCLCE");
        GameArguments.Add("${launcher_version}", LauncherEnvironment.VersionCode.ToString());
        GameArguments.Add("${version_name}", instance.Name);
        var ArgumentInfo =
            Conversions.ToString(LauncherEnvironment.Setup.Get("VersionArgumentInfo", ModMinecraft.McInstanceSelected));
        GameArguments.Add("${version_type}",
            Conversions.ToString(string.IsNullOrEmpty(ArgumentInfo)
                ? Config.Launch.TypeInfo
                : ArgumentInfo));
        GameArguments.Add("${game_directory}",
            LauncherPaths.ShortenPath(Strings.Left(ModMinecraft.McInstanceSelected.PathIndie,
                ModMinecraft.McInstanceSelected.PathIndie.Count() - 1)));
        GameArguments.Add("${assets_root}", LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected + "assets"));
        GameArguments.Add("${user_properties}", "{}");
        GameArguments.Add("${auth_player_name}", McLoginLoader.Output.Name);
        GameArguments.Add("${auth_uuid}", McLoginLoader.Output.Uuid);
        GameArguments.Add("${auth_access_token}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${access_token}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${auth_session}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${user_type}", "msa"); // #1221

        // 窗口尺寸参数
        Size GameSize;
        switch (Config.Launch.GameWindowMode)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 2, false): // 与启动器尺寸一致
            {
                Size Result;
                LauncherDispatcher.RunInUiWait(() => Result = new Size(LauncherWpf.GetPixelSize(ModMain.FrmMain.PanForm.ActualWidth),
                    LauncherWpf.GetPixelSize(ModMain.FrmMain.PanForm.ActualHeight)));
                GameSize = Result;
                GameSize.Height -= 29.5d * LauncherWpf.DPI / 96d; // 标题栏高度
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 3, false): // 自定义
            {
                GameSize = new Size(Math.Max(100, (double)Config.Launch.GameWindowWidth),
                    Math.Max(100, (double)Config.Launch.GameWindowHeight));
                break;
            }

            default:
            {
                GameSize = new Size(854d, 480d);
                break;
            }
        }

        if (ModMinecraft.McInstanceSelected.Info.Drop <= 120 && McLaunchJavaSelected.Installation.MajorVersion <= 8 &&
            McLaunchJavaSelected.Installation.Version.Revision >= 200 &&
            McLaunchJavaSelected.Installation.Version.Revision <= 321 &&
            !ModMinecraft.McInstanceSelected.Info.HasOptiFine && !ModMinecraft.McInstanceSelected.Info.HasForge)
        {
            // 修复 #3463：1.12.2-，JRE 8u200~321 下窗口大小为设置大小的 DPI% 倍
            McLaunchLog($"已应用窗口大小过大修复（{McLaunchJavaSelected.Installation.Version.Revision}）");
            GameSize.Width /= LauncherWpf.DPI / 96d;
            GameSize.Height /= LauncherWpf.DPI / 96d;
        }

        GameArguments.Add("${resolution_width}", Math.Round(GameSize.Width).ToString());
        GameArguments.Add("${resolution_height}", Math.Round(GameSize.Height).ToString());

        // Assets 相关参数
        GameArguments.Add("${game_assets}",
            LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected +
                                @"assets\virtual\legacy")); // 1.5.2 的 pre-1.6 资源索引应与 legacy 合并
        GameArguments.Add("${assets_index_name}", ModMinecraft.McAssetsGetIndexName(instance));

        // 支持库参数
        var LibList = ModMinecraft.McLibListGet(instance, true);
        loader.Output = LibList;
        var CpStrings = new List<string>();
        string OptiFineCp = null;

        // RetroWrapper 释放
        if (McLaunchNeedsRetroWrapper(instance))
        {
            var WrapperPath = ModMinecraft.McFolderSelected + @"libraries\retrowrapper\RetroWrapper.jar";
            try
            {
                LauncherFileSystem.WriteFile(WrapperPath, LauncherPaths.GetResourceStream("Resources/retro-wrapper.jar"));
                CpStrings.Add(WrapperPath);
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "RetroWrapper 释放失败");
            }
        }

        // LWJGL Unsafe Agent 释放
        if (McLaunchUsesLwjglUnsafeAgent(instance))
        {
            string AgentPath = LauncherPaths.PureAsciiDirectory + "lwjgl-unsafe-agent.jar";
            try
            {
                LauncherFileSystem.WriteFile(AgentPath, LauncherPaths.GetResourceStream("Resources/lwjgl-unsafe-agent.jar"));
                CpStrings.Add(AgentPath);
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "LWJGL Unsafe Agent 释放失败");
            }
        }

        foreach (var Library in LibList)
        {
            if (Library.IsNatives)
                continue;
            if (ModMinecraft.McInstanceSelected.Info.HasCleanroom 
                && Library.OriginalName is not null 
                && (Library.OriginalName.Contains("org.lwjgl.lwjgl:lwjgl:2.9.4") 
                    || Library.OriginalName.Contains("net.java.dev.jna:platform:3.4.0")
                    || Library.OriginalName.Contains("com.ibm.icu:icu4j-core-mojang:51.2")))
                continue;
            if (Library.Name is not null && Library.Name == "optifine:OptiFine")
                OptiFineCp = Library.LocalPath;
            else
                CpStrings.Add(Library.LocalPath);
        }

        foreach (var library in Config.Instance.ClasspathHead[instance.PathInstance].Split(";")) // 自定义 Classpath 头部
        {
            if (string.IsNullOrWhiteSpace(library))
                continue;
            CpStrings.Insert(0, library);
        }

        if (OptiFineCp is not null)
            CpStrings.Insert(CpStrings.Count - 2, OptiFineCp); // OptiFine 的总是需要放到倒数第二位
        GameArguments.Add("${classpath}", CpStrings.Select(c => LauncherPaths.ShortenPath(c)).Join(";"));

        return GameArguments;
    }

    #endregion

    #region 解压 Natives

    private static void McLaunchNatives(ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int> Loader)
    {
        // 创建文件夹
        var Target = GetNativesFolder() + @"\";
        Directory.CreateDirectory(Target);

        // 解压文件
        McLaunchLog("正在解压 Natives 文件");
        var ExistFiles = new List<string>();
        foreach (var Native in Loader.Input)
        {
            if (!Native.IsNatives)
                continue;
            ZipArchive Zip;
            try
            {
                Zip = new ZipArchive(new FileStream(Native.LocalPath, FileMode.Open));
            }
            catch (InvalidDataException ex)
            {
                LauncherLogger.Log(ex, "打开 Natives 文件失败（" + Native.LocalPath + "）");
                File.Delete(Native.LocalPath);
                throw new Exception("无法打开 Natives 文件（" + Native.LocalPath + "），该文件可能已损坏，请重新尝试启动游戏");
            }

            foreach (var Entry in Zip.Entries)
            {
                var FileName = Entry.FullName;
                if (FileName.EndsWithF(".dll", true))
                {
                    // 实际解压文件的步骤
                    var FilePath = Target + FileName;
                    ExistFiles.Add(FilePath);
                    var OriginalFile = new FileInfo(FilePath);
                    if (OriginalFile.Exists)
                    {
                        if (OriginalFile.Length == Entry.Length)
                        {
                            if (LauncherLogger.ModeDebug)
                                McLaunchLog("无需解压：" + FilePath);
                            continue;
                        }

                        // 删除原文件
                        try
                        {
                            File.Delete(FilePath);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" + FilePath);
                            McLaunchLog("实际的错误信息：" + ex);
                            break;
                        }
                    }

                    // 解压新文件
                    LauncherFileSystem.WriteFile(FilePath, Entry.Open());
                    McLaunchLog("已解压：" + FilePath);
                }
            }

            if (Zip is not null)
                Zip.Dispose();
        }

        // 删除多余文件
        foreach (var FileName in Directory.GetFiles(Target))
        {
            if (ExistFiles.Contains(FileName))
                continue;
            try
            {
                McLaunchLog("删除：" + FileName);
                File.Delete(FileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤");
                McLaunchLog("实际的错误信息：" + ex);
                return;
            }
        }
    }

    /// <summary>
    ///     获取 Natives 文件夹路径，不以 \ 结尾。
    /// </summary>
    private static string GetNativesFolder()
    {
        var Result = ModMinecraft.McInstanceSelected.PathInstance + ModMinecraft.McInstanceSelected.Name + "-natives";
        if (LauncherEnvironment.IsGBKEncoding || Result.IsASCII())
            return Result;
        Result = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\bin\natives";
        if (Result.IsASCII())
            return Result;
        return LauncherPaths.SystemDrive + @"ProgramData\PCL\natives";
    }

    #endregion

    #region 启动与前后处理

    private static void McLaunchCustom(ModLoader.LoaderTask<int, int> Loader)
    {
        // 获取自定义命令
        var CustomCommandGlobal = Conversions.ToString(Config.Launch.PreLaunchCommand);
        if (!string.IsNullOrEmpty(CustomCommandGlobal))
            CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, true);
        var CustomCommandVersion =
            Conversions.ToString(LauncherEnvironment.Setup.Get("VersionAdvanceRun", ModMinecraft.McInstanceSelected));
        if (!string.IsNullOrEmpty(CustomCommandVersion))
            CustomCommandVersion = ArgumentReplace(CustomCommandVersion, true);

        // 输出 bat
        try
        {
            var CmdString =
                $"{(McLaunchJavaSelected.Installation.MajorVersion > 8 ? "chcp 65001>nul" + "\r\n" : "")}" +
                "@echo off" + "\r\n" + $"title 启动 - {ModMinecraft.McInstanceSelected.Name}" +
                "\r\n" + "echo 游戏正在启动，请稍候。" + "\r\n" +
                $"cd /D \"{LauncherPaths.ShortenPath(ModMinecraft.McInstanceSelected.PathIndie)}\"" + "\r\n" +
                CustomCommandGlobal + "\r\n" + CustomCommandVersion + "\r\n" +
                $"\"{McLaunchJavaSelected.Installation.JavaExePath}\" {McLaunchArgument}" + "\r\n" +
                "echo 游戏已退出。" + "\r\n" + "pause";
            LauncherFileSystem.WriteFile(CurrentLaunchOptions.SaveBatch ?? LauncherPaths.ExecutableDirectory + @"PCL\LatestLaunch.bat",
                ModMinecraft.FilterAccessToken(CmdString, 'F'),
                encoding: McLaunchJavaSelected.Installation.MajorVersion > 8 ? Encoding.UTF8 : Encoding.Default);
            if (CurrentLaunchOptions.SaveBatch is not null)
            {
                McLaunchLog("导出启动脚本完成，强制结束启动过程");
                AbortHint = "导出启动脚本成功！";
                LauncherShell.OpenExplorer(CurrentLaunchOptions.SaveBatch);
                Loader.Parent.Abort();
                return; // 导出脚本完成
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "输出启动脚本失败");
            if (CurrentLaunchOptions.SaveBatch is not null)
                throw; // 直接触发启动失败
        }

        // 执行自定义命令
        if (!string.IsNullOrEmpty(CustomCommandGlobal))
        {
            McLaunchLog("正在执行全局自定义命令：" + CustomCommandGlobal);
            var CustomProcess = new Process();
            try
            {
                CustomProcess.StartInfo.FileName = "cmd.exe";
                CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandGlobal + "\"";
                CustomProcess.StartInfo.WorkingDirectory = LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected);
                CustomProcess.StartInfo.UseShellExecute = false;
                CustomProcess.StartInfo.CreateNoWindow = true;
                CustomProcess.Start();
                if (Conversions.ToBoolean(Config.Launch.PreLaunchCommandWait))
                    while (!CustomProcess.HasExited && !Loader.IsAborted)
                        Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "执行全局自定义命令失败", LauncherLogger.LogLevel.Hint);
            }
            finally
            {
                if (!CustomProcess.HasExited && Loader.IsAborted)
                {
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                    CustomProcess.Kill();
                }
            }
        }

        if (!string.IsNullOrEmpty(CustomCommandVersion))
        {
            McLaunchLog("正在执行实例自定义命令：" + CustomCommandVersion);
            var CustomProcess = new Process();
            try
            {
                CustomProcess.StartInfo.FileName = "cmd.exe";
                CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandVersion + "\"";
                CustomProcess.StartInfo.WorkingDirectory = LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected);
                CustomProcess.StartInfo.UseShellExecute = false;
                CustomProcess.StartInfo.CreateNoWindow = true;
                CustomProcess.Start();
                if (Conversions.ToBoolean(LauncherEnvironment.Setup.Get("VersionAdvanceRunWait", ModMinecraft.McInstanceSelected)))
                    while (!CustomProcess.HasExited && !Loader.IsAborted)
                        Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "执行实例自定义命令失败", LauncherLogger.LogLevel.Hint);
            }
            finally
            {
                if (!CustomProcess.HasExited && Loader.IsAborted)
                {
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                    CustomProcess.Kill();
                }
            }
        }
    }

    /// <summary>
    ///     对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// </summary>
    private static string ArgumentReplace(string text, bool replaceTime, Func<string, string> escapeHandler = null)
    {
        // 预处理
        if (text is null)
            return null;

        string replacer(string s)
        {
            if (s is null)
                return "";
            if (escapeHandler is null)
                return s;
            if (s.Contains(@":\"))
                s = LauncherPaths.ShortenPath(s);
            return escapeHandler(s);
        }

        ;
        // 基础
        text = text.Replace("{pcl_version}", replacer(LauncherEnvironment.VersionBaseName));
        text = text.Replace("{pcl_version_code}", replacer(LauncherEnvironment.VersionCode.ToString()));
        text = text.Replace("{pcl_version_branch}", replacer(LauncherEnvironment.VersionBranchName));
        text = text.Replace("{identify}", replacer(Identify.LauncherId));
        text = text.Replace("{path}", replacer(Basics.CurrentDirectory));
        text = text.Replace("{path_with_name}", replacer(Basics.ExecutablePath));
        text = text.Replace("{path_temp}", replacer(LauncherPaths.TempDirectory));
        // 时间
        if (replaceTime) // 在窗口标题中，时间会被后续动态替换，所以此时不应该替换
        {
            text = text.Replace("{date}", replacer(DateTime.Now.ToString("yyyy'/'M'/'d")));
            text = text.Replace("{time}", replacer(DateTime.Now.ToString("HH':'mm':'ss")));
        }

        // Minecraft
        text = text.Replace("{java}", replacer(McLaunchJavaSelected?.Installation.JavaFolder));
        text = text.Replace("{minecraft}", replacer(ModMinecraft.McFolderSelected));
        if (ModMinecraft.McInstanceSelected?.IsLoaded == true)
        {
            text = text.Replace("{version_path}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
            text = text.Replace("{verpath}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
            text = text.Replace("{version_indie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
            text = text.Replace("{verindie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
            text = text.Replace("{name}", replacer(ModMinecraft.McInstanceSelected.Name));
            if (new[] { "unknown", "old", "pending" }.Contains(
                    ModMinecraft.McInstanceSelected.Info.VanillaName.ToLower()))
                text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Name));
            else
                text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Info.VanillaName));
        }
        else
        {
            text = text.Replace("{version_path}", replacer(null));
            text = text.Replace("{verpath}", replacer(null));
            text = text.Replace("{version_indie}", replacer(null));
            text = text.Replace("{verindie}", replacer(null));
            text = text.Replace("{name}", replacer(null));
            text = text.Replace("{version}", replacer(null));
        }

        // 登录信息
        if (McLoginLoader.State == LoadState.Finished)
        {
            text = text.Replace("{user}", replacer(McLoginLoader.Output.Name));
            text = text.Replace("{uuid}", replacer(McLoginLoader.Output.Uuid?.ToLower()));
            switch (McLoginLoader.Input.Type)
            {
                case McLoginType.Legacy:
                {
                    text = text.Replace("{login}", replacer("离线"));
                    break;
                }
                case McLoginType.Ms:
                {
                    text = text.Replace("{login}", replacer("正版"));
                    break;
                }
                case McLoginType.Auth:
                {
                    text = text.Replace("{login}", replacer("Authlib-Injector"));
                    break;
                }
            }
        }
        else
        {
            text = text.Replace("{user}", replacer(null));
            text = text.Replace("{uuid}", replacer(null));
            text = text.Replace("{login}", replacer(null));
        }

        return text;
    }

    #endregion
}

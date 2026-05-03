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
    #region 内存优化

    private static void McLaunchMemoryOptimize(ModLoader.LoaderTask<int, int> Loader)
    {
        McLaunchLog("内存优化开始");
        var Finished = false;
        LauncherDispatcher.RunInNewThread(() =>
        {
            PageToolsTest.MemoryOptimize(false);
            Finished = true;
        }, "Launch Memory Optimize");
        while (!Finished && !Loader.IsAborted)
        {
            if (Loader.Progress < 0.7d)
                Loader.Progress += 0.007d; // 10s
            else
                Loader.Progress += (0.95d - Loader.Progress) * 0.02d; // 最快 += 0.005

            Thread.Sleep(100);
        }
    }

    #endregion

    #region 开始

    public static bool IsLaunching;
    public static McLaunchOptions CurrentLaunchOptions;

    public partial class McLaunchOptions
    {
        /// <summary>
        ///     额外的启动参数。
        /// </summary>
        public List<string> ExtraArgs = new();

        /// <summary>
        ///     强行指定启动的 MC 实例。
        ///     默认值：Nothing。使用 McInstanceCurrent。
        /// </summary>
        public ModMinecraft.McInstance Instance = null;

        /// <summary>
        ///     是否为 “测试游戏” 按钮启动的游戏。
        ///     如果是，则显示游戏实时日志。
        /// </summary>
        public bool IsTest = false;

        /// <summary>
        ///     将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        ///     默认值：Nothing。不保存。
        /// </summary>
        public string SaveBatch = null;

        /// <summary>
        ///     强制指定在启动后进入的服务器 IP。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string ServerIp = null;

        /// <summary>
        ///     指定在启动之后进入的存档名称。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string WorldName = null;
    }

    /// <summary>
    ///     尝试启动 Minecraft。必须在 UI 线程调用。
    ///     返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    /// </summary>
    public static bool McLaunchStart(McLaunchOptions Options = null)
    {
        IsLaunching = true;
        CurrentLaunchOptions = Options ?? new McLaunchOptions();
        // 预检查
        if (!LauncherDispatcher.RunInUi())
            throw new Exception("McLaunchStart 必须在 UI 线程调用！");
        if (McLaunchLoader.State == LoadState.Loading)
        {
            ModMain.Hint("已有游戏正在启动中！", ModMain.HintType.Critical);
            IsLaunching = false;
            return false;
        }

        // 强制切换需要启动的实例
        if (CurrentLaunchOptions.Instance is not null &&
            ModMinecraft.McInstanceSelected != CurrentLaunchOptions.Instance)
        {
            McLaunchLog("在启动前切换到实例 " + CurrentLaunchOptions.Instance.Name);
            // 检查实例
            CurrentLaunchOptions.Instance.Load();
            if (CurrentLaunchOptions.Instance.State == ModMinecraft.McInstanceState.Error)
            {
                ModMain.Hint("无法启动 Minecraft：" + CurrentLaunchOptions.Instance.Desc, ModMain.HintType.Critical);
                IsLaunching = false;
                return false;
            }

            // 切换实例
            ModMinecraft.McInstanceSelected = CurrentLaunchOptions.Instance;
            States.Game.SelectedInstance = ModMinecraft.McInstanceSelected.Name;
            ModMain.FrmLaunchLeft.RefreshButtonsUI();
            ModMain.FrmLaunchLeft.RefreshPage(false);
        }

        ModMain.FrmMain.AprilGiveup();
        // 禁止进入实例选择页面（否则就可以在启动中切换 McInstanceCurrent 了）
        ModMain.FrmMain.PageStack =
            ModMain.FrmMain.PageStack.Where(p => p.Page != FormMain.PageType.InstanceSelect).ToList();
        // 实际启动加载器
        McLaunchLoader.Start(Options, true);
        return true;
    }


    // 启动状态切换
    public static ModLoader.LoaderTask<McLaunchOptions, object> McLaunchLoader = new("Loader Launch", McLaunchStart)
        { OnStateChanged = a => McLaunchState((dynamic)a) };

    public static ModLoader.LoaderCombo<object> McLaunchLoaderReal;
    public static Process McLaunchProcess;
    public static ModWatcher.Watcher McLaunchWatcher;

    private static void McLaunchState(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
    {
        switch (McLaunchLoader.State)
        {
            case LoadState.Finished:
            case LoadState.Failed:
            case LoadState.Waiting:
            case LoadState.Aborted:
            {
                ModMain.FrmLaunchLeft.PageChangeToLogin();
                break;
            }
            case LoadState.Loading:
            {
                // 在预检测结束后再触发动画
                ModMain.FrmLaunchRight.LabLog.Text = "";
                break;
            }
        }
    }

    /// <summary>
    ///     指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    /// </summary>
    private static string AbortHint;

    // 实际的启动方法
    private static void McLaunchStart(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
    {
        // 开始动画
        LauncherDispatcher.RunInUiWait(ModMain.FrmLaunchLeft.PageChangeToLaunching);
        // 预检测（预检测的错误将直接抛出）
        try
        {
            McLaunchPrecheck();
            McLaunchLog("预检测已通过");
        }
        catch (Exception ex)
        {
            if (!ex.Message.StartsWithF("$$"))
                ModMain.Hint(ex.Message, ModMain.HintType.Critical);
            throw;
        }

        // 正式加载
        try
        {
            // 构造主加载器
            var Loaders = new List<ModLoader.LoaderBase>
            {
                new ModLoader.LoaderTask<int, int>("获取 Java", McLaunchJava) { ProgressWeight = 4d, Block = false },
                McLoginLoader,
                new ModLoader.LoaderCombo<string>("补全文件",
                        ModDownload.DlClientFix(ModMinecraft.McInstanceSelected, false,
                            ModDownload.AssetsIndexExistsBehaviour.DownloadInBackground))
                    { ProgressWeight = 15d, Show = false },
                new ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>>("获取启动参数", McLaunchArgumentMain)
                    { ProgressWeight = 2d },
                new ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int>("解压文件", McLaunchNatives)
                    { ProgressWeight = 2d },
                new ModLoader.LoaderTask<int, int>("预启动处理", _ => McLaunchPrerun()) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, int>("执行自定义命令", McLaunchCustom) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, Process>("启动进程", McLaunchRun) { ProgressWeight = 2d },
                new ModLoader.LoaderTask<Process, int>("等待游戏窗口出现", McLaunchWait) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, int>("结束处理", _ => McLaunchEnd()) { ProgressWeight = 1d }
            }; // .ProgressWeight = 15, .Block = False
            // 内存优化
            switch (LauncherEnvironment.Setup.Get("VersionRamOptimize", ModMinecraft.McInstanceSelected))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全局
                {
                    if (Conversions.ToBoolean(Config.Launch.OptimizeMemory)) // 使用全局设置
                    {
                        ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                        Loaders.Insert(3,
                            new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize)
                                { ProgressWeight = 30d });
                    }

                    break;
                }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 开启
                {
                    ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                    Loaders.Insert(3,
                        new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize) { ProgressWeight = 30d });
                    break;
                }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false): // 关闭
                {
                    break;
                }
            }

            var LaunchLoader = new ModLoader.LoaderCombo<object>("Minecraft 启动", Loaders) { Show = false };
            if (McLoginLoader.State == LoadState.Finished)
                McLoginLoader.State = LoadState.Waiting; // 要求重启登录主加载器，它会自行决定是否启动副加载器
            // 等待加载器执行并更新 UI
            McLaunchLoaderReal = LaunchLoader;
            AbortHint = null;
            LaunchLoader.Start();
            // 任务栏进度条
            ModLoader.LoaderTaskbarAdd(LaunchLoader);
            while (LaunchLoader.State == LoadState.Loading)
            {
                ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
                Thread.Sleep(100);
            }

            ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
            // 成功与失败处理
            switch (LaunchLoader.State)
            {
                case LoadState.Finished:
                {
                    ModMain.Hint(ModMinecraft.McInstanceSelected.Name + " 启动成功！", ModMain.HintType.Finish);
                    break;
                }
                case LoadState.Aborted:
                {
                    if (AbortHint is null)
                        ModMain.Hint(CurrentLaunchOptions?.SaveBatch is null ? "已取消启动！" : "已取消导出启动脚本！");
                    else
                        ModMain.Hint(AbortHint, ModMain.HintType.Finish);

                    break;
                }
                case LoadState.Failed:
                {
                    throw LaunchLoader.Error;
                }

                default:
                {
                    throw new Exception("错误的状态改变：" + LauncherText.GetStringFromEnum(LaunchLoader.State));
                }
            }

            IsLaunching = false;
        }
        catch (Exception ex)
        {
            var CurrentEx = ex;
            NextInner: ;

            if (CurrentEx.Message.StartsWithF("$"))
            {
                // 若有以 $ 开头的错误信息，则以此为准显示提示
                // 若错误信息为 $$，则不提示
                if (!(CurrentEx.Message == "$$"))
                    ModMain.MyMsgBox(CurrentEx.Message.TrimStart('$'),
                        CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
                throw;
            }

            if (CurrentEx.InnerException is not null)
            {
                // 检查下一级错误
                CurrentEx = CurrentEx.InnerException;
                goto NextInner;
            }

            // 没有特殊处理过的错误信息
            McLaunchLog("错误：" + ex);
            LauncherLogger.Log(ex, CurrentLaunchOptions?.SaveBatch is null ? "Minecraft 启动失败" : "导出启动脚本失败",
                LauncherLogger.LogLevel.Msgbox, CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
            throw;
        }
    }

    #endregion

    #region 启动与前后处理

    private static void McLaunchPrerun()
    {
        // 要求 Java 使用高性能显卡
        var javaExePath = McLaunchJavaSelected.Installation.JavawExePath ??
                          McLaunchJavaSelected.Installation.JavaExePath;
        try
        {
            ModMain.SetGPUPreference(javaExePath, Config.Launch.SetGpuPreference);
        }
        catch (Exception ex)
        {
            if (ProcessInterop.IsAdmin() || !Config.Launch.SetGpuPreference)
            {
                LauncherLogger.Log(ex, "直接调整显卡设置失败");
            }
            else
            {
                LauncherLogger.Log(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试");
                try
                {
                    if (ProcessInterop.StartAsAdmin($"--gpu \"{javaExePath}\"").ExitCode ==
                        (int)ProcessReturnValues.TaskDone)
                        McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功");
                    else
                        throw new Exception("调整过程中出现异常");
                }
                catch (Exception exx)
                {
                    LauncherLogger.Log(exx, "调整显卡设置失败，Minecraft 可能会使用默认显卡运行", LauncherLogger.LogLevel.Hint);
                }
            }
        }

        // 更新 launcher_profiles.json
        do
        {
            try
            {
                // 确保可用
                if (!(McLoginLoader.Output.Type == "Microsoft"))
                    break;
                ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.McFolderSelected);
                // 构建需要替换的 Json 对象
                var ReplaceJsonString = @"
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ + McLoginLoader.Output.Name + @"""
                    }
                  }
                }
              },
              ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }";
                var ReplaceJson = (JObject)LauncherSerialization.GetJson(ReplaceJsonString);
                // 更新文件
                var Profiles =
                    (JObject)LauncherSerialization.GetJson(
                        LauncherFileSystem.ReadFile(ModMinecraft.McFolderSelected + "launcher_profiles.json"));
                Profiles.Merge(ReplaceJson);
                LauncherFileSystem.WriteFile(ModMinecraft.McFolderSelected + "launcher_profiles.json", Profiles.ToString(),
                    encoding: Encoding.GetEncoding("GB18030"));
                McLaunchLog("已更新 launcher_profiles.json");
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试");
                try
                {
                    File.Delete(ModMinecraft.McFolderSelected + "launcher_profiles.json");
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.McFolderSelected);
                    // 构建需要替换的 Json 对象
                    var ReplaceJsonString = @"
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ + McLoginLoader.Output.Name + @"""
                            }
                          }
                        }
                      },
                      ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }";
                    var ReplaceJson = (JObject)LauncherSerialization.GetJson(ReplaceJsonString);
                    // 更新文件
                    var Profiles =
                        (JObject)LauncherSerialization.GetJson(
                            LauncherFileSystem.ReadFile(ModMinecraft.McFolderSelected + "launcher_profiles.json"));
                    Profiles.Merge(ReplaceJson);
                    LauncherFileSystem.WriteFile(ModMinecraft.McFolderSelected + "launcher_profiles.json", Profiles.ToString(),
                        encoding: Encoding.GetEncoding("GB18030"));
                    McLaunchLog("已在删除后更新 launcher_profiles.json");
                }
                catch (Exception exx)
                {
                    LauncherLogger.Log(exx, "更新 launcher_profiles.json 失败", LauncherLogger.LogLevel.Feedback);
                }
            }
        } while (false);

        // 更新 options.txt
        var SetupFileAddress = ModMinecraft.McInstanceSelected.PathIndie + "options.txt";

        // 辅助切换游戏语言
        if (Config.Tool.AutoChangeLanguage)
        {
            if (!File.Exists(SetupFileAddress))
            {
                // Yosbr Mod 兼容（#2385）：https://www.curseforge.com/minecraft/mc-mods/yosbr
                var YosbrFileAddress = ModMinecraft.McInstanceSelected.PathIndie + @"config\yosbr\options.txt";
                if (File.Exists(YosbrFileAddress))
                {
                    McLaunchLog("将修改 Yosbr Mod 中的 options.txt");
                    SetupFileAddress = YosbrFileAddress;
                    LauncherSerialization.WriteIni(SetupFileAddress, "lang", "none"); // 忽略默认语言
                }
            }

            try
            {
                // 语言
                // 1.0-     ：没有语言选项
                // 1.1 ~ 5  ：zh_CN 时正常，zh_cn 时崩溃（最后两位字母必须大写，否则将会 NPE 崩溃）
                // 1.6 ~ 10 ：zh_CN 时正常，zh_cn 时自动切换为英文
                // 1.11 ~ 12：zh_cn 时正常，zh_CN 时虽然显示了中文但语言设置会错误地显示选择英文
                // 1.13+    ：zh_cn 时正常，zh_CN 时自动切换为英文
                var CurrentLang = LauncherSerialization.ReadIni(SetupFileAddress, "lang", "none");
                string RequiredLang; // 需要的语言
                var hasExistingSaves = Directory.Exists(ModMinecraft.McInstanceSelected.PathIndie + "saves");
                var shouldUseDefault = CurrentLang == "none" || !hasExistingSaves;

                // 获取 Minecraft 版本信息
                DateTime? mcReleaseTime = ModMinecraft.McInstanceSelected.ReleaseTime;
                var isUnder1dot1 =
                    (bool)((new DateTime(2000, 1, 1) is var arg3 && mcReleaseTime.HasValue
                            ? mcReleaseTime.Value > arg3
                            : (bool?)null) is var arg5 && arg5.HasValue && !arg5.Value ? false :
                        !((new DateTime(2011, 11, 18) is var arg4 && mcReleaseTime.HasValue
                            ? mcReleaseTime.Value <= arg4
                            : (bool?)null) is { } arg6) ? null :
                        arg6 ? arg5 : false); // 1.11 发布日期

                // 对于 1.0 及以下版本，没有语言选项，返回 "none"
                if (isUnder1dot1)
                {
                    RequiredLang = "none";
                }
                else
                {
                    // 根据配置确定默认语言
                    var defaultLang = "zh_cn";
                    RequiredLang = shouldUseDefault ? defaultLang : CurrentLang.ToLower();

                    // 应用版本特定的语言格式规则
                    if (((new DateTime(2012, 1, 12) is var arg7 && mcReleaseTime.HasValue
                                ? mcReleaseTime.Value >= arg7
                                : (bool?)null) is var arg9 && arg9.HasValue && !arg9.Value ? false :
                            !((new DateTime(2016, 6, 8) is var arg8 && mcReleaseTime.HasValue
                                ? mcReleaseTime.Value <= arg8
                                : (bool?)null) is { } arg10) ? null :
                            arg10 ? arg9 : false) == true)
                        // 1.1~1.10：最后两位字母必须大写（zh_CN）
                        RequiredLang = "zh_CN";
                }

                if ((CurrentLang ?? "") == (RequiredLang ?? ""))
                {
                    McLaunchLog($"需要的语言为 {RequiredLang}，当前语言为 {CurrentLang}，无需修改");
                }
                else
                {
                    LauncherSerialization.WriteIni(SetupFileAddress, "lang", "-"); // 触发缓存更改，避免删除后重新下载残留缓存
                    LauncherSerialization.WriteIni(SetupFileAddress, "lang", RequiredLang);
                    McLaunchLog($"已将语言从 {CurrentLang} 修改为 {RequiredLang}");
                }

                // 如果是初次设置，一并修改 forceUnicodeFont，确保中文能正常显示
                if (CurrentLang == "none" || !Directory.Exists(ModMinecraft.McInstanceSelected.PathIndie + "saves"))
                {
                    LauncherSerialization.WriteIni(SetupFileAddress, "forceUnicodeFont", "true");
                    McLaunchLog("已开启 forceUnicodeFont，确保中文字体正常显示");
                }
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "更新 options.txt 失败", LauncherLogger.LogLevel.Hint);
            }
        }

        // 窗口
        switch (Config.Launch.GameWindowMode)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全屏
            {
                LauncherSerialization.WriteIni(SetupFileAddress, "fullscreen", "true");
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 默认
                // 其他
            {
                break;
            }

            default:
            {
                LauncherSerialization.WriteIni(SetupFileAddress, "fullscreen", "false");
                break;
            }
        }
    }

    private static void McLaunchRun(ModLoader.LoaderTask<int, Process> Loader)
    {
        var noJavaw = Conversions.ToBoolean((bool)Config.Launch.NoJavaw &&
                                            McLaunchJavaSelected.Installation.JavawExePath is not null);

        // 启动信息
        var GameProcess = new Process();
        var StartInfo = new ProcessStartInfo(noJavaw
            ? McLaunchJavaSelected.Installation.JavaExePath
            : McLaunchJavaSelected.Installation.JavawExePath);

        // 设置环境变量
        var Paths = new List<string>(StartInfo.EnvironmentVariables["Path"].Split(";"));
        Paths.Add(LauncherPaths.ShortenPath(McLaunchJavaSelected.Installation.JavaFolder));
        StartInfo.EnvironmentVariables["Path"] = Paths.Distinct().ToList().Join(";");
        StartInfo.EnvironmentVariables["appdata"] = LauncherPaths.ShortenPath(ModMinecraft.McFolderSelected);

        // 设置其他参数
        StartInfo.WorkingDirectory = LauncherPaths.ShortenPath(ModMinecraft.McInstanceSelected.PathIndie);
        StartInfo.UseShellExecute = false;
        StartInfo.RedirectStandardOutput = true;
        StartInfo.RedirectStandardError = true;
        StartInfo.CreateNoWindow = noJavaw;
        StartInfo.Arguments = McLaunchArgument;
        GameProcess.StartInfo = StartInfo;

        // 开始进程
        GameProcess.Start();
        McLaunchLog("已启动游戏进程：" + StartInfo.FileName);
        if (Loader.IsAborted)
        {
            McLaunchLog("由于取消启动，已强制结束游戏进程"); // #1631
            GameProcess.Kill();
            return;
        }

        Loader.Output = GameProcess;
        McLaunchProcess = GameProcess;
        // 进程优先级处理
        try
        {
            GameProcess.PriorityBoostEnabled = true;
            switch (Config.Launch.ProcessPriority)
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 高
                {
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false): // 低
                {
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal; // 中
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "设置进程优先级失败", LauncherLogger.LogLevel.Feedback);
        }
    }

    private static void McLaunchWait(ModLoader.LoaderTask<Process, int> Loader)
    {
        // 输出信息
        McLaunchLog("");
        McLaunchLog("~ 基础参数 ~");
        McLaunchLog("PCL 版本：" + LauncherEnvironment.VersionBaseName + " (" + LauncherEnvironment.VersionCode + ")");
        McLaunchLog(
            $"游戏版本：{ModMinecraft.McInstanceSelected.Info.VanillaName}（{ModMinecraft.McInstanceSelected.Info.Vanilla}，Drop {ModMinecraft.McInstanceSelected.Info.Drop}{(ModMinecraft.McInstanceSelected.Info.Reliable ? "" : "，无法完全确定")}）");
        McLaunchLog("资源版本：" + ModMinecraft.McAssetsGetIndexName(ModMinecraft.McInstanceSelected));
        McLaunchLog("实例继承：" + (string.IsNullOrEmpty(ModMinecraft.McInstanceSelected.InheritInstanceName)
            ? "无"
            : ModMinecraft.McInstanceSelected.InheritInstanceName));
        McLaunchLog("分配的内存：" +
                    PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                        !McLaunchJavaSelected.Installation.Is64Bit) + " GB（" +
                    Math.Round(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                        !McLaunchJavaSelected.Installation.Is64Bit) * 1024d) + " MB）");
        McLaunchLog("MC 文件夹：" + ModMinecraft.McFolderSelected);
        McLaunchLog("实例文件夹：" + ModMinecraft.McInstanceSelected.PathInstance);
        McLaunchLog("版本隔离：" + ((ModMinecraft.McInstanceSelected.PathIndie ?? "") ==
                               (ModMinecraft.McInstanceSelected.PathInstance ?? "")));
        McLaunchLog("HMCL 格式：" + ModMinecraft.McInstanceSelected.IsHmclFormatJson);
        McLaunchLog("Java 信息：" + (McLaunchJavaSelected is not null ? McLaunchJavaSelected.ToString : "无可用 Java"));
        // McLaunchLog("环境变量：" & If(McLaunchJavaSelected IsNot Nothing, If(McLaunchJavaSelected.HasEnvironment, "已设置", "未设置"), "未设置"))
        McLaunchLog("Natives 文件夹：" + GetNativesFolder());
        McLaunchLog("");
        McLaunchLog("~ 档案参数 ~");
        McLaunchLog("玩家用户名：" + McLoginLoader.Output.Name);
        McLaunchLog("AccessToken：" + McLoginLoader.Output.AccessToken);
        McLaunchLog("ClientToken：" + McLoginLoader.Output.ClientToken);
        McLaunchLog("UUID：" + McLoginLoader.Output.Uuid);
        McLaunchLog("验证方式：" + McLoginLoader.Output.Type);
        McLaunchLog("");

        // 获取窗口标题
        var WindowTitle = (string?)LauncherEnvironment.Setup.Get("VersionArgumentTitle", ModMinecraft.McInstanceSelected);
        if (string.IsNullOrEmpty(WindowTitle) &&
            !(bool)LauncherEnvironment.Setup.Get("VersionArgumentTitleEmpty", ModMinecraft.McInstanceSelected))
            WindowTitle = Conversions.ToString(Config.Launch.Title);
        WindowTitle = ArgumentReplace(WindowTitle, false);

        // JStack 路径
        var JStackPath = McLaunchJavaSelected.Installation.JavaFolder + @"\jstack.exe";

        // 初始化等待
        var Watcher = new ModWatcher.Watcher(Loader, ModMinecraft.McInstanceSelected, WindowTitle,
            File.Exists(JStackPath) ? JStackPath : "", CurrentLaunchOptions.IsTest);
        McLaunchWatcher = Watcher;

        // 显示实时日志
        if (CurrentLaunchOptions.IsTest)
        {
            if (ModMain.FrmLogLeft is null)
                LauncherDispatcher.RunInUiWait(() => ModMain.FrmLogLeft = new PageLogLeft());
            if (ModMain.FrmLogRight is null)
                LauncherDispatcher.RunInUiWait(() =>
                {
                    ModAnimation.AniControlEnabled += 1;
                    ModMain.FrmLogRight = new PageLogRight();
                    ModAnimation.AniControlEnabled -= 1;
                });
            ModMain.FrmLogLeft.Add(Watcher);
            McLaunchLog("已显示游戏实时日志");
        }

        // 等待
        while (Watcher.State == ModWatcher.Watcher.MinecraftState.Loading)
            Thread.Sleep(100);
        if (Watcher.State == ModWatcher.Watcher.MinecraftState.Crashed) throw new Exception("$$");
    }

    private static void McLaunchEnd()
    {
        McLaunchLog("开始启动结束处理");

        // 暂停或开始音乐播放
        if (Conversions.ToBoolean(Config.Preference.Music.StopInGame))
            LauncherDispatcher.RunInUi(() =>
            {
                if (ModMusic.MusicPause()) LauncherLogger.Log("[Music] 已根据设置，在启动后暂停音乐播放");
            });
        else if (Conversions.ToBoolean(Config.Preference.Music.StartInGame))
            LauncherDispatcher.RunInUi(() =>
            {
                if (ModMusic.MusicResume()) LauncherLogger.Log("[Music] 已根据设置，在启动后开始音乐播放");
            });
        // 暂停视频背景播放
        ModVideoBack.IsGaming = true;
        ModVideoBack.VideoPause();
        // 启动器可见性
        McLaunchLog(
            Conversions.ToString(Operators.ConcatenateObject("启动器可见性：", Config.Launch.LauncherVisibility)));
        switch (Config.Launch.LauncherVisibility)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
            {
                // 直接关闭
                McLaunchLog("已根据设置，在启动后关闭启动器");
                LauncherDispatcher.RunInUi(() => ModMain.FrmMain.EndProgram(false));
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
            case var case2 when Operators.ConditionalCompareObjectEqual(case2, 3, false):
            {
                // 隐藏
                McLaunchLog("已根据设置，在启动后隐藏启动器");
                LauncherDispatcher.RunInUi(() => ModMain.FrmMain.Hidden = true);
                break;
            }
            case var case3 when Operators.ConditionalCompareObjectEqual(case3, 4, false):
            {
                // 最小化
                McLaunchLog("已根据设置，在启动后最小化启动器");
                LauncherDispatcher.RunInUi(() => ModMain.FrmMain.WindowState = WindowState.Minimized);
                break;
            }
            case var case4 when Operators.ConditionalCompareObjectEqual(case4, 5, false):
            {
                break;
            }
            // 啥都不干
        }

        // 启动计数
        States.System.LaunchCount += 1;

        LauncherEnvironment.Setup.Set("VersionLaunchCount",
            Operators.AddObject(LauncherEnvironment.Setup.Get("VersionLaunchCount", ModMinecraft.McInstanceSelected), 1),
            instance: ModMinecraft.McInstanceSelected);
    }

    #endregion
}

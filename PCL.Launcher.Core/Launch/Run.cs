using System.Diagnostics;
using System.Globalization;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Platform;
using PCL.Core.Utils;
using PCL.Core.Utils.Secret;

namespace PCL;

public static partial class GameLaunch
{
    private static void McLaunchPrerun()
    {
        var javaExePath =
            mcLaunchJavaSelected!.Installation.JavawExePath
            ?? mcLaunchJavaSelected.Installation.JavaExePath;
        _SetJavaGpuPreference(javaExePath);
        _UpdateLauncherProfiles();

        var setupFileAddress = Path.Combine(
            InstanceList.McMcInstanceSelected!.PathIndie,
            "options.txt");
        if (Config.Tool.AutoChangeLanguage)
            setupFileAddress = _UpdateMinecraftLanguageOptions(setupFileAddress);
        _ApplyGameWindowMode(setupFileAddress);
    }

    private static void _SetJavaGpuPreference(string javaExePath)
    {
        if (!ApplicationRuntime.Current.Platform.GpuPreference.IsSupported) return;

        try
        {
            LaunchUi.SetGpuPreference(javaExePath, Config.Launch.SetGpuPreference);
        }
        catch (Exception ex)
        {
            var elevation = ApplicationRuntime.GetService<IElevationService>();
            if (elevation is null || elevation.IsElevated || !Config.Launch.SetGpuPreference)
            {
                LauncherCommon.Log(ex, "直接调整显卡设置失败");
                return;
            }

            LauncherCommon.Log(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL CE 再次尝试");
            try
            {
                using var process = elevation.StartElevated(
                    $"--gpu \"{javaExePath}\" {(Config.Launch.SetGpuPreference ? "1" : "0")}");
                if (process is null)
                    throw new InvalidOperationException("无法启动提权进程");

                if (!process.WaitForExit(30_000))
                {
                    process.Kill(true);
                    throw new TimeoutException("提权调整显卡设置超时");
                }

                if (process.ExitCode == (int)LauncherCommon.ProcessReturnValues.TaskDone)
                    McLaunchLog("以管理员权限重启 PCL CE 并调整显卡设置成功");
                else
                    throw new Exception($"提权调整显卡设置失败（退出码 {process.ExitCode}）");
            }
            catch (Exception exx)
            {
                LauncherCommon.Log(
                    exx,
                    Lang.Text("Minecraft.Launch.Error.GpuSet"),
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Minecraft.Launch.Error.GpuSet"));
            }
        }
    }

    private static void _UpdateLauncherProfiles()
    {
        try
        {
            if (mcLoginLoader.output.Type != "Microsoft")
                return;

            GameFolders.McFolderLauncherProfilesJsonCreate(GameFolders.mcFolderSelected!);
            _MergeLauncherProfiles();
            McLaunchLog("已更新 launcher_profiles.json");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试");
            try
            {
                File.Delete($"{GameFolders.mcFolderSelected}launcher_profiles.json");
                GameFolders.McFolderLauncherProfilesJsonCreate(GameFolders.mcFolderSelected!);
                _MergeLauncherProfiles();
                McLaunchLog("已在删除后更新 launcher_profiles.json");
            }
            catch (Exception exx)
            {
                LauncherCommon.Log(
                    exx,
                    "更新 launcher_profiles.json 失败",
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Minecraft.Launch.Error.UpdateProfilesFailed"));
            }
        }
    }

    private static void _MergeLauncherProfiles()
    {
        var replaceJson = _CreateLauncherProfilesJson();
        var profiles = (JsonObject)
            LauncherCommon.GetJson(
                Files.ReadAllTextOrEmpty($"{GameFolders.mcFolderSelected}launcher_profiles.json"));
        profiles.Merge(replaceJson);
        Files.WriteAllText(
            $"{GameFolders.mcFolderSelected}launcher_profiles.json",
            profiles.ToString(),
            Encoding.GetEncoding("GB18030"));
    }

    private static JsonObject _CreateLauncherProfilesJson()
    {
        var userName = mcLoginLoader.output.Name;
        var clientToken = mcLoginLoader.output.ClientToken;
        var replaceJsonString =
            /* language=JSON */
            $$"""
              {
                "authenticationDatabase": {
                  "00000111112222233333444445555566": {
                    "username": "{{userName.Replace("\"", "-")}}",
                    "profiles": {
                      "66666555554444433333222221111100": {
                          "displayName": "{{userName}}"
                      }
                    }
                  }
                },
                "clientToken": "{{clientToken}}",
                "selectedUser": {
                  "account": "00000111112222233333444445555566",
                  "profile": "66666555554444433333222221111100"
                }
              }
              """;
        return (JsonObject)LauncherCommon.GetJson(replaceJsonString);
    }

    private static string _UpdateMinecraftLanguageOptions(string setupFileAddress)
    {
        setupFileAddress = _ResolveOptionsFile(setupFileAddress);

        try
        {
            _ApplyLanguageSettings(setupFileAddress);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "更新 options.txt 失败",
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Minecraft.Launch.Error.UpdateOptionsFailed"));
        }

        return setupFileAddress;
    }

    private static string _ResolveOptionsFile(string setupFileAddress)
    {
        if (File.Exists(setupFileAddress))
            return setupFileAddress;

        var yosbrFileAddress = Path.Combine(
            InstanceList.McMcInstanceSelected!.PathIndie,
            "config",
            "yosbr",
            "options.txt");
        if (!File.Exists(yosbrFileAddress))
            return setupFileAddress;

        McLaunchLog("将修改 Yosbr Mod 中的 options.txt");
        Ini.Write(yosbrFileAddress, "lang", "none");
        return yosbrFileAddress;
    }

    private static void _ApplyLanguageSettings(string setupFileAddress)
    {
        // 1.1 ~ 5 ：zh_CN 时正常，zh_cn 时崩溃（最后两位字母必须大写，否则将会 NPE 崩溃）
        // 1.11 ~ 12：zh_cn 时正常，zh_CN 时虽然显示了中文但语言设置会错误地显示选择英文
        var currentLang = Ini.Read(setupFileAddress, "lang", "none");
        var isLanguageUnconfigured = string.Equals(
            currentLang,
            "none",
            StringComparison.OrdinalIgnoreCase);
        var hasExistingSaves = Directory.Exists(
            Path.Combine(InstanceList.McMcInstanceSelected!.PathIndie, "saves"));
        var shouldUseDefault = isLanguageUnconfigured || !hasExistingSaves;
        var requiredLang = _ResolveMinecraftLanguage(
            currentLang,
            shouldUseDefault,
            InstanceList.McMcInstanceSelected.releaseTime);

        if (currentLang == requiredLang)
        {
            McLaunchLog($"需要的语言为 {requiredLang}，当前语言为 {currentLang}，无需修改");
        }
        else
        {
            Ini.Write(setupFileAddress, "lang", "-");
            Ini.Write(setupFileAddress, "lang", requiredLang);
            McLaunchLog($"已将语言从 {currentLang} 修改为 {requiredLang}");
        }

        if (!shouldUseDefault || !_ShouldEnableForceUnicodeFont())
            return;
        Ini.Write(setupFileAddress, "forceUnicodeFont", "true");
        McLaunchLog("已开启 forceUnicodeFont，确保当前启动器语言字体正常显示");
    }

    private static void _ApplyGameWindowMode(string setupFileAddress)
    {
        switch (Config.Launch.GameWindowMode)
        {
            case GameWindowSizeMode.Fullscreen:
                Ini.Write(setupFileAddress, "fullscreen", "true");
                break;

            case GameWindowSizeMode.Default:
                break;

            case GameWindowSizeMode.Launcher:
            case GameWindowSizeMode.Custom:
            case GameWindowSizeMode.Maximized:
            default:
                Ini.Write(setupFileAddress, "fullscreen", "false");
                break;
        }
    }

    private static string _ResolveMinecraftLanguage(
        string? currentLanguage,
        bool shouldUseLauncherLanguage,
        DateTime? mcReleaseTime)
    {
        if (_IsMinecraftVersionUnder1Dot1(mcReleaseTime))
            return "none";

        // 13w16a ~ 13w23b 只有 en_US.lang，其它语言 NPE 会崩溃
        var vanillaName = InstanceList.McMcInstanceSelected!.Info.VanillaName ?? string.Empty;
        if (McVersionComparer.CompareVersionGe(vanillaName, "13w16a")
            && McVersionComparer.CompareVersionGe("13w23b", vanillaName))
            return "en_US";

        var useLegacyRegionCase = _ShouldUseLegacyMinecraftLanguageCode(mcReleaseTime);
        var languageCode = shouldUseLauncherLanguage
            ? LocalizationService.CurrentLanguage.Code
            : currentLanguage;
        return _NormalizeMinecraftLanguageCode(languageCode, useLegacyRegionCase);
    }

    private static string _NormalizeMinecraftLanguageCode(
        string? languageCode,
        bool useLegacyRegionCase)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(languageCode)
            ? "none"
            : languageCode.Replace('-', '_').Trim();
        if (string.Equals(normalizedCode, "none", StringComparison.OrdinalIgnoreCase))
            return "none";

        var segments = normalizedCode.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return normalizedCode.ToLowerInvariant();

        var language = segments[0].ToLowerInvariant();
        var region = useLegacyRegionCase
            ? segments[1].ToUpperInvariant()
            : segments[1].ToLowerInvariant();
        return $"{language}_{region}";
    }

    private static bool _IsMinecraftVersionUnder1Dot1(DateTime? releaseTime)
    {
        return releaseTime.HasValue
            && releaseTime.Value > new DateTime(2000, 1, 1)
            && releaseTime.Value <= new DateTime(2011, 11, 18);
    }

    private static bool _ShouldUseLegacyMinecraftLanguageCode(DateTime? releaseTime)
    {
        return releaseTime.HasValue
            && releaseTime.Value >= new DateTime(2012, 1, 12)
            && releaseTime.Value <= new DateTime(2016, 6, 8);
    }

    private static bool _ShouldEnableForceUnicodeFont()
    {
        return LocalizationService.CurrentLanguage.FontProfile
            is LocalizationFontProfile.SimplifiedChinese
            or LocalizationFontProfile.TraditionalChinese
            or LocalizationFontProfile.Japanese
            or LocalizationFontProfile.Korean;
    }

    private static void McLaunchCustom(Loading.LoaderTask<int, int> loader)
    {
        var customCommandGlobal = _ReplaceCustomCommand(Config.Launch.PreLaunchCommand);
        var customCommandVersion = _ReplaceCustomCommand(
            Config.Instance.PreLaunchCommand[InstanceList.McMcInstanceSelected!.PathInstance]);

        if (_TryExportLaunchScript(customCommandGlobal, customCommandVersion, loader))
            return;

        if (!string.IsNullOrEmpty(customCommandGlobal))
            _RunCustomCommand(
                customCommandGlobal,
                () => Config.Launch.PreLaunchCommandWait,
                loader,
                $"正在执行全局自定义命令：{customCommandGlobal}");

        if (!string.IsNullOrEmpty(customCommandVersion))
            _RunCustomCommand(
                customCommandVersion,
                () =>
                    Config.Instance.PreLaunchCommandWait[
                        InstanceList.McMcInstanceSelected!.PathInstance
                    ],
                loader,
                $"正在执行实例自定义命令：{customCommandVersion}");
    }

    private static bool _TryExportLaunchScript(
        string customCommandGlobal,
        string customCommandVersion,
        Loading.LoaderTask<int, int> loader)
    {
        try
        {
            var scriptService = ApplicationRuntime.GetService<ILaunchScriptService>()
                ?? throw new InvalidOperationException("启动脚本服务尚未初始化");
            var scriptPath = currentLaunchOptions!.SaveBatch
                ?? Path.Combine(Paths.Data, "LatestLaunch" + scriptService.FileExtension);
            Files.WriteAllText(
                scriptPath,
                LogFilter.FilterAccessToken(
                    _CreateLaunchScript(customCommandGlobal, customCommandVersion),
                    'F'
                ),
                mcLaunchJavaSelected!.Installation.MajorVersion > 8
                    ? Encoding.UTF8
                    : Encoding.Default);
            scriptService.MakeExecutable(scriptPath);
            if (currentLaunchOptions.SaveBatch is null)
                return false;

            McLaunchLog("导出启动脚本完成，强制结束启动过程");
            _abortHint = Lang.Text("Minecraft.Launch.ExportScript.Success");
            LaunchUi.OpenExplorer(currentLaunchOptions.SaveBatch);
            loader.parent.Abort();
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "输出启动脚本失败");
            if (currentLaunchOptions!.SaveBatch is not null)
                throw;
            return false;
        }
    }

    private static string _ReplaceCustomCommand(string command)
    {
        return string.IsNullOrEmpty(command) ? command : ArgumentReplace(command, true) ?? "";
    }

    private static string _CreateLaunchScript(
        string customCommandGlobal,
        string customCommandVersion)
    {
        var instance = InstanceList.McMcInstanceSelected!;
        var javaInstallation = mcLaunchJavaSelected!.Installation;
        var service = ApplicationRuntime.GetService<ILaunchScriptService>()
            ?? throw new InvalidOperationException("启动脚本服务尚未初始化");
        return service.CreateScript(
            new LaunchScriptOptions(
                LauncherCommon.ShortenPath(instance.PathIndie),
                javaInstallation.JavaExePath,
                _mcLaunchArgument,
                customCommandGlobal,
                customCommandVersion,
                javaInstallation.MajorVersion,
                Lang.Text("Minecraft.Launch.Script.Title", instance.Name),
                Lang.Text("Minecraft.Launch.Script.Starting"),
                Lang.Text("Minecraft.Launch.Script.Exited")));
    }

    private static void _RunCustomCommand(
        string command,
        Func<bool> waitForExit,
        Loading.LoaderTask<int, int> loader,
        string logMessage)
    {
        McLaunchLog(logMessage);
        var customProcess = new Process();
        try
        {
            var shell = ApplicationRuntime.GetService<IShellCommandService>()
                ?? throw new InvalidOperationException("Shell 命令服务尚未初始化");
            customProcess.StartInfo = shell.CreateCommand(
                command,
                LauncherCommon.ShortenPath(GameFolders.mcFolderSelected!));
            customProcess.Start();

            if (!waitForExit()) return;

            while (!customProcess.HasExited && !loader.IsAborted)
                Thread.Sleep(10);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.CustomCommand"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Minecraft.Launch.Error.CustomCommand"));
        }
        finally
        {
            if (!customProcess.HasExited && loader.IsAborted)
            {
                McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程");
                customProcess.Kill();
            }
        }
    }

    private static void McLaunchRun(Loading.LoaderTask<int, Process> loader)
    {
        var noJavaw =
            Config.Launch.NoJavaw && mcLaunchJavaSelected!.Installation.JavawExePath is not null;

        var gameProcess = new Process();
        var startInfo = _CreateGameProcessStartInfo(noJavaw);
        gameProcess.StartInfo = startInfo;

        gameProcess.Start();
        McLaunchLog($"已启动游戏进程：{startInfo.FileName}");
        if (loader.IsAborted)
        {
            McLaunchLog("由于取消启动，已强制结束游戏进程");
            gameProcess.Kill();
            return;
        }

        loader.output = gameProcess;
        mcLaunchProcess = gameProcess;
        _ApplyProcessPriority(gameProcess);
    }

    private static ProcessStartInfo _CreateGameProcessStartInfo(bool noJavaw)
    {
        var installation = mcLaunchJavaSelected!.Installation;
        var startInfo = new ProcessStartInfo(
            (noJavaw ? installation.JavaExePath : installation.JavawExePath)
            ?? installation.JavaExePath);

        _PrependJavaFolderToPathVariable(startInfo, installation.JavaFolder);
        _ApplyGpuPreferenceEnvironment(startInfo, Config.Launch.SetGpuPreference);
        _ApplyRendererEnvironment(startInfo);
        startInfo.EnvironmentVariables["appdata"] = LauncherCommon.ShortenPath(
            GameFolders.mcFolderSelected!);

        startInfo.WorkingDirectory =
            LauncherCommon.ShortenPath(InstanceList.McMcInstanceSelected!.PathIndie);
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = noJavaw;
        startInfo.Arguments = _mcLaunchArgument;
        return startInfo;
    }

    /// <summary>
    /// 将 Java 目录添加到游戏进程的 PATH 环境变量开头。 Windows 使用 "Path" 与 ";"，其余平台使用 "PATH" 与
    /// Path.PathSeparator。
    /// </summary>
    private static void _PrependJavaFolderToPathVariable(
        ProcessStartInfo startInfo,
        string javaFolder)
    {
        var service = ApplicationRuntime.GetService<IProcessEnvironmentService>()
            ?? throw new InvalidOperationException("进程环境服务尚未初始化");
        service.PrependPathVariable(startInfo, LauncherCommon.ShortenPath(javaFolder));
    }

    /// <summary>通过平台服务准备独立显卡相关的启动环境。</summary>
    private static void _ApplyGpuPreferenceEnvironment(ProcessStartInfo startInfo, bool enabled)
    {
        var environment = ApplicationRuntime.Current.Platform.LaunchEnvironment;
        if (!environment.IsSupported) return;
        environment.ApplyGpuPreference(startInfo, enabled, McLaunchLog);
    }

    /// <summary>通过平台服务应用渲染器选择。</summary>
    private static void _ApplyRendererEnvironment(ProcessStartInfo startInfo)
    {
        var environment = ApplicationRuntime.Current.Platform.LaunchEnvironment;
        if (!environment.IsSupported) return;

        var instanceRenderer = Config.Instance.Renderer[
            InstanceList.McMcInstanceSelected!.PathInstance];
        var renderer = instanceRenderer != 0 ? instanceRenderer - 1 : Config.Launch.Renderer;
        environment.ApplyRenderer(startInfo, renderer, McLaunchLog);
    }

    private static void _ApplyProcessPriority(Process gameProcess)
    {
        try
        {
            gameProcess.PriorityBoostEnabled = true;
            gameProcess.PriorityClass = Config.Launch.ProcessPriority switch
            {
                GameProcessPriority.RealTime => ProcessPriorityClass.RealTime,
                GameProcessPriority.High => ProcessPriorityClass.High,
                GameProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                GameProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                _ => gameProcess.PriorityClass
            };
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Error.PrioritySet"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Error.PrioritySet"));
        }
    }

    private static void McLaunchWait(Loading.LoaderTask<Process, int> loader)
    {
        _LogLaunchBaseParameters();
        _LogLoginParameters();

        var windowTitle = Config.Instance.Title[InstanceList.McMcInstanceSelected!.PathInstance];
        if (string.IsNullOrEmpty(windowTitle)
            && !Config.Instance.UseGlobalTitle[InstanceList.McMcInstanceSelected.PathInstance])
            windowTitle = Config.Launch.Title;
        windowTitle = ArgumentReplace(windowTitle, false) ?? "";

        var jStackPath = Path.Combine(
            mcLaunchJavaSelected!.Installation.JavaFolder,
            ApplicationRuntime.Current.Platform.Java.JstackExecutableName);

        var watcher = new GameWatcher.Watcher(
            loader,
            InstanceList.McMcInstanceSelected,
            windowTitle,
            File.Exists(jStackPath) ? jStackPath : "",
            currentLaunchOptions!.IsTest);
        mcLaunchWatcher = watcher;

        if (currentLaunchOptions.IsTest)
        {
            LaunchUi.AttachLogPage(watcher);
            McLaunchLog("已显示游戏实时日志");
        }

        while (watcher.State == GameWatcher.Watcher.MinecraftState.Loading)
            Thread.Sleep(100);
        if (watcher.State == GameWatcher.Watcher.MinecraftState.Crashed)
            throw new Exception("$$");
    }

    private static void _LogLaunchBaseParameters()
    {
        var instance = InstanceList.McMcInstanceSelected!;
        McLaunchLog("");
        McLaunchLog("~ 基础参数 ~");
        McLaunchLog($"PCL CE 版本：{LauncherCommon.VersionBaseName} ({LauncherCommon.VersionCode})");
        McLaunchLog(
            $"游戏版本：{instance.Info.VanillaName}（{instance.Info.vanilla}，"
            + $"Drop {instance.Info.Drop}{(instance.Info.Reliable ? "" : "，无法完全确定")}）");
        McLaunchLog($"资源版本：{GameAssets.McAssetsGetIndexName(instance)}");
        McLaunchLog(
            $"实例继承：{(string.IsNullOrEmpty(instance.InheritInstanceName)
                ? "无"
                : instance.InheritInstanceName)}");
        var launchRamGb = MemorySettings.GetRam(
            instance,
            !mcLaunchJavaSelected!.Installation.Is64Bit);
        McLaunchLog(
            $"分配的内存：{launchRamGb.ToString("N1", CultureInfo.InvariantCulture)} GiB（"
            + $"{Math.Round(launchRamGb * 1024d).ToString("N0", CultureInfo.InvariantCulture)} MiB）");
        McLaunchLog($"MC 文件夹：{GameFolders.mcFolderSelected}");
        McLaunchLog($"实例文件夹：{instance.PathInstance}");
        McLaunchLog($"版本隔离：{(instance.PathIndie ?? "") == (instance.PathInstance ?? "")}");
        McLaunchLog($"HMCL 格式：{instance.IsHmclFormatJson}");
        McLaunchLog($"Java 信息：{mcLaunchJavaSelected.Installation}");
        McLaunchLog($"Natives 文件夹：{GetNativesFolder()}");
        McLaunchLog("");
    }

    private static void _LogLoginParameters()
    {
        McLaunchLog("~ 档案参数 ~");
        McLaunchLog($"玩家用户名：{mcLoginLoader.output.Name}");
        McLaunchLog($"AccessToken：{mcLoginLoader.output.AccessToken}");
        McLaunchLog($"ClientToken：{mcLoginLoader.output.ClientToken}");
        McLaunchLog($"UUID：{mcLoginLoader.output.Uuid}");
        McLaunchLog($"验证方式：{mcLoginLoader.output.Type}");
        McLaunchLog("");
    }

    private static void McLaunchEnd()
    {
        McLaunchLog("开始启动结束处理");
        _ApplyMusicState();
        McLaunchLog($"启动器可见性：{Config.Launch.LauncherVisibility}");
        _ApplyLauncherVisibility();

        States.System.LaunchCount += 1;
        _UpdateInstanceLaunchCount();
    }

    private static void _ApplyMusicState()
    {
        if (Config.Preference.Music.StopInGame)
            LauncherCommon.RunInUi(() =>
            {
                MediaService.Current?.PauseMusic();
                LauncherCommon.Log("[Music] 已根据设置，在启动后暂停音乐播放");
            });
        else if (Config.Preference.Music.StartInGame)
            LauncherCommon.RunInUi(() =>
            {
                MediaService.Current?.ResumeMusic();
                LauncherCommon.Log("[Music] 已根据设置，在启动后开始音乐播放");
            });
    }

    private static void _ApplyLauncherVisibility()
    {
        switch (Config.Launch.LauncherVisibility)
        {
            case LauncherVisibility.ExitImmediately:
                McLaunchLog("已根据设置，在启动后关闭启动器");
                LaunchUi.SetMainWindowState(LaunchWindowState.Exit);
                break;

            case LauncherVisibility.HideAndExit:
            case LauncherVisibility.HideAndReopen:
                McLaunchLog("已根据设置，在启动后隐藏启动器");
                LaunchUi.SetMainWindowState(LaunchWindowState.Hide);
                break;

            case LauncherVisibility.MinimizeAndReopen:
                McLaunchLog("已根据设置，在启动后最小化启动器");
                LaunchUi.SetMainWindowState(LaunchWindowState.Minimize);
                break;

            case LauncherVisibility.DoNothing:
            case LauncherVisibility.ObsoleteCaseDoNotUse:
                break;
        }
    }

    private static void _UpdateInstanceLaunchCount()
    {
        var instancePath = InstanceList.McMcInstanceSelected!.PathInstance;
        States.Instance.LaunchCount[instancePath] += 1;
    }

    /// <summary>对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。</summary>
    private static string? ArgumentReplace(
        string? text,
        bool replaceTime,
        Func<string, string>? escapeHandler = null)
    {
        if (text is null)
            return null;

        text = _ReplaceLauncherTokens(text, replaceTime, escapeHandler);
        text = _ReplaceInstanceTokens(text, escapeHandler);
        text = _ReplaceAccountTokens(text, escapeHandler);
        return text;
    }

    private static string _ReplaceLauncherTokens(
        string text,
        bool replaceTime,
        Func<string, string>? escapeHandler)
    {
        text = text.Replace(
            "{pcl_version}",
            _ReplaceToken(LauncherCommon.VersionBaseName, escapeHandler));
        text = text.Replace(
            "{pcl_version_code}",
            _ReplaceToken(LauncherCommon.VersionCode.ToString(), escapeHandler));
        text = text.Replace(
            "{pcl_version_branch}",
            _ReplaceToken(LauncherCommon.VersionBranchName, escapeHandler));
        text = text.Replace(
            "{identify}",
            _ReplaceToken(Identify.LauncherId, escapeHandler));
        text = text.Replace(
            "{path}",
            _ReplaceToken(Basics.CurrentDirectory, escapeHandler));
        text = text.Replace(
            "{path_with_name}",
            _ReplaceToken(Basics.ExecutablePath, escapeHandler));
        text = text.Replace(
            "{path_temp}",
            _ReplaceToken(LauncherPaths.Temp, escapeHandler));
        if (!replaceTime)
            return text;

        text = text.Replace(
            "{date}",
            _ReplaceToken(Lang.Date(DateTime.Now, "d"), escapeHandler));
        text = text.Replace(
            "{time}",
            _ReplaceToken(Lang.Date(DateTime.Now, "T"), escapeHandler));
        return text;
    }

    private static string _ReplaceInstanceTokens(string text, Func<string, string>? escapeHandler)
    {
        text = text.Replace(
            "{java}",
            _ReplaceToken(mcLaunchJavaSelected?.Installation.JavaFolder, escapeHandler));
        text = text.Replace(
            "{minecraft}",
            _ReplaceToken(GameFolders.mcFolderSelected, escapeHandler));
        var instance = InstanceList.McMcInstanceSelected;
        if (instance?.IsLoaded == true)
        {
            text = text.Replace(
                "{version_path}",
                _ReplaceToken(instance.PathInstance, escapeHandler));
            text = text.Replace(
                "{verpath}",
                _ReplaceToken(instance.PathInstance, escapeHandler));
            text = text.Replace(
                "{version_indie}",
                _ReplaceToken(instance.PathIndie, escapeHandler));
            text = text.Replace(
                "{verindie}",
                _ReplaceToken(instance.PathIndie, escapeHandler));
            text = text.Replace(
                "{name}",
                _ReplaceToken(instance.Name, escapeHandler));

            var vanillaName = instance.Info.VanillaName;
            text = text.Replace(
                "{version}",
                new[]
                {
                    "unknown",
                    "old",
                    "pending"
                }.Contains(vanillaName.ToLower())
                    ? _ReplaceToken(instance.Name, escapeHandler)
                    : _ReplaceToken(vanillaName, escapeHandler));
            return text;
        }

        text = text.Replace("{version_path}", _ReplaceToken(null, escapeHandler));
        text = text.Replace("{verpath}", _ReplaceToken(null, escapeHandler));
        text = text.Replace("{version_indie}", _ReplaceToken(null, escapeHandler));
        text = text.Replace("{verindie}", _ReplaceToken(null, escapeHandler));
        text = text.Replace("{name}", _ReplaceToken(null, escapeHandler));
        text = text.Replace("{version}", _ReplaceToken(null, escapeHandler));
        return text;
    }

    private static string _ReplaceAccountTokens(string text, Func<string, string>? escapeHandler)
    {
        if (mcLoginLoader.State != LoadState.Finished)
        {
            text = text.Replace("{user}", _ReplaceToken(null, escapeHandler));
            text = text.Replace("{uuid}", _ReplaceToken(null, escapeHandler));
            text = text.Replace("{login}", _ReplaceToken(null, escapeHandler));
            return text;
        }

        text = text.Replace(
            "{user}",
            _ReplaceToken(mcLoginLoader.output.Name, escapeHandler));
        text = text.Replace(
            "{uuid}",
            _ReplaceToken(mcLoginLoader.output.Uuid?.ToLower(), escapeHandler));
        return mcLoginLoader.input!.LoginType switch
        {
            McLoginType.Legacy => text.Replace(
                "{login}",
                _ReplaceToken("离线", escapeHandler)),

            McLoginType.Ms => text.Replace(
                "{login}",
                _ReplaceToken("正版", escapeHandler)),

            McLoginType.Auth => text.Replace(
                "{login}",
                _ReplaceToken("Authlib-Injector", escapeHandler)
            ),

            _ => text
        };
    }

    private static string _ReplaceToken(string? value, Func<string, string>? escapeHandler)
    {
        if (value is null)
            return "";
        if (escapeHandler is null)
            return value;
        if (value.Contains(@":\"))
            value = LauncherCommon.ShortenPath(value);
        return escapeHandler(value);
    }
}
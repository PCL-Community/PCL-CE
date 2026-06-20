using System.IO;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;
using PCL.Core.Utils;

namespace PCL;

public static class ModJava
{
    public static int javaListCacheVersion = 7;

    /// <summary>
    ///     防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    /// </summary>
    public static object javaLock = new();

    /// <summary>
    ///     目前所有可用的 Java。
    /// </summary>
    public static JavaManager Javas => JavaService.JavaManager;

    /// <summary>
    ///     根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ///     最小与最大版本在与输入相同时也会通过。
    ///     必须在工作线程调用，且必须包括 SyncLock JavaLock。
    /// </summary>
    public static JavaEntry JavaSelect(string cancelException, Version minVersion = null, Version maxVersion = null,
        McInstance relatedInstance = null)
    {
        ModBase.Log(
            $"[Java] 要求选择合适 Java，要求最低版本 {(minVersion is not null ? minVersion.ToString() : "未指定")}，要求选择的最高版本 {(maxVersion is not null ? maxVersion.ToString() : "未指定")}，关联实例 {(relatedInstance is not null ? relatedInstance.Name : "未指定")}");

        // 版本范围验证函数（安全处理 null 边界）
        bool IsVersionSuitable(Version ver)
        {
            return (minVersion is null || ver >= minVersion) && (maxVersion is null || ver <= maxVersion);
        }

        // ===== 优先级 1：实例专属 Java 偏好 =====
        if (relatedInstance is not null && relatedInstance.PathInstance is not null)
        {
            var rawPreference = Config.Instance.SelectedJava[relatedInstance.PathInstance];

            if (!string.IsNullOrWhiteSpace(rawPreference))
            {
                var preference = GetInstanceJavaPreference(relatedInstance);

                // 处理解析成功的偏好
                if (preference is not null)
                    switch (true)
                    {
                        case object _ when preference is ExistingJava: // "exist"
                        {
                            var existPref = (ExistingJava)preference;
                            var candidate = Javas.AddOrGet(existPref.JavaExePath);

                            if (candidate is not null && candidate.IsEnabled)
                            {
                                if (!IsVersionSuitable(candidate.Installation.Version))
                                    ModMain.Hint(
                                        Lang.Text("Minecraft.Java.InstanceOutsideRange",
                                            candidate.Installation.Version,
                                            minVersion?.ToString() ?? Lang.Text("Minecraft.Java.NoLowerLimit"),
                                            maxVersion?.ToString() ?? Lang.Text("Minecraft.Java.NoUpperLimit")));
                                ModBase.Log($"[Java] 返回实例 '{relatedInstance.Name}' 指定的 Java: {candidate}");
                                return candidate;
                            }

                            ModBase.Log($"[Java] 警告：实例指定的 Java 路径无效或不可用: {existPref.JavaExePath}");

                            break;
                        }

                        case object _ when preference is UseRelativePath: // "relative"
                        {
                            var relPref = (UseRelativePath)preference;
                            var absPath =
                                Path.GetFullPath(Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath));

                            if (Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory))
                            {
                                var candidate = Javas.Get(absPath);
                                if (candidate is not null && candidate.IsEnabled)
                                {
                                    if (!IsVersionSuitable(candidate.Installation.Version))
                                        ModMain.Hint(
                                            Lang.Text("Minecraft.Java.RelativeOutsideRange",
                                                candidate.Installation.Version),
                                            ModMain.HintType.Critical);
                                    ModBase.Log(
                                        $"[Java] 返回实例 '{relatedInstance.Name}' 相对路径指定的 Java ({relPref.RelativePath}): {candidate}");
                                    return candidate;
                                }
                            }
                            else
                            {
                                ModBase.Log($"[Java] 警告：实例相对路径指定的 Java 无效: {absPath}");
                            }

                            break;
                        }

                        case object _ when preference is UseGlobalPreference: // "global"
                        {
                            // 不返回，继续到全局设置检查
                            ModBase.Log($"[Java] 实例 '{relatedInstance.Name}' 配置为使用全局 Java 设置，继续检查全局配置");
                            break;
                        }

                        default:
                        {
                            ModBase.Log($"[Java] 警告：未知的 Java 偏好类型 '{preference}'，跳过处理");
                            break;
                        }
                    }
                else
                    ModBase.Log($"[Java] 实例 '{relatedInstance.Name}' 未指定 Java 偏好（空值），使用自动选择策略");
            }
            else
            {
                ModBase.Log($"[Java] 实例 '{relatedInstance.Name}' 无 Java 偏好配置，使用自动选择策略");
            }
        }

        // ===== 优先级 2：全局指定的 Java =====
        var globalJavaPath = Config.Launch.SelectedJava;
        if (!string.IsNullOrWhiteSpace(globalJavaPath))
        {
            globalJavaPath = globalJavaPath.Trim();
            var candidate = Javas.AddOrGet(globalJavaPath);

            if (candidate is not null && candidate.IsEnabled)
            {
                if (!IsVersionSuitable(candidate.Installation.Version))
                    ModMain.Hint(Lang.Text("Minecraft.Java.GlobalOutsideRange",
                        candidate.Installation.Version));
                ModBase.Log($"[Java] 返回全局指定的 Java: {candidate}");
                return candidate;
            }

            ModBase.Log($"[Java] 警告：全局指定的 Java 路径无效或不可用: {globalJavaPath}");
        }
        else
        {
            ModBase.Log("[Java] 无全局 Java 配置，使用自动选择策略");
        }

        // ===== 优先级 3：自动搜索合适版本 =====
        ModBase.Log("[Java] 开始自动搜索符合版本要求的 Java 运行时");
        Javas.CheckAllAvailability();

        var reqMin = minVersion ?? new Version(1, 0, 0);
        var reqMax = maxVersion ?? new Version(999, 999, 999);

        var ret = LauncherJavaApplicationAdapter.SelectBestJava(Javas.GetSortedJavaList(), reqMin, reqMax);

        if (ret is null)
        {
            ModBase.Log("[Java] 未找到符合版本要求的 Java，触发全盘重新扫描");
            Javas.ScanJavaAsync().GetAwaiter().GetResult();
            ret = LauncherJavaApplicationAdapter.SelectBestJava(Javas.GetSortedJavaList(), reqMin, reqMax);
        }

        if (ret is not null)
            ModBase.Log($"[Java] 返回自动选择的 Java: {ret}");
        else
            ModBase.Log("[Java] 最终未能确定可用的 Java 运行时");

        return ret;
    }

    public static JavaPreference GetInstanceJavaPreference(McInstance instance) =>
        LauncherJavaApplicationAdapter.ParseInstanceJavaPreference(
            Config.Instance.SelectedJava[instance.PathInstance],
            Basics.ExecutableDirectory);

    /// <summary>
    ///     是否强制指定了 64 位 Java。如果没有强制指定，返回是否安装了 64 位 Java。
    /// </summary>
    public static bool IsGameSet64BitJava(McInstance relatedVersion = null)
    {
        try
        {
            // 检查强制指定
            var userSetup = Config.Launch.SelectedJava;
            if (userSetup.StartsWith("{")) // 旧版本 Json 格式
            {
                var js = ModBase.GetJson(userSetup);
                userSetup = $"{js["Path"]}java.exe";
                Config.Launch.SelectedJava = userSetup;
            }

            if (relatedVersion is not null)
            {
                var instancePreference = GetInstanceJavaPreference(relatedVersion);
                switch (true)
                {
                    case object _ when instancePreference is AutoSelect:
                    {
                        return Javas.Existing64BitJava();
                    }
                    case object _ when instancePreference is ExistingJava:
                    {
                        var m = (ExistingJava)instancePreference;
                        var java = Javas.AddOrGet(m.JavaExePath);
                        return java is not null && java.Installation.Is64Bit;
                    }
                    case object _ when instancePreference is UseRelativePath:
                    {
                        var m = (UseRelativePath)instancePreference;
                        var javaExePath = Path.GetFullPath(m.RelativePath);
                        if (Files.IsPathWithinDirectory(javaExePath, Basics.ExecutableDirectory))
                        {
                            var java = Javas.Get(javaExePath);
                            return java is not null && java.Installation.Is64Bit;
                        }

                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(userSetup) && !File.Exists(userSetup))
            {
                Config.Launch.SelectedJava = "";
                userSetup = string.Empty;
            }

            if (string.IsNullOrEmpty(userSetup)) return Javas.Existing64BitJava();
            var j = Javas.AddOrGet(userSetup);
            return j is not null && j.Installation.Is64Bit;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检查 Java 类别时出错", ModBase.LogLevel.Feedback);
            if (relatedVersion is not null)
                Config.Instance.SelectedJava[relatedVersion.PathInstance] = "使用全局设置";
            Config.Launch.SelectedJava = "";
        }

        return true;
    }

    #region 下载

    /// <summary>
    ///     提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    /// </summary>
    public static bool JavaDownloadConfirm(string versionDescription, bool forcedManualDownload = false)
    {
        if (forcedManualDownload)
        {
            ModMain.MyMsgBox(
                Lang.Text("Minecraft.Java.ManualMissing.Message", versionDescription),
                Lang.Text("Minecraft.Launch.Java.NotFound.Title"));
            return false;
        }

        return ModMain.MyMsgBox(
            Lang.Text("Minecraft.Java.AutoDownload.Message", versionDescription),
            Lang.Text("Minecraft.Java.AutoDownload.Title"),
            Lang.Text("Minecraft.Java.AutoDownload.Confirm"), Lang.Text("Common.Action.Cancel")) == 1;
    }

    /// <summary>
    ///     获取下载 Java 的加载器。需要开启 isForceRestart 以正常刷新 Java 列表。
    /// </summary>
    public static ModLoader.LoaderCombo<string> GetJavaDownloadLoader()
    {
        var javaDownloadLoader = new LoaderDownload(Lang.Text("Minecraft.Java.Download.FileTask"), new List<DownloadFile>())
            { ProgressWeight = 10d };
        var loader = new ModLoader.LoaderCombo<string>(Lang.Text("Minecraft.Java.Download.Task"),
            new ModLoader.LoaderBase[]
            {
                new ModLoader.LoaderTask<string, List<DownloadFile>>(Lang.Text("Minecraft.Java.Download.InfoTask"), JavaFileList)
                    { ProgressWeight = 2d },
                javaDownloadLoader
            });
        javaDownloadLoader.OnStateChangedThread += (raw, newState, oldState) =>
        {
            if ((newState == ModBase.LoadState.Failed || newState == ModBase.LoadState.Aborted) &&
                lastJavaBaseDir is not null)
            {
                ModBase.Log($"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{lastJavaBaseDir}", ModBase.LogLevel.Debug);
                ModBase.DeleteDirectory(lastJavaBaseDir);
            }
            else if (newState == ModBase.LoadState.Finished)
            {
                Javas.ScanJavaAsync().GetAwaiter().GetResult();
                lastJavaBaseDir = null;
            }
        };
        javaDownloadLoader.hasOnStateChangedThread = true;
        return loader;
    }

    private static string lastJavaBaseDir; // 用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java

    private static void JavaFileList(ModLoader.LoaderTask<string, List<DownloadFile>> loader)
    {
        ModBase.Log("[Java] 开始获取 Java 下载信息");
        var plan = LauncherJavaApplicationAdapter.CreateJavaRuntimeDownloadPlan(
            loader.input);
        ModLaunch.McLaunchLog($"准备下载 Java {plan.VersionName}（{plan.ComponentName}）：{plan.ManifestUrl}");
        lastJavaBaseDir = plan.TargetDirectory;
        var results = new List<DownloadFile>(plan.Files.Count);
        foreach (var file in plan.Files)
        {
            var checker = new ModBase.FileChecker(actualSize: file.Size, hash: file.Sha1);
            if (checker.Check(file.TargetPath) is null)
                continue; // 跳过已存在的文件
            results.Add(new DownloadFile(
                ModDownload.DlSourceOrder(new[] { file.Url },
                    new[] { file.Url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com") }),
                file.TargetPath,
                checker));
        }

        loader.output = results;
        ModBase.Log($"[Java] 需要下载 {results.Count} 个文件，目标文件夹：{lastJavaBaseDir}");
    }

    #endregion
}

using System.IO;
using System.Text.Json;
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
                                    HintService.Hint(_GetJavaRangeWarning(
                                        "Minecraft.Launch.Java.Compatibility.InstanceSelectedOutOfRange",
                                        candidate.Installation.Version,
                                        minVersion,
                                        maxVersion));
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
                                        HintService.Hint(_GetJavaRangeWarning(
                                                "Minecraft.Launch.Java.Compatibility.RelativePathSelectedOutOfRange",
                                                candidate.Installation.Version,
                                                minVersion,
                                                maxVersion),
                                            HintType.Error);
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
                    HintService.Hint(_GetJavaRangeWarning(
                        "Minecraft.Launch.Java.Compatibility.GlobalSelectedOutOfRange",
                        candidate.Installation.Version,
                        minVersion,
                        maxVersion));
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

        var candidates = Javas.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult();
        var ret = candidates.FirstOrDefault();

        if (ret is null && candidates.Length == 0)
        {
            ModBase.Log("[Java] 未找到符合版本要求的 Java，触发全盘重新扫描");
            Javas.ScanJavaAsync().GetAwaiter().GetResult();
            candidates = Javas.SelectSuitableJavaAsync(reqMin, reqMax).GetAwaiter().GetResult();
            ret = candidates.FirstOrDefault();
        }

        if (ret is not null)
            ModBase.Log($"[Java] 返回自动选择的 Java: {ret}");
        else
            ModBase.Log("[Java] 最终未能确定可用的 Java 运行时");

        return ret;
    }

    private static string _GetJavaRangeWarning(
        string key,
        Version selectedVersion,
        Version? minVersion,
        Version? maxVersion)
    {
        return Lang.Text(
            key,
            selectedVersion,
            minVersion?.ToString() ?? Lang.Text("Minecraft.Launch.Java.Compatibility.NoMinimum"),
            maxVersion?.ToString() ?? Lang.Text("Minecraft.Launch.Java.Compatibility.NoMaximum"));
    }

    public static JavaPreference GetInstanceJavaPreference(McInstance instance)
    {
        var rawPreference = Config.Instance.SelectedJava[instance.PathInstance];

        JavaPreference preference = default;
        
        // 尝试读取 JSON 配置
        if (!string.IsNullOrEmpty(rawPreference))
        {
            try
            {
                preference = JsonSerializer.Deserialize<JavaPreference>(rawPreference, JsonCompat.SerializerOptions);
            }
            catch (JsonException)
            {
                // ignored
            }
        }
        // 以旧方式读取配置
        if (preference is null)
        {
            var trimmed = rawPreference?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                preference = new AutoSelect();
            }
            else if (trimmed == "使用全局设置")
            {
                preference = new UseGlobalPreference();
            }
            else
            {
                preference = new ExistingJava(trimmed);
            }
        }

        switch (true)
        {
            case object _ when preference is ExistingJava:
            {
                var m = (ExistingJava)preference;
                if (!Path.IsPathRooted(m.JavaExePath)) preference = new UseGlobalPreference();

                break;
            }
            case object _ when preference is UseRelativePath:
            {
                var m = (UseRelativePath)preference;
                if (!Files.IsPathWithinDirectory(m.RelativePath, Basics.ExecutableDirectory))
                    preference = new UseGlobalPreference();

                break;
            }
        }

        return preference;
    }

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
            ModBase.Log(
                ex,
                "检查 Java 类别时出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Java.Compatibility.CheckFailed"));
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
        if (!forcedManualDownload)
            return ModMain.MyMsgBox(
                Lang.Text("Minecraft.Launch.Java.AutoDownload.Message", versionDescription),
                Lang.Text("Minecraft.Launch.Java.AutoDownload.Title"),
                Lang.Text("Minecraft.Launch.Java.AutoDownload.Action"),
                Lang.Text("Common.Action.Cancel")) == 1;

        ModMain.MyMsgBox(
            Lang.Text("Minecraft.Launch.Java.NotFound.Manual.Message", versionDescription),
            Lang.Text("Minecraft.Launch.Java.NotFound.Title"));

        return false;
    }

    /// <summary>
    ///     获取下载 Java 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    /// </summary>
    public static ModLoader.LoaderCombo<string> GetJavaDownloadLoader()
    {
        var javaDownloadLoader = new LoaderDownload(
            Lang.Text("Minecraft.Launch.Java.Task.DownloadFiles"),
            [])
        {
            ProgressWeight = 10d
        };

        var loader = new ModLoader.LoaderCombo<string>(
            Lang.Text("Minecraft.Launch.Java.Task.Download"),
            [
                new ModLoader.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Launch.Java.Task.GetDownloadInfo"),
                    JavaFileList)
                {
                    ProgressWeight = 2d
                },
                javaDownloadLoader
            ]);

        javaDownloadLoader.OnStateChangedThread += (_, newState, _) =>
        {
            switch (newState)
            {
                case ModBase.LoadState.Failed or ModBase.LoadState.Aborted
                    when lastJavaBaseDir is not null:
                    ModBase.Log(
                        $"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{lastJavaBaseDir}",
                        ModBase.LogLevel.Debug);

                    ModBase.DeleteDirectory(lastJavaBaseDir);
                    break;
                case ModBase.LoadState.Finished:
                    Javas.ScanJavaAsync().GetAwaiter().GetResult();
                    lastJavaBaseDir = null;
                    break;
            }
        };

        javaDownloadLoader.hasOnStateChangedThread = true;
        return loader;
    }

    private static string lastJavaBaseDir; // 用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java

    private static readonly HashSet<string> ignoreHash = new[]
    {
        "12976a6c2b227cbac58969c1455444596c894656", "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    }.ToHashSet();

    private static void JavaFileList(ModLoader.LoaderTask<string, List<DownloadFile>> loader)
    {
        ModBase.Log("[Java] 开始获取 Java 下载信息");
        var indexFileStr = ModNet.NetGetCodeByLoader(
            ModDownload.DlVersionListOrder(
                new[]
                {
                    "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                },
                new[]
                {
                    "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                }), isJson: true);
        // 查找要下载的目标 Java
        string? targetName = null;
        JsonNode? targetValue = null;
        var components =
            (JsonObject)((JsonObject)ModBase.GetJson(indexFileStr))[$"windows-x{(SystemInfo.Is32BitSystem ? "86" : "64")}"];
        if (components.ContainsKey(loader.input)) // 精确匹配
        {
            targetName = loader.input;
            targetValue = components[loader.input];
        }
        else // 模糊匹配
        {
            var match = components.FirstOrDefault(c =>
                c.Value?.AsArray().FirstOrDefault()?["version"]?["name"]?.ToString().StartsWithF(loader.input) ?? false);
            targetName = match.Key;
            targetValue = match.Value;
            if (targetName is null)
                throw new Exception($"未能找到所需的 Java {loader.input}");
        }

        var targetComponent = targetValue?.AsArray().FirstOrDefault();
        if (targetComponent is null)
            throw new Exception($"Mojang 未提供所需的 Java {loader.input}");
        // 获取文件列表
        var address = (string)targetComponent["manifest"]["url"];
        ModLaunch.McLaunchLog($"准备下载 Java {targetComponent["version"]["name"]}（{targetName}）：{address}");
        var listFileStr = (JsonObject)Requester.FetchJson(
            ModDownload.DlSourceOrder(new[] { address },
                new[] { address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com") }).First(), RequestParam.WithRetry);
        lastJavaBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "runtime", targetName);
        var results = new List<DownloadFile>(listFileStr["files"].AsObject().Count);
        foreach (var File in listFileStr["files"].AsObject())
        {
            if (File.Value?.AsObject()?["downloads"]?["raw"] is null)
                continue;

            var info = File.Value["downloads"]["raw"].AsObject();
            var checkHash = info["sha1"];
            if (ignoreHash.Contains((string)checkHash))
                continue; // 跳过 3 个无意义大量重复文件（#3827）

            var checker = new ModBase.FileChecker(actualSize: (long)info["size"], hash: (string)info["sha1"]);
            var filePath = Path.GetFullPath(Path.Combine(lastJavaBaseDir, File.Key));
            if (!Files.IsPathWithinDirectory(filePath, lastJavaBaseDir))
                throw new Exception($"{filePath} 不在 {lastJavaBaseDir} 中");

            if (checker.Check(filePath) is null)
                continue; // 跳过已存在的文件
            var url = (string)info["url"];
            results.Add(new DownloadFile(
                ModDownload.DlSourceOrder(new[] { url },
                    new[] { url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com") }), filePath, checker));
        }

        loader.output = results;
        ModBase.Log($"[Java] 需要下载 {results.Count} 个文件，目标文件夹：{lastJavaBaseDir}");
    }

    #endregion
}

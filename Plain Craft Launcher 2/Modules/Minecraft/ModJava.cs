using System.IO;
using System.Text.Json;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;

namespace PCL;

public static class ModJava
{
    public static int JavaListCacheVersion = 7;

    /// <summary>
    ///     防止多个需要 Java 的部分同时要求下载 Java（#3797）。
    /// </summary>
    public static object JavaLock = new();

    /// <summary>
    ///     目前所有可用的 Java。
    /// </summary>
    public static JavaManager Javas => JavaService.JavaManager;

    /// <summary>
    ///     根据要求返回最适合的 Java，若找不到则返回 Nothing。
    ///     最小与最大版本在与输入相同时也会通过。
    ///     必须在工作线程调用，且必须包括 SyncLock JavaLock。
    /// </summary>
    public static JavaEntry JavaSelect(string CancelException, Version MinVersion = null, Version MaxVersion = null,
        ModMinecraft.McInstance RelatedInstance = null)
    {
        ModBase.Log(
            $"[Java] 要求选择合适 Java，要求最低版本 {(MinVersion is not null ? MinVersion.ToString() : "未指定")}，要求选择的最高版本 {(MaxVersion is not null ? MaxVersion.ToString() : "未指定")}，关联实例 {(RelatedInstance is not null ? RelatedInstance.Name : "未指定")}");

        // 版本范围验证函数（安全处理 null 边界）
        bool IsVersionSuitable(Version ver)
        {
            return (MinVersion is null || ver >= MinVersion) && (MaxVersion is null || ver <= MaxVersion);
        }

        // ===== 优先级 1：实例专属 Java 偏好 =====
        if (RelatedInstance is not null && RelatedInstance.PathInstance is not null)
        {
            var rawPreference = Config.Instance.SelectedJava[RelatedInstance.PathInstance];

            if (!string.IsNullOrWhiteSpace(rawPreference))
            {
                var preference = GetInstanceJavaPreference(RelatedInstance);

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
                                        $"实例指定的 Java ({candidate.Installation.Version}) 超出版本要求范围 [{MinVersion?.ToString() ?? "无下限"}, {MaxVersion?.ToString() ?? "无上限"}]，可能导致游戏崩溃");
                                ModBase.Log($"[Java] 返回实例 '{RelatedInstance.Name}' 指定的 Java: {candidate}");
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
                                            $"实例相对路径指定的 Java (v{candidate.Installation.Version}) 超出版本要求范围，可能导致游戏崩溃",
                                            ModMain.HintType.Critical);
                                    ModBase.Log(
                                        $"[Java] 返回实例 '{RelatedInstance.Name}' 相对路径指定的 Java ({relPref.RelativePath}): {candidate}");
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
                            ModBase.Log($"[Java] 实例 '{RelatedInstance.Name}' 配置为使用全局 Java 设置，继续检查全局配置");
                            break;
                        }

                        default:
                        {
                            ModBase.Log($"[Java] 警告：未知的 Java 偏好类型 '{preference}'，跳过处理");
                            break;
                        }
                    }
                else
                    ModBase.Log($"[Java] 实例 '{RelatedInstance.Name}' 未指定 Java 偏好（空值），使用自动选择策略");
            }
            else
            {
                ModBase.Log($"[Java] 实例 '{RelatedInstance.Name}' 无 Java 偏好配置，使用自动选择策略");
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
                    ModMain.Hint($"全局指定的 Java (v{candidate.Installation.Version}) 超出版本要求范围，可能导致游戏崩溃");
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

        var reqMin = MinVersion ?? new Version(1, 0, 0);
        var reqMax = MaxVersion ?? new Version(999, 999, 999);

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

    public static JavaPreference GetInstanceJavaPreference(ModMinecraft.McInstance instance)
    {
        var rawPreference = Config.Instance.SelectedJava[instance.PathInstance];

        JavaPreference preference = default;
        
        // 尝试读取 JSON 配置
        if (!string.IsNullOrEmpty(rawPreference))
        {
            try
            {
                preference = JsonSerializer.Deserialize<JavaPreference>(rawPreference);
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
    public static bool IsGameSet64BitJava(ModMinecraft.McInstance RelatedVersion = null)
    {
        try
        {
            // 检查强制指定
            var UserSetup = Config.Launch.SelectedJava;
            if (UserSetup.StartsWith("{")) // 旧版本 Json 格式
            {
                var js = JsonNode.Parse(UserSetup);
                UserSetup = $"{js["Path"]}java.exe";
                Config.Launch.SelectedJava = UserSetup;
            }

            if (RelatedVersion is not null)
            {
                var instancePreference = GetInstanceJavaPreference(RelatedVersion);
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

            if (!string.IsNullOrEmpty(UserSetup) && !File.Exists(UserSetup))
            {
                Config.Launch.SelectedJava = "";
                UserSetup = string.Empty;
            }

            if (string.IsNullOrEmpty(UserSetup)) return Javas.Existing64BitJava();
            var j = Javas.AddOrGet(UserSetup);
            return j is not null && j.Installation.Is64Bit;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检查 Java 类别时出错", ModBase.LogLevel.Feedback);
            if (RelatedVersion is not null)
                Config.Instance.SelectedJava[RelatedVersion.PathInstance] = "使用全局设置";
            Config.Launch.SelectedJava = "";
        }

        return true;
    }

    #region 下载

    /// <summary>
    ///     提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    /// </summary>
    public static bool JavaDownloadConfirm(string VersionDescription, bool ForcedManualDownload = false)
    {
        if (ForcedManualDownload)
        {
            ModMain.MyMsgBox(
                $"PCL 未找到 {VersionDescription}。" + "\r\n" +
                $"请自行搜索并安装 {VersionDescription}，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。", "未找到 Java");
            return false;
        }

        return ModMain.MyMsgBox(
            $"PCL 未找到 {VersionDescription}，是否需要 PCL 自动下载？" + "\r\n" +
            $"如果你已经安装了 {VersionDescription}，可以在 设置 → 启动选项 → 游戏 Java 中手动导入。", "自动下载 Java？", "自动下载", Lang.Text("Common.Action.Cancel")) == 1;
    }

    /// <summary>
    ///     获取下载 Java 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    /// </summary>
    public static ModLoader.LoaderCombo<string> GetJavaDownloadLoader()
    {
        var JavaDownloadLoader = new LoaderDownload("下载 Java 文件", new List<DownloadFile>())
            { ProgressWeight = 10d };
        var Loader = new ModLoader.LoaderCombo<string>("下载 Java",
            new ModLoader.LoaderBase[]
            {
                new ModLoader.LoaderTask<string, List<DownloadFile>>("获取 Java 下载信息", JavaFileList)
                    { ProgressWeight = 2d },
                JavaDownloadLoader
            });
        JavaDownloadLoader.OnStateChangedThread += (Raw, NewState, OldState) =>
        {
            if ((NewState == ModBase.LoadState.Failed || NewState == ModBase.LoadState.Aborted) &&
                LastJavaBaseDir is not null)
            {
                ModBase.Log($"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{LastJavaBaseDir}", ModBase.LogLevel.Debug);
                ModBase.DeleteDirectory(LastJavaBaseDir);
            }
            else if (NewState == ModBase.LoadState.Finished)
            {
                Javas.ScanJavaAsync().GetAwaiter().GetResult();
                LastJavaBaseDir = null;
            }
        };
        JavaDownloadLoader.HasOnStateChangedThread = true;
        return Loader;
    }

    private static string LastJavaBaseDir; // 用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java

    private static readonly HashSet<string> IgnoreHash = new[]
    {
        "12976a6c2b227cbac58969c1455444596c894656", "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    }.ToHashSet();

    private static void JavaFileList(ModLoader.LoaderTask<string, List<DownloadFile>> Loader)
    {
        ModBase.Log("[Java] 开始获取 Java 下载信息");
        var IndexFileStr = ModNet.NetGetCodeByLoader(
            ModDownload.DlVersionListOrder(
                new[]
                {
                    "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                },
                new[]
                {
                    "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                }), IsJson: true);
        // 查找要下载的目标 Java
        string? targetName = null;
        JsonNode? targetValue = null;
        var Components =
            (JsonObject)((JsonObject)ModBase.GetJson(IndexFileStr))[$"windows-x{(ModBase.Is32BitSystem ? "86" : "64")}"];
        if (Components.ContainsKey(Loader.Input)) // 精确匹配
        {
            targetName = Loader.Input;
            targetValue = Components[Loader.Input];
        }
        else // 模糊匹配
        {
            var match = Components.FirstOrDefault(c =>
                c.Value?.AsArray().FirstOrDefault()?["version"]?["name"]?.ToString().StartsWithF(Loader.Input) ?? false);
            targetName = match.Key;
            targetValue = match.Value;
            if (targetName is null)
                throw new Exception($"未能找到所需的 Java {Loader.Input}");
        }

        var TargetComponent = targetValue?.AsArray().FirstOrDefault();
        if (TargetComponent is null)
            throw new Exception($"Mojang 未提供所需的 Java {Loader.Input}");
        // 获取文件列表
        var Address = (string)TargetComponent["manifest"]["url"];
        ModLaunch.McLaunchLog($"准备下载 Java {TargetComponent["version"]["name"]}（{targetName}）：{Address}");
        var ListFileStr = (JsonObject)Requester.FetchJson(
            ModDownload.DlSourceOrder(new[] { Address },
                new[] { Address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com") }).First(), RequestParam.WithRetry);
        LastJavaBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft", "runtime", targetName);
        var Results = new List<DownloadFile>(ListFileStr["files"].AsObject().Count);
        foreach (var File in ListFileStr["files"].AsObject())
        {
            if (File.Value?.AsObject()?["downloads"]?["raw"] is null)
                continue;

            var Info = File.Value["downloads"]["raw"].AsObject();
            var checkHash = Info["sha1"];
            if (IgnoreHash.Contains((string)checkHash))
                continue; // 跳过 3 个无意义大量重复文件（#3827）

            var Checker = new ModBase.FileChecker(ActualSize: (long)Info["size"], Hash: (string)Info["sha1"]);
            var filePath = Path.GetFullPath(Path.Combine(LastJavaBaseDir, File.Key));
            if (!Files.IsPathWithinDirectory(filePath, LastJavaBaseDir))
                throw new Exception($"{filePath} 不在 {LastJavaBaseDir} 中");

            if (Checker.Check(filePath) is null)
                continue; // 跳过已存在的文件
            var Url = (string)Info["url"];
            Results.Add(new DownloadFile(
                ModDownload.DlSourceOrder(new[] { Url },
                    new[] { Url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com") }), filePath, Checker));
        }

        Loader.Output = Results;
        ModBase.Log($"[Java] 需要下载 {Results.Count} 个文件，目标文件夹：{LastJavaBaseDir}");
    }

    #endregion
}

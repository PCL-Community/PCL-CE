using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Utils.OS;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class JavaRuntime
{
    // 用于在下载中断或失败时删除未完成下载的 Java 文件夹，防止残留只下了一半但 -version 能跑的 Java
    private static string? _lastJavaBaseDir;

    private static readonly HashSet<string> IgnoreHash =
    [
        "12976a6c2b227cbac58969c1455444596c894656",
        "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    ];

    /// <summary>
    ///     提示 Java 缺失，并弹窗确认是否自动下载。返回玩家选择是否下载。
    /// </summary>
    public static bool JavaDownloadConfirm(
        string versionDescription,
        bool forcedManualDownload = false)
    {
        if (!forcedManualDownload)
            return LauncherUi.Interaction?.ShowConfirm(
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Message", versionDescription),
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Title"),
                    Lang.Text("Minecraft.Launch.Java.AutoDownload.Action"),
                    Lang.Text("Common.Action.Cancel"))
                ?? false;

        LauncherUi.Interaction?.ShowMessageBox(
            Lang.Text("Minecraft.Launch.Java.NotFound.Manual.Message", versionDescription),
            Lang.Text("Minecraft.Launch.Java.NotFound.Title"));

        return false;
    }

    /// <summary>
    ///     获取下载 Java 的加载器。需要开启 IsForceRestart 以正常刷新 Java 列表。
    /// </summary>
    public static Loading.LoaderCombo<string> GetJavaDownloadLoader()
    {
        var javaDownloadLoader = new LoaderDownload(
            Lang.Text("Minecraft.Launch.Java.Task.DownloadFiles"),
            [])
        {
            ProgressWeight = 10d
        };

        var loader = new Loading.LoaderCombo<string>(
            Lang.Text("Minecraft.Launch.Java.Task.Download"),
            [
                new Loading.LoaderTask<string, List<DownloadFile>>(
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
                case LoadState.Failed or LoadState.Aborted
                    when _lastJavaBaseDir is not null:
                    LauncherCommon.Log(
                        $"[Java] 由于下载未完成，清理未下载完成的 Java 文件：{_lastJavaBaseDir}",
                        LauncherLogLevel.Debug);
                    LauncherCommon.DeleteDirectory(_lastJavaBaseDir);
                    break;

                case LoadState.Finished:
                    Javas.ScanJavaAsync().GetAwaiter().GetResult();
                    _lastJavaBaseDir = null;
                    break;
            }
        };

        javaDownloadLoader.hasOnStateChangedThread = true;
        return loader;
    }

    private static void JavaFileList(Loading.LoaderTask<string, List<DownloadFile>> loader)
    {
        LauncherCommon.Log("[Java] 开始获取 Java 下载信息");

        var components = GetJavaRuntimeComponents();
        var requestedName = loader.input
            ?? throw new InvalidOperationException("Java 下载加载器输入为空");
        var (targetName, targetComponent) = SelectJavaRuntimeComponent(components, requestedName);
        var address = targetComponent["manifest"]?["url"]?.ToString()
            ?? throw new Exception($"Java {targetName} 的下载信息缺少地址");
        var targetVersion = targetComponent["version"]?["name"]?.ToString() ?? targetName;

        LauncherCommon.Log(
            $"准备下载 Java {targetVersion}（{targetName}）：{address}");

        var manifest = FetchJavaRuntimeManifest(address);
        _lastJavaBaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft",
            "runtime",
            targetName);

        var results = CreateJavaDownloadFiles(manifest, _lastJavaBaseDir);
        loader.output = results;

        LauncherCommon.Log(
            $"[Java] 需要下载 {results.Count} 个文件，目标文件夹：{_lastJavaBaseDir}");
    }

    private static JsonObject GetJavaRuntimeComponents()
    {
        var indexFile = NetFacade.NetGetCodeByLoader(
            GameDownload.DlVersionListOrder(
                [
                    "https://piston-meta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                ],
                [
                    "https://bmclapi2.bangbang93.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json"
                ]),
            isJson: true);

        return ((JsonObject)LauncherCommon.GetJson(indexFile))[
                $"windows-x{(SystemInfo.Is32BitSystem ? "86" : "64")}"] as JsonObject
            ?? throw new Exception("获取 Java 运行时索引失败");
    }

    private static (string Name, JsonNode Component) SelectJavaRuntimeComponent(
        JsonObject components,
        string requestedName)
    {
        string? targetName;
        JsonNode? targetValue;

        if (components.ContainsKey(requestedName))
        {
            targetName = requestedName;
            targetValue = components[requestedName];
        }
        else
        {
            var match = components.FirstOrDefault(component =>
                component.Value?.AsArray().FirstOrDefault()?["version"]?["name"]
                    ?.ToString()
                    .StartsWithF(requestedName)
                ?? false);

            targetName = match.Key;
            targetValue = match.Value;

            if (targetName is null)
                throw new Exception($"未能找到所需的 Java {requestedName}");
        }

        var targetComponent = targetValue?.AsArray().FirstOrDefault();
        return targetComponent is null
            ? throw new Exception($"Mojang 未提供所需的 Java {requestedName}")
            : (targetName, targetComponent);
    }

    private static JsonObject FetchJavaRuntimeManifest(string address)
    {
        var manifestUrl = GameDownload
            .DlSourceOrder(
                [address],
                [address.Replace("piston-meta.mojang.com", "bmclapi2.bangbang93.com")])
            .First();

        return (JsonObject)Requester.FetchJson(manifestUrl, RequestParam.WithRetry);
    }

    private static List<DownloadFile> CreateJavaDownloadFiles(
        JsonObject manifest,
        string baseDirectory)
    {
        var manifestFiles = manifest["files"]?.AsObject()
            ?? throw new Exception("Java 运行时清单缺少 files 字段");
        var results = new List<DownloadFile>(manifestFiles.Count);

        foreach (var file in manifestFiles)
        {
            var rawDownloads = file.Value?.AsObject()?["downloads"]?["raw"];
            if (rawDownloads is null)
                continue;

            var info = rawDownloads.AsObject();
            var sha1 = info["sha1"]?.ToString()
                ?? throw new Exception($"Java 运行时清单项 {file.Key} 缺少 sha1");
            if (IgnoreHash.Contains(sha1))
                continue;

            var size = info["size"]?.GetValue<long>()
                ?? throw new Exception($"Java 运行时清单项 {file.Key} 缺少 size");
            var checker = new FileChecker(
                actualSize: size,
                hash: sha1);
            var filePath = Path.GetFullPath(Path.Combine(baseDirectory, file.Key));

            if (!Files.IsPathWithinDirectory(filePath, baseDirectory))
                throw new Exception($"{filePath} 不在 {baseDirectory} 中");

            if (checker.Check(filePath) is null)
                continue;

            var url = info["url"]?.ToString()
                ?? throw new Exception($"Java 运行时清单项 {file.Key} 缺少 url");
            results.Add(
                new DownloadFile(
                    GameDownload.DlSourceOrder(
                        [url],
                        [url.Replace("piston-data.mojang.com", "bmclapi2.bangbang93.com")]),
                    filePath,
                    checker));
        }

        return results;
    }
}
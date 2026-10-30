using System.IO;
using System.IO.Compression;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    private static void _AddModernForgelikeInstallLoaders(
        List<Loading.LoaderBase> loaders,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderName,
        string inherit,
        string loaderVersion,
        string targetVersion,
        string installerAddress,
        string versionFolder,
        string mcFolder,
        bool isCustomFolder,
        string? clientFolder,
        Func<Loading.LoaderCombo<string>?> clientLoaderAccessor
    )
    {
        Common.Log(
            $"[Download] 检测为{(forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                ? "新版 Forge"
                : $" {forgeType}")}：{loaderVersion}"
        );
        List<Libraries.McLibToken>? libs = null;
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeLoaderLibraries", loaderName),
                task => libs = _AnalyzeForgelikeLibraries(
                    task,
                    installerAddress,
                    inherit,
                    loaderName,
                    loaderVersion,
                    forgeType)
            )
            {
                ProgressWeight = 2d
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", loaderName),
                [])
            {
                ProgressWeight = 12d
            }
        );
        loaders.Add(
            new Loading.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.GetLoaderLibraries", loaderName),
                task =>
                    _ApplyForgelikeLibraries(
                        task,
                        libs ?? [],
                        isCustomFolder,
                        loaderName,
                        mcFolder,
                        clientFolder,
                        clientLoaderAccessor(),
                        inherit
                    )
            )
            {
                ProgressWeight = 0.1d,
                show = false
            }
        );
        loaders.Add(
            new Loading.LoaderTask<bool, bool>(
                forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodA")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", forgeType),
                task => _InstallModernForgelike(
                    task,
                    installerAddress,
                    mcFolder,
                    targetVersion,
                    versionFolder,
                    loaderName,
                    forgeType)
            )
            {
                ProgressWeight = 10d
            }
        );
    }

    private static List<Libraries.McLibToken> _AnalyzeForgelikeLibraries(
        Loading.LoaderTask<string, List<DownloadFile>> task,
        string installerAddress,
        string inherit,
        string loaderName,
        string loaderVersion,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        task.output = [];
        ZipArchive? installer = null;
        try
        {
            Common.WaitForFileReady(installerAddress);
            installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
            task.Progress = 0.2d;
            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            var versionEntry = installer.GetEntry("version.json")
                ?? throw new InvalidOperationException("安装器缺少 version.json");
            var json = (JsonObject)Common.GetJson(
                Common.ReadFile(installProfileEntry.Open()));
            var json2 = (JsonObject)Common.GetJson(
                Common.ReadFile(versionEntry.Open()));
            json.Merge(json2);
            if (inherit == "1.16.5" && Config.Download.FixAuthLib)
                json = (JsonObject)
                    Common.GetJson(
                        json
                            .ToString()
                            .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                            .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                            .Replace(
                                "ad54da276bf59983d02d5ed16fc14541354c71fd",
                                "bbd00ca33b052f73a6312254780fc580d2da3535")
                            .Replace("76328", "87662")
                    );
            var libs = Libraries.McLibListGetWithJson(json, true);
            if (json["data"]?["MOJMAPS"] != null)
            {
                task.Progress = 0.4d;
                var clientListUrl = GameDownload.DlClientListGet(inherit)?.ToString()
                    ?? throw new InvalidOperationException("未获取到 Minecraft Json 地址");
                var rawJson = (JsonObject)
                    Common.GetJson(
                        NetFacade.NetGetCodeByLoader(
                            GameDownload.DlSourceLauncherOrMetaGet(clientListUrl),
                            isJson: true
                        )
                    );
                var mappingsClientNode = json["data"]?["MOJMAPS"]?["client"]
                    ?? throw new InvalidOperationException("未找到 MOJMAPS 客户端映射");
                var mappingsClient = mappingsClientNode
                    .ToString()
                    .Trim("[]".ToCharArray());
                var originalName = mappingsClient.BeforeFirst("@");
                var address = Libraries
                    .McLibGet(originalName)
                    .Replace(
                        ".jar",
                        $"-mappings.{mappingsClient.Split("@")[1]}");
                var clientMappings = rawJson["downloads"]?["client_mappings"]
                    ?? throw new InvalidOperationException("未找到客户端映射下载信息");
                libs.Add(
                    new Libraries.McLibToken
                    {
                        IsNatives = false,
                        LocalPath = address,
                        OriginalName = originalName,
                        Url = clientMappings["url"]?.ToString() ?? "",
                        size = (long?)clientMappings["size"] ?? -1L,
                        Sha1 = clientMappings["sha1"]?.ToString() ?? ""
                    }
                );
                Common.Log(
                    $"[Download] 需要下载 Mappings：{clientMappings["url"]} (SHA1: {clientMappings["sha1"]})");
            }

            task.Progress = 0.8d;
            for (int i = 0, loopTo = libs.Count - 1; i <= loopTo; i++)
                if (libs[i]
                        .LocalPath.EndsWithF(
                            $"{loaderName.ToLower()}-{inherit}-{loaderVersion}.jar")
                    || libs[i]
                        .LocalPath.EndsWithF(
                            $"{loaderName.ToLower()}-{inherit}-{loaderVersion}-client.jar"))
                {
                    Common.Log(
                        $"[Download] 已从待下载 {loaderName} 支持库中移除：{libs[i].LocalPath}",
                        Common.LogLevel.Debug);
                    libs.RemoveAt(i);
                    break;
                }

            task.output = Libraries.McLibNetFilesFromTokens(libs);
            return libs;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderLibraryListFailed",
                    forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                        ? Lang.Text("Minecraft.Download.Loader.NewForge")
                        : $" {forgeType}"
                ),
                ex
            );
        }
        finally
        {
            installer?.Dispose();
        }
    }

    private static void _ApplyForgelikeLibraries(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        List<Libraries.McLibToken> libs,
        bool isCustomFolder,
        string loaderName,
        string mcFolder,
        string? clientFolder,
        Loading.LoaderCombo<string>? clientDownloadLoader,
        string inherit
    )
    {
        if (isCustomFolder)
        {
            if (GameFolders.mcFolderSelected is not { } selectedMcFolder)
                return;
            foreach (var libFile in libs)
            {
                var realPath = libFile.LocalPath.Replace(selectedMcFolder, mcFolder);
                if (!File.Exists(realPath))
                {
                    var realPathFolder = Path.GetDirectoryName(realPath);
                    if (realPathFolder is not null)
                        Directory.CreateDirectory(realPathFolder);
                    Common.CopyFile(libFile.LocalPath, realPath);
                }

                if (Common.modeDebug)
                    Common.Log($"[Download] 复制的 {loaderName} 支持库文件：{libFile.LocalPath}");
            }
        }

        if (clientDownloadLoader is null)
            return;

        var targetLoaders = clientDownloadLoader
            .GetLoaderList()
            .Where(l =>
                (l.name ?? "") == McDownloadClientLibName
                || (l.name ?? "") == McDownloadClientJsonName)
            .Where(l => l.State != Common.LoadState.Finished)
            .ToList();
        if (targetLoaders.Count != 0)
            Common.Log($"[Download] {loaderName} 安装正在等待原版文件下载完成");
        while (targetLoaders.Count != 0 && !task.IsAborted)
        {
            targetLoaders = [.. targetLoaders.Where(l => l.State != Common.LoadState.Finished)];
            Thread.Sleep(50);
        }

        if (task.IsAborted)
            return;
        if (!isCustomFolder || clientFolder is null)
            return;

        lock (VanillaSyncLock)
        {
            var clientName = Common.GetFolderNameFromPath(clientFolder);
            var inheritedVersionFolder = Path.Combine(mcFolder, "versions", inherit);
            Directory.CreateDirectory(inheritedVersionFolder);
            if (!File.Exists(Path.Combine(inheritedVersionFolder, $"{inherit}.json")))
                Common.CopyFile(
                    Path.Combine(clientFolder, $"{clientName}.json"),
                    Path.Combine(
                        inheritedVersionFolder,
                        $"{inherit}.json"));
            if (!File.Exists(Path.Combine(inheritedVersionFolder, $"{inherit}.jar")))
                Common.CopyFile(
                    Path.Combine(clientFolder, $"{clientName}.jar"),
                    Path.Combine(
                        inheritedVersionFolder,
                        $"{inherit}.jar"));
        }
    }

    private static void _InstallModernForgelike(
        Loading.LoaderTask<bool, bool> task,
        string installerAddress,
        string mcFolder,
        string targetVersion,
        string versionFolder,
        string loaderName,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        Common.WaitForFileReady(installerAddress);
        var installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
        try
        {
            Common.Log($"[Download] 开始进行 Forgelike 安装：{installerAddress}");
            var oldList = new DirectoryInfo(Path.Combine(mcFolder, "versions"))
                .EnumerateDirectories()
                .Select(i => i.FullName)
                .ToList();

            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            _ = Common.GetJson(Common.ReadFile(installProfileEntry.Open()));
            Directory.CreateDirectory(versionFolder);
            task.Progress = 0.04d;
            GameFolders.McFolderLauncherProfilesJsonCreate(mcFolder);
            task.Progress = 0.05d;
            var useJavaWrapper = Common.IsUtf8CodePage();

            _RunForgelikeInstallerWithRetry(
                task,
                installerAddress,
                mcFolder,
                useJavaWrapper,
                forgeType,
                loaderName);

            task.Progress = 0.97d;
            _CopyGeneratedForgelikeInstance(mcFolder, oldList, versionFolder, targetVersion);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.LoaderInstallFailed", loaderName),
                ex);
        }
        finally
        {
            try
            {
                installer?.Dispose();
                if (File.Exists(installerAddress))
                    File.Delete(installerAddress);
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"安装 {loaderName} 清理文件时出错");
            }
        }
    }

    private static void _RunForgelikeInstallerWithRetry(
        Loading.LoaderTask<bool, bool> task,
        string installerAddress,
        string mcFolder,
        bool useJavaWrapper,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderName
    )
    {
        var useWrapper = useJavaWrapper;
        while (true)
            try
            {
                Common.WriteFile(
                    Path.Combine(Common.pathTemp, "Cache", "forge_installer.jar"),
                    Common.GetResourceStream("Resources/forge-installer.jar")
                    ?? throw new InvalidOperationException("缺少 forge-installer.jar 资源")
                );
                task.Progress = 0.06d;
                ForgelikeInjector(installerAddress, task, mcFolder, useWrapper, forgeType);
                return;
            }
            catch (Exception ex)
            {
                if (useWrapper)
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", loaderName),
                        ex);

                Common.Log(ex, $"不使用 JavaWrapper 安装 {loaderName} 失败，将使用 JavaWrapper 并重试");
                useWrapper = true;
            }
    }

    private static void _CopyGeneratedForgelikeInstance(
        string mcFolder,
        List<string> oldList,
        string versionFolder,
        string targetVersion)
    {
        var deltaList = new DirectoryInfo(Path.Combine(mcFolder, "versions"))
            .EnumerateDirectories()
            .SkipWhile(i => oldList.Contains(i.FullName))
            .ToList();

        if (deltaList.Count > 1)
            deltaList =
            [
                .. deltaList.Where(l =>
                    l.Name.ContainsF("forge", true) && l.EnumerateFiles().Any())
            ];
        switch (deltaList.Count)
        {
            case 1:
            {
                var jsonFile = deltaList[0].EnumerateFiles().First();
                Common.WriteFile(
                    Path.Combine(versionFolder, $"{targetVersion}.json"),
                    Common.ReadFile(jsonFile.FullName));
                Common.Log(
                    $"[Download] 已拷贝新增的实例 Json 文件：{jsonFile.FullName} -> {versionFolder}{targetVersion}.json");
                break;
            }
            case > 1:
                Common.Log(
                    $"[Download] 有多个疑似的新增实例，无法确定：{string.Join(
                        ";",
                        deltaList.Select<DirectoryInfo, string>(d => d.Name))}"
                );
                break;
            default:
                Common.Log("[Download] 未找到新增的实例文件夹");
                break;
        }
    }
}
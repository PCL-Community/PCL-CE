using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 OptiFine 实例的加载器列表。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadOptiFineLoader(
        GameDownload.DlOptiFineListEntry downloadInfo,
        string? mcFolder = null,
        Loading.LoaderCombo<string>? clientDownloadLoader = null,
        string? clientFolder = null,
        bool fixLibrary = true
    )
    {
        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var isCustomFolder = (mcFolder ?? "") != (GameFolders.mcFolderSelected ?? "");
        var id = downloadInfo.NameVersion
            ?? throw new InvalidOperationException("OptiFine 版本缺少实例名");
        var inherit = downloadInfo.Inherit ?? "";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", id);
        var isNewVersion = inherit.Contains("w")
            || McVersionComparer.CompareVersionGe(inherit, "1.14");
        var target = _GetOptiFineLoaderTarget(downloadInfo, isNewVersion, mcFolder ?? "");
        var loaders = new List<Loading.LoaderBase>();
        var javaVersion = McVersionComparer.CompareVersionGe(inherit, "26.1.2")
            ? new Version(21, 0, 0, 0)
            : new Version(1, 8, 0, 0);

        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainOptiFineUrl"),
                task =>
                {
                    clientDownloadLoader = _EnsureOptiFineClientLoader(
                        clientDownloadLoader,
                        isCustomFolder,
                        inherit);

                    task.Progress = 0.1d;
                    var sources = _GetOptiFineDownloadSources(downloadInfo, task);
                    task.output =
                    [
                        new DownloadFile([.. sources], target, new Common.FileChecker(300 * 1024))
                    ];
                }
            )
            {
                ProgressWeight = 8d
            }
        );
        loaders.Add(
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"), [])
            {
                ProgressWeight = 8d
            });
        loaders.Add(
            new Loading.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.WaitVanillaDownload"),
                task =>
                    _WaitForOptiFineVanillaDownload(
                        task,
                        clientDownloadLoader,
                        isCustomFolder,
                        mcFolder ?? "",
                        clientFolder,
                        inherit
                    )
            )
            {
                ProgressWeight = 0.1d,
                show = false
            }
        );

        if (isNewVersion)
        {
            Common.Log($"[Download] 检测为新版 OptiFine：{inherit}");
            loaders.Add(
                new Loading.LoaderTask<List<DownloadFile>, bool>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodA"),
                    task => _InstallNewOptiFine(
                        task,
                        downloadInfo,
                        mcFolder ?? "",
                        target,
                        javaVersion)
                )
                {
                    ProgressWeight = 8d
                }
            );
        }
        else
        {
            Common.Log($"[Download] 检测为旧版 OptiFine：{inherit}");
            loaders.Add(
                new Loading.LoaderTask<List<DownloadFile>, bool>(
                    Lang.Text("Minecraft.Download.Stage.InstallOptiFine.MethodB"),
                    task => _InstallLegacyOptiFine(
                        task,
                        downloadInfo,
                        mcFolder ?? "",
                        versionFolder,
                        id)
                )
                {
                    ProgressWeight = 1d
                }
            );
        }

        if (fixLibrary)
            _AddOptiFineLibraryLoaders(loaders, versionFolder);

        return loaders;
    }

    private static void _AddOptiFineLibraryLoaders(
        List<Loading.LoaderBase> loaders,
        string versionFolder)
    {
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeOptiFineLibraries"),
                task => task.output =
                    Libraries.McLibNetFilesFromInstance(new McInstance(versionFolder))
            )
            {
                ProgressWeight = 1d,
                show = false
            }
        );
        loaders.Add(
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadOptiFineLibraries"), [])
            {
                ProgressWeight = 4d
            });
    }

    private static string _GetOptiFineLoaderTarget(
            GameDownload.DlOptiFineListEntry downloadInfo,
            bool isNewVersion,
            string mcFolder)
        {
            if (isNewVersion)
                return Path.Combine(Launcher.RequestTaskTempFolder(), "OptiFine.jar");

            var nameFile = downloadInfo.NameFile
                ?? throw new InvalidOperationException("OptiFine 版本缺少文件名");
            return Path.Combine(
                mcFolder,
                "libraries",
                "optifine",
                "OptiFine",
                nameFile
                    .Replace("OptiFine_", "")
                    .Replace(".jar", "")
                    .Replace("preview_", ""),
                nameFile.Replace("OptiFine_", "OptiFine-").Replace("preview_", "")
            );
        }

        private static Loading.LoaderCombo<string>? _EnsureOptiFineClientLoader(
            Loading.LoaderCombo<string>? clientDownloadLoader,
            bool isCustomFolder,
            string inherit
        )
        {
            if (clientDownloadLoader is not null)
                return clientDownloadLoader;

            if (isCustomFolder)
                throw new Exception(Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified"));

            return McDownloadClient(NetPreDownloadBehaviour.ExitWhileExistsOrDownloading, inherit);
        }

        private static List<string> _GetOptiFineDownloadSources(
            GameDownload.DlOptiFineListEntry downloadInfo,
            Loading.LoaderBase task)
        {
            var sources = new List<string>();
            var inherit = downloadInfo.Inherit ?? "";
            var displayName = downloadInfo.DisplayName ?? "";
            var bmclapiInherit = inherit;
            if (bmclapiInherit is "1.8" or "1.9")
                bmclapiInherit += ".0";

            if (downloadInfo.IsPreview)
                sources.Add(
                    $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U_{displayName.Replace($"{inherit} ", "").Replace(" ", "/")}"
                );
            else
                sources.Add(
                    $"https://bmclapi2.bangbang93.com/optifine/{bmclapiInherit}/HD_U/{displayName.Replace($"{inherit} ", "")}"
                );

            try
            {
                string pageData;
                using (
                    var resp = HttpRequest
                        .Create($"https://optifine.net/adloadx?f={downloadInfo.NameFile}")
                        .WithHeader("Accept", "text/html")
                        .WithHeader("Accept-Language", "en-US,en;q=0.5")
                        .WithHeader("X-Requested-With", "XMLHttpRequest")
                        .SendAsync()
                        .GetAwaiter()
                        .GetResult()
                )
                {
                    resp.EnsureSuccessStatusCode();
                    pageData = resp.AsString();
                }

                task.Progress = 0.8d;
                sources.Add(
                    $"https://optifine.net/{pageData.RegexSearch(@"downloadx\?f=[^""']+")[0]}");
                Common.Log(
                    $"[Download] OptiFine {downloadInfo.DisplayName} 官方下载地址：{sources.Last()}");
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"获取 OptiFine {downloadInfo.DisplayName} 官方下载地址失败");
            }

            return sources;
        }

        private static void _WaitForOptiFineVanillaDownload(
            Loading.LoaderTask<List<DownloadFile>, bool> task,
            Loading.LoaderCombo<string>? clientDownloadLoader,
            bool isCustomFolder,
            string mcFolder,
            string? clientFolder,
            string inherit
        )
        {
            if (clientDownloadLoader is null)
                return;

            var targetLoaders = clientDownloadLoader
                .GetLoaderList()
                .Where(l => l.Identifier is McDownloadClientLibId or McDownloadClientJsonId)
                .Where(l => l.State != Common.LoadState.Finished)
                .ToList();
            if (targetLoaders.Any())
                Common.Log("[Download] OptiFine 安装正在等待原版文件下载完成");
            while (targetLoaders.Any() && !task.IsAborted)
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

        /// <summary>
        ///     获取保存某个 OptiFine 版本的加载器列表。
        /// </summary>
        private static List<Loading.LoaderBase> McDownloadOptiFineSaveLoader(
            GameDownload.DlOptiFineListEntry downloadInfo,
            string targetFolder)
        {
            var loaders = new List<Loading.LoaderBase>
            {
                new Loading.LoaderTask<GameDownload.DlOptiFineListEntry, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.ObtainOptiFineDownloadUrl"),
                    task =>
                    {
                        var sources = _GetOptiFineDownloadSources(downloadInfo, task);
                        task.Progress = 0.9d;
                        task.output =
                        [
                            new DownloadFile(
                                [.. sources],
                                targetFolder,
                                new Common.FileChecker(64 * 1024))
                        ];
                    }
                )
                {
                    ProgressWeight = 6d
                },
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Stage.DownloadOptiFineMainFile"),
                    [])
                {
                    ProgressWeight = 10d,
                    block = true
                }
            };
            return loaders;
        }
    }
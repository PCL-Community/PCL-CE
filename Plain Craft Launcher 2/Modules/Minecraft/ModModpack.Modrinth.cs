using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region Modrinth

    private static LoaderCombo<string> _InstallModrinth(string sourcePath, IModpackArchiveReader archive,
        string archiveBaseFolder, string instanceName = null, string logo = null, string resourceId = null,
        bool isOnlineInstall = false)
    {
        // 读取 Json 文件
        JsonObject json;
        try
        {
            json = (JsonObject)ModBase.GetJson(archive.ReadEntryText(archiveBaseFolder + "modrinth.index.json"));
        }
        catch (Exception ex)
        {
            throw new Exception("Modrinth 整合包安装信息存在问题", ex);
        }

        var manifest = ModrinthManifest.Parse(json);
        if (manifest is null || manifest.GetDependency("minecraft") is null)
            throw new Exception("Modrinth 整合包未提供 Minecraft 版本信息");
        // 获取 Mod API 版本信息
        string minecraftVersion = null;
        string forgeVersion = null;
        string neoForgeVersion = null;
        string fabricVersion = null;
        var modLoader = ModComp.CompLoaderType.Any;
        foreach (var dependency in manifest.Dependencies ?? new Dictionary<string, string>())
            switch (dependency.Key.ToLower() ?? "")
            {
                case "minecraft":
                {
                    minecraftVersion = dependency.Value;
                    break;
                }
                case "forge": // eg. 14.23.5.2859 / 1.19-41.1.0
                {
                    forgeVersion = dependency.Value;
                    modLoader = ModComp.CompLoaderType.Forge;
                    ModBase.Log("[ModPack] 整合包 Forge 版本：" + forgeVersion);
                    break;
                }
                case "neoforge":
                case "neo-forge": // eg. 20.6.98-beta
                {
                    neoForgeVersion = dependency.Value;
                    modLoader = ModComp.CompLoaderType.NeoForge;
                    ModBase.Log("[ModPack] 整合包 NeoForge 版本：" + neoForgeVersion);
                    break;
                }
                case "fabric-loader": // eg. 0.14.14
                {
                    fabricVersion = dependency.Value;
                    modLoader = ModComp.CompLoaderType.Fabric;
                    ModBase.Log("[ModPack] 整合包 Fabric 版本：" + fabricVersion);
                    break;
                }
                case "quilt-loader": // eg. 0.26.0
                {
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
                }

                default:
                {
                    HintService.Hint(Lang.Text("Minecraft.Download.Modpack.UnknownLoader", dependency.Key,
                        dependency.Value), HintType.Error);
                    break;
                }
            }

        // 获取实例名
        if (instanceName is null)
            instanceName = _PromptInstanceName(manifest.Name ?? "");

        // 解压
        var installTemp = ModMain.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>();
        installLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
        {
            _ExtractModpackFiles(installTemp, sourcePath, task, 0.5d);
            _CopyOverrideDirectory(Path.Combine(installTemp, archiveBaseFolder, "overrides"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName), task, 0.4d);
            _CopyOverrideDirectory(Path.Combine(installTemp, archiveBaseFolder, "client-overrides"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName), task, 0.1d);
        })
        {
            ProgressWeight = _GetModpackProgressWeight(sourcePath),
            block = false
        }); // 每 6M 需要 1s
        // 获取下载文件列表
        var fileList = new List<DownloadFile>();
        // 目录形式的下载项（地图存档等）：先下载到临时目录，再解压到目标文件夹
        var archiveExtracts = new List<(string archivePath, string targetDirectory)>();
        foreach (var file in manifest.Files ?? [])
        {
            // 检查是否需要该文件
            if (file.Env is not null)
                switch (file.Env.Client ?? "")
                {
                    case "optional":
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Message",
                                    ModBase.GetFileNameFromPath(file.Path ?? "")),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download"),
                                Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")
                            ) == 2) continue;

                        break;
                    }
                    case "unsupported":
                    {
                        continue;
                    }
                }

            // 缺少下载地址或目标路径的文件项视为损坏，跳过（原实现会在此处抛出异常）
            if (file.Downloads is null || file.Downloads.Count == 0 || string.IsNullOrEmpty(file.Path))
            {
                HintService.Hint(Lang.Text("Minecraft.Download.Modpack.ModMissingRequiredInfoSkipped", file.Path ?? ""),
                    HintType.Warning);
                continue;
            }

            // 添加下载文件
            var urls = file.Downloads
                .Select(x => ModComp.CompFile.HandleCurseForgeDownloadUrls(x))
                .ToList();
            // 镜像源
            urls = urls.SelectMany(x => ModDownload.DlSourceModDownloadGet(x)).ToList();
            var targetPath = _GetVersionFolder(instanceName) + file.Path;
            if (!Path.GetFullPath(targetPath).StartsWithF(_GetVersionFolder(instanceName), true))
            {
                ModMain.MyMsgBox(Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Message", targetPath),
                    Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Title"), isWarn: true);
                throw new ModBase.CancelledException();
            }

            var fileChecker = new ModBase.FileChecker(actualSize: file.FileSize, hash: file.Hashes?.Sha1);
            if (_IsDirectoryDownload(file))
            {
                // 目录形式（文件名无扩展名，通常是地图存档 zip）：先下载到临时目录，安装阶段再解压
                var archivePath = Path.Combine(installTemp, "download_" + archiveExtracts.Count + ".tmp");
                fileList.Add(new DownloadFile(
                    ModComp.CompFile.HandleModrinthDownloadUrls(urls, ModComp.DownloadReason.ModPack, minecraftVersion,
                        modLoader), archivePath, fileChecker, true));
                archiveExtracts.Add((archivePath, targetPath));
            }
            else
            {
                fileList.Add(new DownloadFile(
                    ModComp.CompFile.HandleModrinthDownloadUrls(urls, ModComp.DownloadReason.ModPack, minecraftVersion,
                        modLoader), targetPath, fileChecker, true));
            }
        }

        if (fileList.Any())
            installLoaders.Add(
                new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadAdditions"), fileList)
                { ProgressWeight = fileList.Count * 1.5d }); // 每个 Mod 需要 1.5s
        // 解压目录形式的下载项（地图存档等）
        if (archiveExtracts.Any())
            installLoaders.Add(new LoaderTask<string, int>(
                Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"), task =>
            {
                foreach (var (archivePath, targetDirectory) in archiveExtracts)
                    _ExtractArchiveToDirectory(archivePath, targetDirectory);
            })
            { show = false, ProgressWeight = archiveExtracts.Count * 0.5d });

        // 构造加载器
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = _GetVersionFolder(instanceName),
            minecraftName = minecraftVersion,
            forgeVersion = forgeVersion,
            neoForgeVersion = neoForgeVersion,
            fabricVersion = fabricVersion,
        };
        var mergeLoaders = ModDownloadLib.McInstallLoader(request);
        // 构造总加载器
        var loaders = new List<LoaderBase>();
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
                installLoaders)
            { show = false, block = false, ProgressWeight = installLoaders.Sum(l => l.ProgressWeight) });
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), mergeLoaders)
            { show = false, ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight) });
        loaders.Add(new LoaderTask<string, string>(Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"), task =>
        {
            _FinalizeInstance(_GetVersionFolder(instanceName), sourcePath, logo, "Modrinth",
                manifest.VersionId, resourceId);
        })
        {
            ProgressWeight = 0.1d,
            show = false
        });

        // 启动
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.ModrinthInstall", instanceName);
        return _StartInstall(loaderName, loaders, request.targetInstanceFolder, isOnlineInstall);
    }

    /// <summary>
    ///     判断 Modrinth 文件项的目标是否是一个文件夹（文件名无扩展名）。
    ///     若是，下载内容应为需解压的压缩包（通常用于地图存档等文件夹形式的内容）。
    /// </summary>
    private static bool _IsDirectoryDownload(ModrinthFile file)
    {
        var path = file.Path ?? "";
        var trimmed = path.TrimEnd('/', '\\');
        return !string.IsNullOrEmpty(trimmed) && !Path.GetFileName(trimmed).Contains(".");
    }

    /// <summary>
    ///     将下载的压缩包解压到目标文件夹（用于地图存档等目录形式的下载项）。
    ///     解压到临时暂存目录后，若存在单层嵌套的存档根目录（zip 内再包一层文件夹）则直接使用该目录；
    ///     内容不是压缩包时回退为按普通文件复制。
    /// </summary>
    private static void _ExtractArchiveToDirectory(string archivePath, string targetDirectory)
    {
        try
        {
            var staging = Path.Combine(Path.GetDirectoryName(archivePath) ?? "", "_extract_" + Path.GetFileName(archivePath));
            ModBase.DeleteDirectory(staging);
            Directory.CreateDirectory(staging);
            ModBase.ExtractFile(archivePath, staging, Encoding.UTF8, null);
            var saveRoot = SaveImportHelper.GetSaveRootDirectory(staging) ?? staging;
            ModBase.CopyDirectory(saveRoot, targetDirectory, null);
            ModBase.DeleteDirectory(staging);
        }
        catch (Exception ex)
        {
            // 内容不是可解压的压缩包，改为按普通文件复制到目标路径
            ModBase.Log(ex, "整合包下载项按压缩包解压失败，改为直接复制文件：" + archivePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory) ?? targetDirectory);
            ModBase.CopyFile(archivePath, targetDirectory);
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
        }
    }

    #endregion
}

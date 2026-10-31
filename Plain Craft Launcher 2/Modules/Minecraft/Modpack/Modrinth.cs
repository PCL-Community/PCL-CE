using System.IO;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static LoaderCombo<string> InstallPackModrinth(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder,
        string? instanceName = null,
        string? logo = null,
        string? resourceId = null,
        bool isOnlineInstall = false)
    {
        var json = _ReadModrinthManifest(archive, archiveBaseFolder);
        var dependencies = json["dependencies"] as JsonObject;
        if (dependencies?["minecraft"] is null)
            throw new Exception("Modrinth 整合包未提供 Minecraft 版本信息");

        var loaderVersions = _ParseModrinthDependencies(dependencies);
        instanceName = _ResolveModpackInstanceName(instanceName, json["name"]?.ToString() ?? "");

        var installTemp = Launcher.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>
        {
            _CreateModrinthExtractLoader(fileAddress, archiveBaseFolder, instanceName, installTemp)
        };
        var fileList = _BuildModrinthDownloadFiles(
            json,
            instanceName,
            loaderVersions.MinecraftVersion,
            loaderVersions.ModLoader);
        if (fileList.Any())
            installLoaders.Add(
                new LoaderDownload(
                    Lang.Text("Minecraft.Download.Modpack.Stage.DownloadAdditions"),
                    fileList)
                {
                    ProgressWeight = fileList.Count * 1.5d
                });

        var request = new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\",
            minecraftName = loaderVersions.MinecraftVersion,
            forgeVersion = loaderVersions.ForgeVersion,
            neoForgeVersion = loaderVersions.NeoForgeVersion,
            fabricVersion = loaderVersions.FabricVersion
        };
        var mergeLoaders = DownloadUi.McInstallLoader(request);
        var loaders = _CreateModrinthLoaderList(
            installLoaders,
            mergeLoaders,
            fileAddress,
            json,
            instanceName,
            logo,
            resourceId);

        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.ModrinthInstall", instanceName);
        var loaderIdentifier = $"Modpack:Modrinth:{instanceName}";
        if (loaderTaskbar.Any(l => l.Identifier == loaderIdentifier))
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Modpack.Installing")));
            throw new Common.CancelledException();
        }

        var loader = new LoaderCombo<string>(loaderName, loaders)
        {
            OnStateChanged = DownloadUi.McInstallState,
            Identifier = loaderIdentifier
        };
        loader.Start(request.targetInstanceFolder);
        LoaderTaskbarAdd(loader);
        Launcher.frmMain!.BtnExtraDownload.ShowRefresh();
        if (!isOnlineInstall)
            Common.RunInUi(() => Launcher.frmMain.PageChange(FormMain.PageType.TaskManager));
        return loader;
    }

    private static JsonObject _ReadModrinthManifest(ZipArchive archive, string archiveBaseFolder)
    {
        try
        {
            return (JsonObject)
                Common.GetJson(
                    Common.ReadFile(
                        archive.GetEntry($"{archiveBaseFolder}modrinth.index.json")!.Open()));
        }
        catch (Exception ex)
        {
            throw new Exception("Modrinth 整合包安装信息存在问题", ex);
        }
    }

    private static ModrinthLoaderVersions _ParseModrinthDependencies(JsonObject dependencies)
    {
        string? minecraftVersion = null;
        string? forgeVersion = null;
        string? neoForgeVersion = null;
        string? fabricVersion = null;

        var modLoader = Comp.CompLoaderType.Any;
        foreach (var entry in dependencies)
            switch (entry.Key.ToLower() ?? "")
            {
                case "minecraft":
                    minecraftVersion = entry.Value?.ToObject<string>();
                    break;

                case "forge":
                    forgeVersion = entry.Value?.ToObject<string>();
                    modLoader = Comp.CompLoaderType.Forge;
                    Common.Log($"[ModPack] 整合包 Forge 版本：{forgeVersion}");
                    break;

                case "neoforge":
                case "neo-forge":
                    neoForgeVersion = entry.Value?.ToObject<string>();
                    modLoader = Comp.CompLoaderType.NeoForge;
                    Common.Log($"[ModPack] 整合包 NeoForge 版本：{neoForgeVersion}");
                    break;

                case "fabric-loader":
                    fabricVersion = entry.Value?.ToObject<string>();
                    modLoader = Comp.CompLoaderType.Fabric;
                    Common.Log($"[ModPack] 整合包 Fabric 版本：{fabricVersion}");
                    break;

                case "quilt-loader":
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));

                default:
                    HintService.Show(Hints.Error(Lang.Text(
                            "Minecraft.Download.Modpack.UnknownLoader",
                            entry.Key,
                            entry.Value
                        )));
                    break;
            }

        return new ModrinthLoaderVersions(
            minecraftVersion,
            forgeVersion,
            neoForgeVersion,
            fabricVersion,
            modLoader);
    }

    private static LoaderBase _CreateModrinthExtractLoader(
        string fileAddress,
        string archiveBaseFolder,
        string instanceName,
        string installTemp)
    {
        return new LoaderTask<string, int>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
            {
                ExtractModpackFiles(installTemp, fileAddress, task, 0.5d);
                CopyOverrideDirectory(
                    Path.Combine(installTemp, archiveBaseFolder, "overrides"),
                    Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName),
                    task,
                    0.4d);
                CopyOverrideDirectory(
                    Path.Combine(installTemp, archiveBaseFolder, "client-overrides"),
                    Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName),
                    task,
                    0.1d);
            })
        {
            ProgressWeight = new FileInfo(fileAddress).Length / 1024d / 1024d / 6d,
            block = false
        };
    }

    private static List<DownloadFile> _BuildModrinthDownloadFiles(
        JsonObject json,
        string instanceName,
        string? minecraftVersion,
        Comp.CompLoaderType modLoader)
    {
        var fileList = new List<DownloadFile>();
        foreach (var file in (dynamic)json["files"]! ?? Array.Empty<JsonNode>())
        {
            if (!_ShouldIncludeModrinthFile(file))
                continue;

            var urls = ((JsonArray)file["downloads"])
                .OfType<JsonNode>()
                .Select(x => Comp.CompFile.HandleCurseForgeDownloadUrls(x.ToString()))
                .ToList();
            urls = [.. urls.SelectMany(GameDownload.DlSourceModDownloadGet)];
            var targetPath =
                $@"{GameFolders.mcFolderSelected}versions\{instanceName}\{file["path"]}";
            if (!Path
                    .GetFullPath(targetPath)
                    .StartsWithF($@"{GameFolders.mcFolderSelected}versions\{instanceName}\", true))
            {
                DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Title"),
                    Content = Lang.Text("Minecraft.Download.Modpack.PathOutsideInstance.Message", targetPath),
                    Buttons = DialogButtons.Ok,
                    IsWarn = true,
                    ForceWait = true
                });
                throw new Common.CancelledException();
            }

            fileList.Add(
                new DownloadFile(
                    Comp.CompFile.HandleModrinthDownloadUrls(
                        urls,
                        Comp.DownloadReason.ModPack,
                        minecraftVersion,
                        modLoader
                    ),
                    targetPath,
                    new Common.FileChecker(
                        actualSize: ((JsonNode)file["fileSize"]).ToObject<long>(),
                        hash: file["hashes"]["sha1"].ToString()
                    ),
                    true));
        }

        return fileList;
    }

    private static bool _ShouldIncludeModrinthFile(JsonNode file)
    {
        var env = file["env"];
        if (env is null)
            return true;

        return env["client"]!.ToString() switch
        {
            "optional" => DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                    Content = Lang.Text(
                        "Minecraft.Download.Modpack.OptionalFile.Message",
                        Common.GetFileNameFromPath(file["path"]!.ToString())
                    ),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download")),
                        DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip"))),
                    ForceWait = true
                }).Result
                ?.Button != 2,
            "unsupported" => false,
            _ => true
        };
    }

    private static List<LoaderBase> _CreateModrinthLoaderList(
        List<LoaderBase> installLoaders,
        List<LoaderBase> mergeLoaders,
        string fileAddress,
        JsonObject json,
        string instanceName,
        string? logo,
        string? resourceId)
    {
        var loaders = _CreateModpackInstallLoaderList(installLoaders, mergeLoaders);
        loaders.Add(
            new LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"),
                _ => _FinalizeModpackInstance(
                    fileAddress,
                    instanceName,
                    logo,
                    resourceId,
                    "Modrinth",
                    json["versionId"]?.ToString()))
            {
                ProgressWeight = 0.1d,
                show = false
            });
        return loaders;
    }

    private sealed record ModrinthLoaderVersions(
        string? MinecraftVersion,
        string? ForgeVersion,
        string? NeoForgeVersion,
        string? FabricVersion,
        Comp.CompLoaderType ModLoader);
}
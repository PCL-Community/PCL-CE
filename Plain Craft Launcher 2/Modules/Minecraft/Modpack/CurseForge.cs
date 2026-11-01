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
    private static LoaderCombo<string> InstallPackCurseForge(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder,
        string? instanceName = null,
        string? logo = null,
        string? resourceId = null,
        bool isOnlineInstall = false)
    {
        var json = _ReadCurseForgeManifest(archive, archiveBaseFolder);
        if (json["minecraft"] is null || json["minecraft"]!["version"] is null)
            throw new Exception("CurseForge 整合包未提供 Minecraft 版本信息");

        instanceName = _ResolveModpackInstanceName(instanceName, json["name"]?.ToString() ?? "");
        var loaderVersions = _ParseCurseForgeLoaderVersions(json);

        var installTemp = Launcher.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>();
        var overrideHome = json["overrides"]?.ToString() ?? "";
        if (!string.IsNullOrEmpty(overrideHome))
            installLoaders.Add(
                _CreateCurseForgeExtractLoader(
                    fileAddress,
                    archiveBaseFolder,
                    instanceName,
                    installTemp,
                    overrideHome));

        var (modList, modOptionalList) = _CollectCurseForgeModIds(json);
        if (modList.Count != 0)
            installLoaders.Add(
                _CreateCurseForgeModDownloadLoaders(
                    modList,
                    modOptionalList,
                    json,
                    instanceName,
                    loaderVersions.ModLoader));

        var request = _CreateCurseForgeInstallRequest(instanceName, json, loaderVersions);
        var mergeLoaders = DownloadUi.McInstallLoader(request);
        var loaders = _CreateCurseForgeLoaderList(
            installLoaders,
            mergeLoaders,
            fileAddress,
            json,
            instanceName,
            logo,
            resourceId);

        var loaderName = Lang.Text(
            "Minecraft.Download.Modpack.Task.CurseForgeInstall",
            instanceName);
        var loaderIdentifier = $"Modpack:CurseForge:{instanceName}";
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

    private static JsonObject _ReadCurseForgeManifest(ZipArchive archive, string archiveBaseFolder)
    {
        try
        {
            return (JsonObject)
                Common.GetJson(
                    Common.ReadFile(archive.GetEntry($"{archiveBaseFolder}manifest.json")!.Open()));
        }
        catch (Exception ex)
        {
            throw new Exception("CurseForge 整合包安装信息存在问题", ex);
        }
    }

    private static CurseForgeLoaderVersions _ParseCurseForgeLoaderVersions(JsonObject json)
    {
        string? forgeVersion = null;
        string? neoForgeVersion = null;
        string? fabricVersion = null;
        var modLoader = Comp.CompLoaderType.Any;
        foreach (var entry in (dynamic)json["minecraft"]!["modLoaders"]! ?? Array.Empty<JsonNode>())
        {
            string id = (entry["id"] ?? "").ToString().ToLower();
            if (id.StartsWithF("forge-"))
            {
                if (id.Contains("recommended"))
                    throw new Exception(Lang.Text("Minecraft.Download.Modpack.TooOldUnsupported"));
                Common.Log($"[ModPack] 整合包 Forge 版本：{id}");
                forgeVersion = id.Replace("forge-", "");
                modLoader = Comp.CompLoaderType.Forge;
            }
            else if (id.StartsWithF("neoforge-"))
            {
                Common.Log($"[ModPack] 整合包 NeoForge 版本：{id}");
                neoForgeVersion = id.Replace("neoforge-", "");
                modLoader = Comp.CompLoaderType.NeoForge;
            }
            else if (id.StartsWithF("fabric-"))
            {
                try
                {
                    Common.Log($"[ModPack] 整合包 Fabric 版本：{id}");
                    fabricVersion = id.Replace("fabric-", "");
                    modLoader = Comp.CompLoaderType.Fabric;
                    break;
                }
                catch (Exception ex)
                {
                    Common.Log(ex, $"读取整合包 Fabric 版本失败：{id}");
                }
            }
            else if (id.StartsWithF("quilt-"))
            {
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));
            }
        }

        return new CurseForgeLoaderVersions(
            forgeVersion,
            neoForgeVersion,
            fabricVersion,
            modLoader);
    }

    private static LoaderBase _CreateCurseForgeExtractLoader(
        string fileAddress,
        string archiveBaseFolder,
        string instanceName,
        string installTemp,
        string overrideHome)
    {
        return new LoaderTask<string, int>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
            {
                ExtractModpackFiles(installTemp, fileAddress, task, 0.6d);
                CopyOverrideDirectory(
                    Path.Combine(
                        installTemp,
                        archiveBaseFolder,
                        overrideHome is "." or "./" ? "" : overrideHome
                    ),
                    $@"{GameFolders.mcFolderSelected}versions\{instanceName}",
                    task,
                    0.4d);
            })
        {
            ProgressWeight = new FileInfo(fileAddress).Length / 1024d / 1024d / 6d,
            block = false
        };
    }

    private static (List<int> ModList, List<int> OptionalList) _CollectCurseForgeModIds(
        JsonObject json)
    {
        var modList = new List<int>();
        var modOptionalList = new List<int>();
        foreach (var modEntry in (dynamic)json["files"]! ?? Array.Empty<JsonNode>())
        {
            if (modEntry["projectID"] is null || modEntry["fileID"] is null)
            {
                HintService.Show(Hints.Info(Lang.Text(
                        "Minecraft.Download.Modpack.ModMissingRequiredInfoSkipped",
                        modEntry)));
                continue;
            }

            modList.Add((int)modEntry["fileID"]);
            if (modEntry["required"] is JsonNode requiredNode && !requiredNode.ToObject<bool>())
                modOptionalList.Add((int)modEntry["fileID"]);
        }

        return (modList, modOptionalList);
    }

    private static LoaderBase _CreateCurseForgeModDownloadLoaders(
        List<int> modList,
        List<int> modOptionalList,
        JsonObject json,
        string instanceName,
        Comp.CompLoaderType modLoader)
    {
        var modDownloadLoaders = new List<LoaderBase>
        {
            new LoaderTask<int, JsonArray>(
                Lang.Text("Minecraft.Download.Modpack.Stage.PrepareModsDownloadInfo"),
                task => _FetchCurseForgeModFiles(modList, task))
            {
                ProgressWeight = modList.Count / 10d
            },
            new LoaderTask<JsonArray, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Modpack.Stage.BuildModsDownloadInfo"),
                task =>
                    _BuildCurseForgeDownloadFiles(
                        task,
                        modList,
                        modOptionalList,
                        json,
                        instanceName,
                        modLoader))
            {
                ProgressWeight = modList.Count / 200d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods"), [])
            {
                ProgressWeight = modList.Count * 1.5d
            }
        };
        return new LoaderCombo<int>(
            Lang.Text("Minecraft.Download.Modpack.Stage.DownloadMods.MainLoader"),
            modDownloadLoaders)
        {
            show = false,
            ProgressWeight = modDownloadLoaders.Sum(l => l.ProgressWeight)
        };
    }

    private static void _FetchCurseForgeModFiles(List<int> modList, LoaderTask<int, JsonArray> task)
    {
        var allowMirror = true;
        JsonArray files;
        var tryCount = 0;
        do
        {
            tryCount += 1;
            var response = (JsonObject)
                Common.GetJson(
                    GameDownload.DlModRequest(
                        "https://api.curseforge.com/v1/mods/files",
                        "POST",
                        $"{{\"fileIds\": [{modList.Join(",")}]}}",
                        "application/json",
                        allowMirror));
            files = (JsonArray)response["data"]!;
            if (modList.Count <= files.Count)
            {
                Common.Log("[Modpack] 已获取到的模组数量足够，开始进行下一步");
                break;
            }

            allowMirror = false;
            Common.Log($"[Modpack] 获取模组数量不达标，设置镜像源允许状态为: {allowMirror}");
            if (tryCount > 3)
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.SomeModsDeleted"));
        }
        while (true);

        task.output = files;
    }

    private static List<DownloadFile> _BuildCurseForgeDownloadFiles(
        LoaderTask<JsonArray, List<DownloadFile>> task,
        List<int> modList,
        List<int> modOptionalList,
        JsonObject json,
        string instanceName,
        Comp.CompLoaderType modLoader)
    {
        var fileList = new Dictionary<int, DownloadFile>();
        foreach (var modJson in task.input!.OfType<JsonObject>())
        {
            var id = modJson["id"].ToObject<int>();
            if (fileList.ContainsKey(id))
                continue;
            if (modOptionalList.Contains(id)
                && DialogService.Instance.Show(new TextDialogRequest
                    {
                        Title = Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
                        Content = Lang.Text(
                            "Minecraft.Download.Modpack.OptionalFile.Message",
                            modJson["displayName"]
                        ),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download")),
                            DialogButton.Create(Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip"))),
                        ForceWait = true
                    }).Result
                    ?.Button
                == 2)
                continue;

            var (targetFolder, type) = _ResolveCurseForgeTargetFolder(modJson);
            var file = new Comp.CompFile(modJson, type);
            if (!file.Available)
                continue;

            fileList.Add(
                id,
                file.ToNetFile(
                    $@"{GameFolders.mcFolderSelected}versions\{instanceName}\{targetFolder}\",
                    Comp.DownloadReason.ModPack,
                    json["minecraft"]!["version"]!.ToString(),
                    modLoader));
            task.Progress += 1d / (1 + modList.Count);
        }

        return [.. fileList.Values];
    }

    private static (string Folder, Comp.CompType Type) _ResolveCurseForgeTargetFolder(
        JsonObject modJson)
    {
        if (modJson["modules"]!.AsArray().Count == 0)
            return ("mods", Comp.CompType.Mod);

        var moduleNames = modJson["modules"]!
            .AsArray()
            .Select(l => l!["name"]!.ToString())
            .ToList();
        if (moduleNames.Contains("META-INF")
            || moduleNames.Contains("mcmod.info")
            || (modJson["FileName"]?.ToString()?.EndsWithF(".jar", true)).GetValueOrDefault())
            return ("mods", Comp.CompType.Mod);
        if (moduleNames.Contains("pack.mcmeta"))
            return ("resourcepacks", Comp.CompType.ResourcePack);
        if (moduleNames.Contains("level.dat"))
            return ("saves", Comp.CompType.World);
        return ("shaderpacks", Comp.CompType.Shader);
    }

    private static DownloadUi.McInstallRequest _CreateCurseForgeInstallRequest(
        string instanceName,
        JsonObject json,
        CurseForgeLoaderVersions loaderVersions)
    {
        return new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\",
            minecraftName = json["minecraft"]!["version"]!.ToString(),
            forgeVersion = loaderVersions.ForgeVersion,
            neoForgeVersion = loaderVersions.NeoForgeVersion,
            fabricVersion = loaderVersions.FabricVersion
        };
    }

    private static List<LoaderBase> _CreateCurseForgeLoaderList(
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
                    "CurseForge",
                    json["version"]?.ToString()))
            {
                ProgressWeight = 0.1d,
                show = false
            });
        return loaders;
    }

    private sealed record CurseForgeLoaderVersions(
        string? ForgeVersion,
        string? NeoForgeVersion,
        string? FabricVersion,
        Comp.CompLoaderType ModLoader);
}
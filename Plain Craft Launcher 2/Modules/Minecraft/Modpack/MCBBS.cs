using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using static PCL.Loading;

namespace PCL;

public static partial class Modpack
{
    private static string HandleLaunchArguments(JsonNode? argumentNode)
    {
        var arguments = argumentNode switch
        {
            JsonArray array => array
                .OfType<JsonValue>()
                .Where(argument => argument.GetValueKind() == JsonValueKind.String)
                .Select(argument => argument.GetValue<string>()),
            JsonValue argument when argument.GetValueKind() == JsonValueKind.String =>
            [
                argument.GetValue<string>()
            ],
            _ => []
        };
        return string.Join(" ", arguments.Where(argument => !string.IsNullOrWhiteSpace(argument)));
    }

    private static LoaderCombo<string>? InstallPackMcbbs(
        string fileAddress,
        ZipArchive archive,
        string archiveBaseFolder,
        string? instanceName = null)
    {
        var json = _ReadMcbbsManifest(archive, archiveBaseFolder);
        instanceName = _ResolveModpackInstanceName(instanceName, json["name"]?.ToString() ?? "");

        var installTemp = Launcher.RequestTaskTempFolder();
        var versionFolder = $"{GameFolders.mcFolderSelected}versions\\{instanceName}";
        var installLoaders = new List<LoaderBase>
        {
            _CreateMcbbsExtractLoader(
                fileAddress,
                archiveBaseFolder,
                instanceName,
                installTemp,
                versionFolder,
                json
            )
        };

        if (json["addons"] is null)
            throw new Exception(
                Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.McbbsAddons"));

        var addons = _ParseMcbbsAddons(json);
        if (!addons.TryGetValue("game", out var minecraftVersion))
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic")));
            return null;
        }

        if (addons.ContainsKey("quilt"))
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));

        var request = new DownloadUi.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = $@"{GameFolders.mcFolderSelected}versions\{instanceName}\",
            minecraftName = minecraftVersion,
            optiFineVersion = addons.GetValueOrDefault("optifine"),
            forgeVersion = addons.GetValueOrDefault("forge"),
            neoForgeVersion = addons.GetValueOrDefault("neoforge"),
            fabricVersion = addons.GetValueOrDefault("fabric")
        };

        var mergeLoaders = DownloadUi.McInstallLoader(request);
        var loaders = _CreateModpackInstallLoaderList(installLoaders, mergeLoaders);

        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.McbbsInstall", instanceName);
        var loaderIdentifier = $"Modpack:Mcbbs:{instanceName}";
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
        Common.RunInUi(() => Launcher.frmMain.PageChange(FormMain.PageType.TaskManager));

        return loader;
    }

    private static JsonObject _ReadMcbbsManifest(ZipArchive archive, string archiveBaseFolder)
    {
        try
        {
            var entry =
                archive.GetEntry($"{archiveBaseFolder}mcbbs.packmeta")
                ?? archive.GetEntry($"{archiveBaseFolder}manifest.json");
            using var stream = entry!.Open();
            return (JsonObject)Common.GetJson(Common.ReadFile(stream, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            throw new Exception("MCBBS 整合包安装信息存在问题", ex);
        }
    }

    private static LoaderBase _CreateMcbbsExtractLoader(
        string fileAddress,
        string archiveBaseFolder,
        string instanceName,
        string installTemp,
        string versionFolder,
        JsonObject json)
    {
        return new LoaderTask<string, int>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
            {
                ExtractModpackFiles(installTemp, fileAddress, task, 0.6);
                CopyOverrideDirectory(
                    Path.Combine(installTemp, archiveBaseFolder, "overrides"),
                    Path.Combine(GameFolders.mcFolderSelected!, "versions", instanceName),
                    task,
                    0.4);

                if (json["launchInfo"] is not null)
                {
                    var launchInfo = json["launchInfo"]!.AsObject();
                    Config.Instance.JvmArgs[versionFolder] = HandleLaunchArguments(
                        launchInfo["javaArgument"]);
                    Config.Instance.GameArgs[versionFolder] = HandleLaunchArguments(
                        launchInfo["launchArgument"]);
                }

                if (json["version"] is not null)
                    States.Instance.ModpackVersion[versionFolder] = json["version"]!.ToString();
            })
        {
            ProgressWeight = new FileInfo(fileAddress).Length / 1024.0 / 1024.0 / 6.0,
            block = false
        };
    }

    private static Dictionary<string, string> _ParseMcbbsAddons(JsonObject json)
    {
        return json["addons"]!
            .AsArray()
            .Select(entryNode => entryNode!.AsObject())
            .ToDictionary(
                entry => entry["id"]!.ToString(),
                entry => entry["version"]!.ToString());
    }
}
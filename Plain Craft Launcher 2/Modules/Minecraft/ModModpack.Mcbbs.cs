using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.Utils;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region MCBBS

    private static LoaderCombo<string> _InstallMcbbs(string fileAddress, ZipArchive archive,
        string archiveBaseFolder, string instanceName = null)
    {
        // 读取 Json 文件
        JsonObject json;
        try
        {
            var entry = archive.GetEntry(archiveBaseFolder + "mcbbs.packmeta") ??
                        archive.GetEntry(archiveBaseFolder + "manifest.json");
            using (var stream = entry.Open())
            {
                json = (JsonObject)ModBase.GetJson(ModBase.ReadFile(stream, Encoding.UTF8));
            }
        }
        catch (Exception ex)
        {
            throw new Exception("MCBBS 整合包安装信息存在问题", ex);
        }

        // 获取实例名
        if (instanceName is null)
            instanceName = _PromptInstanceName(json["name"]?.ToString() ?? "");

        // 解压与路径准备
        var installTemp = ModMain.RequestTaskTempFolder();
        var versionFolder = $"{ModFolder.mcFolderSelected}versions\\{instanceName}";
        var installLoaders = new List<LoaderBase>();

        // 解压整合包文件任务
        var unzipTask = new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
        {
            _ExtractModpackFiles(installTemp, fileAddress, task, 0.6);
            _CopyOverrideDirectory(
                Path.Combine(installTemp, archiveBaseFolder, "overrides"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName),
                task, 0.4);

            // JVM 参数处理
            if (json["launchInfo"] is not null)
            {
                var launchInfo = (JsonObject)json["launchInfo"];
                Config.Instance.JvmArgs[versionFolder] = string.Join(" ", launchInfo["javaArgument"]);
                Config.Instance.GameArgs[versionFolder] = string.Join(" ", launchInfo["launchArgument"]);
            }

            // 整合包版本
            if (json["version"] is not null) States.Instance.ModpackVersion[versionFolder] = json["version"].ToString();
        });

        unzipTask.ProgressWeight = new FileInfo(fileAddress).Length / 1024.0 / 1024.0 / 6.0; // 每 6M 需要 1s
        unzipTask.block = false;
        installLoaders.Add(unzipTask);

        // 构造加载器
        if (json["addons"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.McbbsAddons"));

        var addons = new Dictionary<string, string>();
        foreach (var entryNode in json["addons"].AsArray()) { var entry = entryNode.AsObject(); addons.Add(entry["id"].ToString(), entry["version"].ToString()); }

        if (!addons.ContainsKey("game"))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"), HintType.Error);
            return null;
        }

        if (addons.ContainsKey("quilt"))
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.QuiltUnsupported"));

        // 构造安装请求
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = _GetVersionFolder(instanceName),
            minecraftName = addons["game"],
            optiFineVersion = addons.ContainsKey("optifine") ? addons["optifine"] : null,
            forgeVersion = addons.ContainsKey("forge") ? addons["forge"] : null,
            neoForgeVersion = addons.ContainsKey("neoforge") ? addons["neoforge"] : null,
            fabricVersion = addons.ContainsKey("fabric") ? addons["fabric"] : null,
        };

        var mergeLoaders = ModDownloadLib.McInstallLoader(request);

        // 构造总加载器
        var loaders = new List<LoaderBase>();
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
            installLoaders)
        {
            show = false,
            block = false,
            ProgressWeight = installLoaders.Sum(l => l.ProgressWeight)
        });
        loaders.Add(new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), mergeLoaders)
        {
            show = false,
            ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight)
        });

        // 启动任务
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.McbbsInstall", instanceName);
        return _StartInstall(loaderName, loaders, request.targetInstanceFolder, false);
    }

    #endregion
}

using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Utils;
using static PCL.ModLoader;

namespace PCL;

public static partial class ModModpack
{
    #region HMCL

    private static LoaderCombo<string> _InstallHmcl(string sourcePath, IModpackArchiveReader archive,
        string archiveBaseFolder)
    {
        // 读取 Json 文件
        JsonObject json;
        try
        {
            json = (JsonObject)ModBase.GetJson(archive.ReadEntryText(archiveBaseFolder + "modpack.json"));
        }
        catch (Exception ex)
        {
            throw new Exception("HMCL 整合包安装信息存在问题", ex);
        }

        // 获取实例名
        var instanceName = _PromptInstanceName((string)(json["name"] ?? ""));

        // 解压
        var installTemp = ModMain.RequestTaskTempFolder();
        var installLoaders = new List<LoaderBase>();
        installLoaders.Add(new LoaderTask<string, int>(Lang.Text("Minecraft.Download.Modpack.Stage.ExtractModpack"),
            task =>
        {
            _ExtractModpackFiles(installTemp, sourcePath, task, 0.6d);
            _CopyOverrideDirectory(Path.Combine(installTemp, archiveBaseFolder, "minecraft"),
                Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName), task, 0.4d);
        })
        {
            ProgressWeight = _GetModpackProgressWeight(sourcePath),
            block = false
        }); // 每 6M 需要 1s
        // 构造游戏本体安装加载器
        if (json["gameVersion"] is null)
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Hmcl"));
        var request = new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = _GetVersionFolder(instanceName),
            minecraftName = json["gameVersion"].ToString()
        };
        var mergeLoaders = ModDownloadLib.McInstallLoader(request);
        // 构造总加载器
        var loaders = new List<LoaderBase>
        {
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"), installLoaders)
                { show = false, block = false, ProgressWeight = installLoaders.Sum(l => l.ProgressWeight) },
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), mergeLoaders)
                { show = false, ProgressWeight = mergeLoaders.Sum(l => l.ProgressWeight) }
        };

        // 启动
        var loaderName = Lang.Text("Minecraft.Download.Modpack.Task.HmclInstall", instanceName);
        return _StartInstall(loaderName, loaders, request.targetInstanceFolder, false);
    }

    #endregion
}

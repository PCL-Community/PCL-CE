using System.IO;
using System.IO.Compression;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class DownloadUi
{
    private static void _AddLegacyForgelikeInstallLoader(
        List<Loading.LoaderBase> loaders,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderVersion,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit
    )
    {
        Common.Log($"[Download] 检测为非新版 Forge：{loaderVersion}");
        loaders.Add(
            new Loading.LoaderTask<List<DownloadFile>, bool>(
                forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodB")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", forgeType),
                task => _InstallLegacyForgelike(
                    task,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    inherit)
            )
            {
                ProgressWeight = 1d
            }
        );
    }

    private static void _InstallLegacyForgelike(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit
    )
    {
        ZipArchive? installer = null;
        try
        {
            Common.WaitForFileReady(installerAddress);
            installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
            task.Progress = 0.2d;
            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            var json = (JsonObject)Common.GetJson(
                Common.ReadFile(installProfileEntry.Open()));
            task.Progress = 0.4d;
            Directory.CreateDirectory(versionFolder);
            task.Progress = 0.5d;
            if (json["install"] is null)
                _InstallLegacyForgelikeMethodOne(
                    task,
                    installer,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    json);
            else
                _InstallLegacyForgelikeMethodTwo(
                    task,
                    installer,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    inherit,
                    json);
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.ForgeOldInstallFailed"), ex);
        }
        finally
        {
            try
            {
                installer?.Dispose();
                if (File.Exists(installerAddress))
                    File.Delete(installerAddress);
                var unrarDir = Path.Combine(
                    Path.GetDirectoryName(installerAddress)
                    ?? throw new InvalidOperationException("安装器路径无效"),
                    "_unrar");
                if (Directory.Exists(unrarDir))
                    Common.DeleteDirectory(unrarDir);
            }
            catch (Exception ex)
            {
                Common.Log(ex, "非新版方式安装 Forge 清理文件时出错");
            }
        }
    }

    private static void _InstallLegacyForgelikeMethodOne(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        ZipArchive installer,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        JsonObject json
    )
    {
        Common.Log($"[Download] 开始进行 Forge 安装，Legacy 方式 1：{installerAddress}");
        var versionEntryName = json["json"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少版本 Json 信息");
        var versionEntry = installer.GetEntry(versionEntryName.TrimStart('/'))
            ?? throw new InvalidOperationException("安装器缺少版本 Json 文件");
        var jsonVersion = (JsonObject)Common.GetJson(
            Common.ReadFile(versionEntry.Open()));
        jsonVersion["id"] = targetVersion;
        Common.WriteFile(
            Path.Combine(versionFolder, $"{targetVersion}.json"),
            jsonVersion.ToString());
        task.Progress = 0.6d;
        installer.Dispose();
        var unrarDir = Path.Combine(
            Path.GetDirectoryName(installerAddress)
            ?? throw new InvalidOperationException("安装器路径无效"),
            "_unrar");
        Common.ExtractFile(installerAddress, unrarDir);
        Common.CopyDirectory(Path.Combine(unrarDir, "maven"), Path.Combine(mcFolder, "libraries"));
        Common.DeleteDirectory(unrarDir);
    }

    private static void _InstallLegacyForgelikeMethodTwo(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        ZipArchive installer,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit,
        JsonObject json
    )
    {
        Common.Log($"[Download] 开始进行 Forge 安装，Legacy 方式 2：{installerAddress}");
        var installInfo = json["install"]
            ?? throw new InvalidOperationException("安装器缺少 install 信息");
        var jarAddress = Libraries.McLibGet(
            installInfo["path"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少安装路径"),
            customMcFolder: mcFolder);
        if (File.Exists(jarAddress))
            File.Delete(jarAddress);
        var installEntryName = installInfo["filePath"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少安装文件名");
        Common.WriteFile(
            jarAddress,
            (installer.GetEntry(installEntryName)
                ?? throw new InvalidOperationException("安装器缺少安装文件")).Open());
        task.Progress = 0.9d;
        var versionInfo = json["versionInfo"]
            ?? throw new InvalidOperationException("安装器缺少 versionInfo 信息");
        versionInfo["id"] = targetVersion;
        if (versionInfo["inheritsFrom"] is null)
            ((JsonObject)versionInfo).Add("inheritsFrom", inherit);
        Common.WriteFile(
            Path.Combine(versionFolder, $"{targetVersion}.json"),
            versionInfo.ToString());
    }
}
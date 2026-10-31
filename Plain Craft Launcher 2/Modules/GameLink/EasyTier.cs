using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.EasyTier;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class GameLink
{
    public static Loading.LoaderCombo<JsonObject>? dlEasyTierLoader;

    public static int DownloadEasyTier()
    {
        var dlTargetPath = $"{Common.pathTemp}EasyTier\\EasyTier-{ETInfoProvider.ETVersion}.zip";

        Basics.RunInNewThread(() =>
        {
            try
            {
                var loaders = new List<Loading.LoaderBase>();

                var architecture = SystemInfo.IsArm64System
                    ? "arm64"
                    : "x86_64";
                var addresses = new List<string>
                {
                    $"https://staticassets.naids.com/resources/pclce/static/easytier/easytier-windows-{architecture}-v{ETInfoProvider.ETVersion}.zip",
                    $"https://s3.pysio.online/pcl2-ce/static/easytier/easytier-windows-{architecture}-v{ETInfoProvider.ETVersion}.zip"
                };

                loaders.Add(
                    new LoaderDownload(
                        Lang.Text("Link.Mod.Task.DownloadEasyTier"),
                        [
                            new DownloadFile(
                                [.. addresses],
                                dlTargetPath,
                                new FileChecker(1024 * 64))
                        ])
                    {
                        ProgressWeight = 15
                    });

                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Link.Mod.Task.ExtractFiles"),
                        _ => Common.ExtractFile(
                            dlTargetPath,
                            Path.Combine(
                                Paths.SharedLocalData,
                                "EasyTier",
                                ETInfoProvider.ETVersion))
                    )
                    {
                        block = true
                    });

                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Link.Mod.Task.CleanCache"),
                        _ =>
                        {
                            File.Delete(dlTargetPath);
                            CleanupEasyTierCache();
                        }));

                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Link.Mod.Task.RefreshUi"),
                        _ =>
                            HintWrapper.Show(
                                Lang.Text("Link.Mod.DownloadComplete"),
                                HintTheme.Success)
                    )
                    {
                        show = false
                    });

                dlEasyTierLoader =
                    new Loading.LoaderCombo<JsonObject>(
                        Lang.Text("Link.Mod.Task.InitLobby"),
                        loaders);
                dlEasyTierLoader.Start();

                Loading.LoaderTaskbarAdd(dlEasyTierLoader);
                Launcher.frmMain?.BtnExtraDownload.ShowRefresh();
                Launcher.frmMain?.BtnExtraDownload.Ribble();
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "Failed to download EasyTier dependency files");
                HintWrapper.Show(Lang.Text("Link.Mod.DownloadEasyTierFailed"), HintTheme.Error);
            }
        });

        return 0;
    }

    private static void CleanupEasyTierCache()
    {
        var subDirs = Directory.GetDirectories(Path.Combine(Paths.SharedLocalData, "EasyTier"));
        foreach (var folderPath in subDirs)
        {
            var name = Path.GetFileName(folderPath);
            if (name.Equals(ETInfoProvider.ETVersion)) continue;
            try
            {
                Directory.Delete(folderPath, true);
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Link] 清理旧版本 EasyTier 出错");
            }
        }
    }
}

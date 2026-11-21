using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Views.Pages.Select;

namespace PCL.UI.Bridges;

/// <summary>整合包安装与文件夹列表的桥接。</summary>
public sealed class ModpackUiBridge : IModpackUi
{
    public List<Loading.LoaderBase> CreateInstallLoader(McInstallRequest request)
    {
        return McInstallPipeline.CreateInstallLoader(request);
    }

    public void OnInstallStateChanged(Loading.LoaderBase loader)
    {
        var combo = loader as Loading.LoaderCombo;
        switch (loader.State)
        {
            case LoadState.Finished:
                HandleInstallFinished(loader, combo);
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Success")}",
                    NotificationType.Success);
                break;

            case LoadState.Failed:
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Failure")}{loader.Error?.Message}",
                    NotificationType.Error);
                break;

            case LoadState.Aborted:
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Cancelled")}",
                    NotificationType.Info);
                break;

            case LoadState.Loading:
                return;

            case LoadState.Waiting:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(loader));
        }

        McInstallRecovery.HandleTerminalState(loader, combo?.input?.ToString() ?? "");
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var root = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            root,
            Loading.LoaderFolderRunType.ForceRun,
            1,
            $"versions{Path.DirectorySeparatorChar}");
    }

    public void AddFolder(string folderPath, string displayName, bool showHint)
    {
        PageSelectLeft.AddFolder(folderPath, displayName, showHint);
    }

    private static void HandleInstallFinished(Loading.LoaderBase loader, Loading.LoaderCombo? combo)
    {
        var instanceFolder = combo?.input?.ToString() ?? "";
        var instanceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(instanceFolder));
        var mcFolder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(mcFolder) || string.IsNullOrEmpty(instanceName))
            return;
        var pclIni = Path.Combine(mcFolder, "PCL.ini");
        if (Config.Download.AutoSelectInstance)
            Ini.Write(pclIni, "Version", instanceName);
        Ini.Write(pclIni, "InstanceCache", "");
    }
}
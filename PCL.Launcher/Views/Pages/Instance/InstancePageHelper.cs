using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>实例设置各子页共用的取值、布局与提示辅助。</summary>
internal static class InstancePageHelper
{
    public static McInstance? ResolveInstance(PageParameter? parameter)
    {
        return parameter is McInstance instance
            ? instance
            : PageInstanceLeft.ResolveInstance();
    }

    public static string SavesPath(McInstance instance)
    {
        return Path.Combine(instance.PathIndie, "saves");
    }

    public static string ModsPath(McInstance instance)
    {
        return Path.Combine(instance.PathIndie, "mods");
    }

    public static string ScreenshotsPath(McInstance instance)
    {
        return Path.Combine(instance.PathIndie, "screenshots");
    }

    public static string DatapacksPath(string saveFolder)
    {
        return Path.Combine(saveFolder, "datapacks");
    }

    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                HintService.Show(Hints.Warning(Lang.Text("Instance.Saves.FolderNotFound")));
                return;
            }

            Basics.OpenPath(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "打开文件夹失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
            HintService.Show(Hints.Error(Lang.Text("Instance.Error.OperationFailed")));
        }
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024d && unit < units.Length - 1)
        {
            size /= 1024d;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    public static void ReportError(string titleKey, Exception ex)
    {
        LauncherCommon.Log(
            ex,
            Lang.Text(titleKey),
            LauncherLogLevel.Feedback,
            userSummary: Lang.Text("Instance.Error.OperationFailed"));
        HintService.Show(Hints.Error(Lang.Text("Instance.Error.OperationFailed")));
    }
}
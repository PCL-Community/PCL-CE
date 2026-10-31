using PCL.Core.App.Localization;

namespace PCL;

public static partial class Loading
{
    public enum LoaderFolderRunType
    {
        RunOnUpdated,
        ForceRun,
        UpdateOnly
    }

    public static SafeList<LoaderBase> loaderTaskbar = new();
    public static double loaderTaskbarProgress;

    private static TaskbarProgressState _loaderTaskbarProgressLast =
        TaskbarProgressState.None;

    private static readonly Dictionary<LoaderBase, LoaderFolderDictionaryEntry>
        LoaderFolderDictionary = new();

    public static void LoaderTaskbarAdd<T>(LoaderCombo<T> loader)
    {
        LoadingUi.Current?.TaskAdded(loader);
        loaderTaskbar.Add(loader);
        LauncherCommon.Log($"[Taskbar] {loader.name} 已加入任务列表");
    }

    public static void LoaderTaskbarProgressRefresh()
    {
        try
        {
            TaskbarProgressState newState;
            var newProgress = LoaderTaskbarProgressGet();
            foreach (var task in loaderTaskbar.ToList())
                if (loaderTaskbar.All(l => l.State != LoadState.Loading)
                    || task.State == LoadState.Waiting
                    || task.State == LoadState.Aborted)
                {
                    LoadingUi.Current?.TaskRefreshed(task);
                    loaderTaskbar.Remove(task);
                    LauncherCommon.Log($"[Taskbar] {task.name} 已移出任务列表");
                }

            if (newProgress <= 0d || newProgress >= 1d || loaderTaskbarProgress > newProgress)
                loaderTaskbarProgress = newProgress;
            else
                loaderTaskbarProgress = loaderTaskbarProgress * 0.9d + newProgress * 0.1d;
            LoadingUi.Current?.UpdateButtonProgress(loaderTaskbarProgress);
            if (loaderTaskbar.Count == 0 || loaderTaskbarProgress == 1d)
                newState = TaskbarProgressState.None;
            else if (loaderTaskbarProgress < 0.015d)
                newState = TaskbarProgressState.Indeterminate;
            else
                newState = TaskbarProgressState.Normal;

            if (_loaderTaskbarProgressLast == newState) return;

            _loaderTaskbarProgressLast = newState;
            LoadingUi.Current?.UpdateTaskbar(newState, loaderTaskbarProgress);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "刷新任务栏进度显示失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Application.Loader.Error.OperationFailed"));
        }
    }

    public static double LoaderTaskbarProgressGet()
    {
        try
        {
            return loaderTaskbar.Count == 0
                ? 1d
                : Math.Clamp(
                    loaderTaskbar.Select(l => l.Progress).Average(),
                    0,
                    1
                );
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "获取任务栏进度出错",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Application.Loader.Error.OperationFailed"));
            return 0.5d;
        }
    }

    /// <summary>
    ///     执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    ///     返回是否执行了加载器。
    /// </summary>
    /// <param name="extraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
    /// <param name="loaderInput">如果不想要文件夹路径为输入值，则传入期望数据</param>
    public static bool LoaderFolderRun(
        LoaderBase loader,
        string folderPath,
        LoaderFolderRunType type,
        int maxDepth = 0,
        string extraPath = "",
        bool waitForExit = false,
        object? loaderInput = null)
    {
        var value = new LoaderFolderDictionaryEntry
        {
            folderPath = folderPath + extraPath,
            lastCheckTime = null
        };
        try
        {
            var folderInfo = new DirectoryInfo(folderPath + extraPath);
            value.lastCheckTime = folderInfo.Exists
                ? GetActualLastWriteTimeUtc(folderInfo, maxDepth)
                : null;
            if (type == LoaderFolderRunType.RunOnUpdated
                && LoaderFolderDictionary.ContainsKey(loader))
            {
                if (folderInfo.Exists)
                {
                    if (LoaderFolderDictionary[loader].lastCheckTime is not null
                        && value.Equals(LoaderFolderDictionary[loader]))
                        return false;
                }
                else if (LoaderFolderDictionary[loader].lastCheckTime is null)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "文件夹加载器启动检测出错");
        }

        LoaderFolderDictionary[loader] = value;
        if (type == LoaderFolderRunType.UpdateOnly)
            return false;
        if (waitForExit)
            loader.WaitForExit(loaderInput ?? folderPath, isForceRestart: true);
        else
            loader.Start(loaderInput ?? folderPath, true);
        return true;
    }

    private static DateTime GetActualLastWriteTimeUtc(DirectoryInfo folderInfo, int maxDepth)
    {
        var time = folderInfo.LastWriteTimeUtc;
        if (maxDepth <= 0) return time;
        foreach (var folder in folderInfo.EnumerateDirectories())
        {
            var folderTime = GetActualLastWriteTimeUtc(folder, maxDepth - 1);
            if (folderTime > time)
                time = folderTime;
        }

        return time;
    }
}
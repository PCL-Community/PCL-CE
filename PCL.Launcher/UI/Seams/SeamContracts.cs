namespace PCL.UI.Seams;

/// <summary>
///     页面/壳向业务层暴露的接口与宿主注册表；宿主未登记时调用会记录警告并返回不可用结果。
/// </summary>
public interface ISeamLaunchPage
{
    void RefreshLaunchButtons();

    void RefreshLaunchPage(bool show);

    void ShowLogin();

    void ShowLaunching();

    void RefreshLaunching();
}

/// <summary>启动页日志输出目标（启动页右侧的启动日志文本）。</summary>
public interface ISeamLaunchLogSink
{
    void AppendLaunchLog(string text);

    void ClearLaunchLog();
}

/// <summary>游戏实时日志页左栏：接收需要显示日志的游戏监视器。</summary>
public interface ISeamGameLogSink
{
    void AttachWatcher(GameWatcher.Watcher watcher);
}

/// <summary>设置页“更新”子页：响应更新流程对更新按钮的修改。</summary>
public interface ISeamSetupUpdatePage
{
    void SetUpdateButton(string text, bool enabled);
}

/// <summary>任务管理页：接收后台加载器任务列表的变化。</summary>
public interface ISeamTaskListPage
{
    void OnTaskAdded(Loading.LoaderBase loader);

    void OnTaskRemoved(Loading.LoaderBase loader);

    void OnTaskRefreshed(Loading.LoaderBase loader);
}

/// <summary>实例本地资源页：向业务层公开加载状态控件与当前浏览文件夹。</summary>
public interface ISeamCompLocalPage
{
    bool LoadProgressVisible { get; set; }

    string? CurrentFolderPath { get; }

    void SetLoadText(string key);

    void UpdateOnly();

    void RefreshModList();
}

/// <summary>主窗口壳的快捷按钮组。</summary>
public interface ISeamShellActions
{
    void ShowUpdateRestartButton(string tooltip);

    void RefreshDownloadButton();

    void RefreshShutdownButton();
}

/// <summary>音频与背景视频宿主；当前版本没有可用的播放引擎，宿主通常不会登记。</summary>
public interface ISeamMediaHost
{
    void PauseMusic();

    void ResumeMusic();

    void SetVideoGaming(bool gaming);
}

/// <summary>seam 宿主注册表：页面、壳与媒体宿主在自身构造完成后登记实现。</summary>
public static class SeamHosts
{
    public static ISeamLaunchPage? LaunchPage { get; set; }

    public static ISeamLaunchLogSink? LaunchLog { get; set; }

    public static ISeamGameLogSink? GameLog { get; set; }

    public static ISeamSetupUpdatePage? SetupUpdatePage { get; set; }

    public static ISeamTaskListPage? TaskListPage { get; set; }

    public static ISeamShellActions? Shell { get; set; }

    public static ISeamMediaHost? Media { get; set; }
}

/// <summary>seam 能力查询：供页面/壳决定是否显示依赖某项能力的功能。</summary>
public static class SeamCapabilities
{
    public static bool MediaAvailable => SeamHosts.Media is not null;

    public static bool LaunchPageAvailable => SeamHosts.LaunchPage is not null;

    public static bool TaskListAvailable => SeamHosts.TaskListPage is not null;
}

/// <summary>本地资源更新占用登记：页面在批量更新期间登记资源目录，业务层据此等待更新完成。</summary>
public static class CompLocalUpdateRegistry
{
    private static readonly HashSet<string> Updating = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock Lock = new();

    public static void Begin(string compPath)
    {
        if (string.IsNullOrEmpty(compPath)) return;
        lock (Lock)
        {
            Updating.Add(Normalize(compPath));
        }
    }

    public static void End(string compPath)
    {
        if (string.IsNullOrEmpty(compPath)) return;
        lock (Lock)
        {
            Updating.Remove(Normalize(compPath));
        }
    }

    public static bool IsUpdating(string compPath)
    {
        if (string.IsNullOrEmpty(compPath)) return false;
        lock (Lock)
        {
            return Updating.Contains(Normalize(compPath));
        }
    }

    private static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(path);
    }
}
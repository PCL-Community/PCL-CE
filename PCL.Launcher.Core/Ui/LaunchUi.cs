namespace PCL;

public interface ILaunchUi
{
    int ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool forceWait = false,
        Action? button1Action = null);

    void SetGpuPreference(string javaExePath, bool preference);
    int? ShowSelect(string caption, IReadOnlyList<string> options);
    void OpenExplorer(string path);
    void PrepareLaunchNavigation();
    void RefreshLaunchButtons();
    void RefreshLaunchPage(bool show);
    void AppendLaunchLog(string text);
    void ClearLaunchLog();
    void ShowLoginPage();
    void ShowLaunchingPage();
    void RefreshLaunching();
    void AttachLogPage(GameWatcher.Watcher watcher);
    void SetMainWindowState(LaunchWindowState state);
    (double Width, double Height) GetMainPanSize();
}

public enum LaunchWindowState
{
    Exit,
    Hide,
    Minimize
}

public static class LaunchUi
{
    public static ILaunchUi? Current { get; set; }

    public static int ShowMessageBox(
        string text,
        string? title = null,
        string? button1 = null,
        string? button2 = "",
        string? button3 = "",
        bool isWarn = false,
        bool forceWait = false,
        Action? button1Action = null)
    {
        return Current?.ShowMessageBox(
                text,
                title,
                button1,
                button2,
                button3,
                isWarn,
                forceWait,
                button1Action)
            ?? 0;
    }

    public static void SetGpuPreference(string javaExePath, bool preference)
    {
        Current?.SetGpuPreference(javaExePath, preference);
    }

    public static int? ShowSelect(string caption, IReadOnlyList<string> options)
    {
        return Current?.ShowSelect(caption, options);
    }

    public static void OpenExplorer(string path)
    {
        Current?.OpenExplorer(path);
    }

    public static void PrepareLaunchNavigation()
    {
        Current?.PrepareLaunchNavigation();
    }

    public static void RefreshLaunchButtons()
    {
        Current?.RefreshLaunchButtons();
    }

    public static void RefreshLaunchPage(bool show)
    {
        Current?.RefreshLaunchPage(show);
    }

    public static void AppendLaunchLog(string text)
    {
        Current?.AppendLaunchLog(text);
    }

    public static void ClearLaunchLog()
    {
        Current?.ClearLaunchLog();
    }

    public static void ShowLoginPage()
    {
        Current?.ShowLoginPage();
    }

    public static void ShowLaunchingPage()
    {
        Current?.ShowLaunchingPage();
    }

    public static void RefreshLaunching()
    {
        Current?.RefreshLaunching();
    }

    public static void AttachLogPage(GameWatcher.Watcher watcher)
    {
        Current?.AttachLogPage(watcher);
    }

    public static void SetMainWindowState(LaunchWindowState state)
    {
        Current?.SetMainWindowState(state);
    }

    public static (double Width, double Height) GetMainPanSize()
    {
        return Current?.GetMainPanSize() ?? (0, 0);
    }
}
namespace PCL;

/// <summary>自定义事件涉及页面/工具的操作。</summary>
public interface IEventUi
{
    bool RefreshPage();
    void ShowDailyFortune();
    void ClearTrash();
    void SwitchPage(string pageType, string? subType);
    void StartModpackInstall();
    void StartCustomDownload(string url, string savePath, string? fileName);
    void OpenHelp(string location);
}

public static class EventUi
{
    public static IEventUi? Current { get; set; }

    public static bool RefreshPage()
    {
        return Current?.RefreshPage() ?? false;
    }

    public static void ShowDailyFortune()
    {
        Current?.ShowDailyFortune();
    }

    public static void ClearTrash()
    {
        Current?.ClearTrash();
    }

    public static void SwitchPage(string pageType, string? subType)
    {
        Current?.SwitchPage(pageType, subType);
    }

    public static void StartModpackInstall()
    {
        Current?.StartModpackInstall();
    }

    public static void StartCustomDownload(string url, string savePath, string? fileName)
    {
        Current?.StartCustomDownload(url, savePath, fileName);
    }

    public static void OpenHelp(string location)
    {
        Current?.OpenHelp(location);
    }
}
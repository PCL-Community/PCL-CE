using PCL.Core.Platform;

namespace PCL;

public sealed class WpfEventUi : IEventUi
{
    public bool RefreshPage()
    {
        if (Launcher.frmMain?.pageRight is not IRefreshable refreshable) return false;
        PlatformServices.UiDispatcher?.Invoke(() => refreshable.Refresh());
        return true;
    }

    public void ShowDailyFortune()
    {
        PageToolsTest.Jrrp();
    }

    public void ClearTrash()
    {
        PageToolsTest.RubbishClear();
    }

    public void SwitchPage(string pageType, string? subType)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            var page = (FormMain.PageType)Enum.Parse(typeof(FormMain.PageType), pageType, true);
            var sub = string.IsNullOrEmpty(subType)
                ? FormMain.PageSubType.Default
                : (FormMain.PageSubType)Enum.Parse(typeof(FormMain.PageSubType), subType, true);
            Launcher.frmMain?.PageChange(page, sub);
        });
    }

    public void StartModpackInstall()
    {
        Modpack.ModpackInstall();
    }

    public void StartCustomDownload(string url, string savePath, string? fileName)
    {
        PageToolsTest.StartCustomDownload(url, savePath, fileName);
    }

    public void OpenHelp(string location)
    {
        var content = PageHelpDetail.LoadContent(location);
        PlatformServices.UiDispatcher?.Post(() =>
        {
            var page = new PageHelpDetail(content);
            Launcher.frmMain?.PageChange(
                new FormMain.PageStackData
                {
                    page = FormMain.PageType.HelpDetail,
                    helpPage = page
                });
        });
    }
}
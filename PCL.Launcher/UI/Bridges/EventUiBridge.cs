using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages;
using PCL.Views.Pages.Tools;

namespace PCL.UI.Bridges;

/// <summary>自定义事件涉及的页面/工具操作。</summary>
public sealed class EventUiBridge : IEventUi
{
    public bool RefreshPage()
    {
        var refreshed = false;
        LauncherCommon.RunInUiWait(() =>
        {
            if (PageRouter.CurrentPage is not IRefreshable refreshable) return;
            refreshable.Refresh();
            refreshed = true;
        });
        return refreshed;
    }

    public void ShowDailyFortune()
    {
        PageToolsTest.ShowDailyFortune();
    }

    public void ClearTrash()
    {
        PageToolsTest.ClearTrash();
    }

    public void SwitchPage(string pageType, string? subType)
    {
        if (!PageCatalog.TryParsePage(pageType, out var page))
        {
            LogWrapper.Warn("UI", $"自定义事件切换页面失败：无法识别页面类型 {pageType}");
            return;
        }

        SubPageId? sub = null;
        if (!string.IsNullOrEmpty(subType)
            && !PageCatalog.TryParseSubPage(page, subType, out sub))
        {
            LogWrapper.Warn("UI", $"自定义事件切换页面失败：无法识别子页面类型 {subType}");
            return;
        }

        PageRouter.Navigate(page, sub);
    }

    public async void StartModpackInstall()
    {
        try
        {
            await Modpack.ModpackInstall();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "启动整合包安装失败");
        }
    }

    public void StartCustomDownload(string url, string savePath, string? fileName)
    {
        _ = PageToolsTest.StartCustomDownloadAsync(url, fileName, savePath);
    }

    public void OpenHelp(string location)
    {
        try
        {
            var title = PageHelpDetail.ReadTitle(location);
            LauncherCommon.RunInUiWait(() => PageRouter.Navigate(
                new PageRoute(
                    PageIds.HelpDetail,
                    null,
                    location,
                    string.IsNullOrEmpty(title) ? null : title)));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", location),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", location));
        }
    }
}
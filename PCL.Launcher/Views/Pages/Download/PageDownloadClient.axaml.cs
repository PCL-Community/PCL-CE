using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
///     下载页的 Minecraft 版本列表：按正式版、预览版、远古版、愚人节版分组展示，
///     最新版本置顶；点击版本行把版本文件另存到所选目录，行内悬停按钮提供下载服务端、
///     更新日志与进入安装页；已安装的版本带标记。
/// </summary>
public partial class PageDownloadClient : MyPageRight, IPageView, IRefreshable
{
    private readonly Dictionary<string, MyListItem> _versionItems =
        new(StringComparer.OrdinalIgnoreCase);

    private JsonArray? _versions;

    public PageDownloadClient()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Grid.SetIsSharedSizeScope(PanMain, true);
        PageLoaderInit(
            Load,
            PanLoad,
            PanBack,
            null,
            GameDownload.dlClientListLoader,
            _ => OnLoadFinished());
        Loaded += (_, _) => PanBack?.ScrollToHome();
        GameFolders.McFolderListLoader.PreviewFinish +=
            _ => Dispatcher.UIThread.Post(RefreshInstalledMarks);
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack?.ScrollToHome();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        GameDownload.dlClientListLoader.Start(null, true);
    }

    private void RefreshInstalledMarks()
    {
        if (_versions is null || _versionItems.Count == 0)
            return;
        try
        {
            var installed = DownloadVersionList.GetInstalledVersionNames();
            foreach (var node in _versions)
                if (node is JsonObject version
                    && version["id"]?.ToString() is { } versionId
                    && _versionItems.TryGetValue(versionId, out var item))
                    DownloadVersionList.ApplyVersionTags(
                        item,
                        version,
                        installed.Contains(versionId));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新版本已安装标记失败");
        }
    }

    private void OnLanguageChanged()
    {
        if (GameDownload.dlClientListLoader.State == LoadState.Finished)
            OnLoadFinished();
    }

    private void OnLoadFinished()
    {
        try
        {
            if (PanMain is null)
                return;
            if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions)
                return;
            _versions = versions;
            _versionItems.Clear();
            DownloadVersionList.Build(
                PanMain,
                versions,
                SelectVersion,
                DownloadVersionListMode.SaveOnly,
                _versionItems);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "可视化 Minecraft 版本列表出错");
        }
    }

    private static void SelectVersion(JsonObject version)
    {
        try
        {
            PageRouter.Navigate(PageType.Download, PageSubType.DownloadInstall, version);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开 Minecraft 安装页失败");
        }
    }
}
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
/// 下载页的 Minecraft 版本列表：按正式版、预览版、远古版、愚人节版分组展示， 最新版本置顶；点击版本行把版本文件另存到所选目录，行内悬停按钮提供下载服务端、
/// 更新日志与进入安装页；已安装的版本带标记。
/// </summary>
public partial class PageDownloadClient : MyPageRight, IPageView, IRefreshable
{
    public PageDownloadClient()
    {
        InitializeComponent();
        PanScroll = PanBack;
        VersionList.Initialize(DownloadVersionListMode.SaveOnly, null, false);
        VersionList.Activate();
        PageLoaderInit(
            Load,
            PanLoad,
            PanBack,
            null,
            GameDownload.dlClientListLoader);
        Loaded += (_, _) => PanBack?.ScrollToHome();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        VersionList.Activate();
        VersionList.Rebuild();
        PanBack?.ScrollToHome();
    }

    public override void OnNavigatedFrom()
    {
        VersionList.Deactivate();
    }

    public void Refresh()
    {
        VersionList.Refresh();
    }
}
using Avalonia;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Controls.Navigation;

namespace PCL.Views.Pages.Download;

/// <summary>下载页左栏：按页面描述生成「Minecraft 本体 / 社区资源 / 安装包」分组与子页。</summary>
public partial class PageDownloadLeft : MyPageLeft, IPageView, IRefreshable
{
    private static readonly PageDescriptor Page =
        PageCatalog.RequirePage(PageIds.Download);

    private readonly List<(SubPageId Sub, MyListItem Item)> _items = [];
    private SubPageId _current = SubPageIds.DownloadInstall;
    private bool _isSynchronizing;

    public PageDownloadLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        BuildItems();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        ClearCompCachesIfNeeded(_current);
        RefreshRequested?.Invoke(_current);
    }

    private static void ClearCompCachesIfNeeded(SubPageId subPage)
    {
        if (subPage != SubPageIds.DownloadMod
            && subPage != SubPageIds.DownloadPack
            && subPage != SubPageIds.DownloadDataPack
            && subPage != SubPageIds.DownloadResourcePack
            && subPage != SubPageIds.DownloadShader
            && subPage != SubPageIds.DownloadWorld)
            return;
        Comp.compProjectCache.Clear();
        Comp.compFilesCache.Clear();
    }

    public event Action<SubPageId>? PageSelected;

    public event Action<SubPageId>? RefreshRequested;

    /// <summary>再次点击已经选中的「Minecraft」项时触发，用于退出安装配置阶段。</summary>
    public event Action? InstallItemReselected;

    public void Select(SubPageId subPage)
    {
        _current = Page.NormalizeSubPage(subPage);
        _isSynchronizing = true;
        try
        {
            PageNavigationListBuilder.Select(_items, _current);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void BuildItems()
    {
        var result = PageNavigationListBuilder.Build(
            Page,
            PanItem,
            true,
            sub =>
            {
                if (_isSynchronizing)
                    return;
                if (sub == SubPageIds.DownloadInstall && _current == sub)
                    InstallItemReselected?.Invoke();
                _current = sub;
                PageSelected?.Invoke(sub);
            },
            onRefresh: OnRefreshClicked,
            categoryMargin: new Thickness(13d, 10d, 5d, 4d));
        _items.Clear();
        _items.AddRange(result.Items);

        if (result.Items.FirstOrDefault(pair => pair.Sub == SubPageIds.DownloadInstall).Item
            is { } install)
            install.Click += (_, _) =>
            {
                if (_current == SubPageIds.DownloadInstall && install.Checked)
                    InstallItemReselected?.Invoke();
            };
    }

    private void OnRefreshClicked(SubPageId subPage)
    {
        try
        {
            if (subPage != _current)
                PageRouter.Navigate(PageIds.Download, subPage);
            RefreshRequested?.Invoke(subPage);
            HintService.Show(Hints.Info(Lang.Text("Download.Left.Hint.Refreshing"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新下载子页失败");
        }
    }
}
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>
///     下载页左栏：按「Minecraft 本体 / 社区资源 / 安装包」三组列出全部下载子页，
///     选中项与当前路由同步；行尾的刷新按钮只刷新对应子页，不改变当前页面。
/// </summary>
public partial class PageDownloadLeft : MyPageLeft, IPageView, IRefreshable
{
    private readonly List<(PageSubType Sub, MyListItem Item)> _items = [];
    private PageSubType _current = PageSubType.DownloadInstall;
    private bool _isSynchronizing;

    public PageDownloadLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;

        _items.Add((PageSubType.DownloadInstall, ItemInstall));
        _items.Add((PageSubType.DownloadMod, ItemMod));
        _items.Add((PageSubType.DownloadPack, ItemPack));
        _items.Add((PageSubType.DownloadDataPack, ItemDataPack));
        _items.Add((PageSubType.DownloadResourcePack, ItemResourcePack));
        _items.Add((PageSubType.DownloadShader, ItemShader));
        _items.Add((PageSubType.DownloadWorld, ItemWorld));
        _items.Add((PageSubType.DownloadCompFavorites, ItemFavorites));
        _items.Add((PageSubType.DownloadClient, ItemClient));
        _items.Add((PageSubType.DownloadOptiFine, ItemOptiFine));
        _items.Add((PageSubType.DownloadForge, ItemForge));
        _items.Add((PageSubType.DownloadNeoForge, ItemNeoForge));
        _items.Add((PageSubType.DownloadCleanroom, ItemCleanroom));
        _items.Add((PageSubType.DownloadFabric, ItemFabric));
        _items.Add((PageSubType.DownloadLegacyFabric, ItemLegacyFabric));
        _items.Add((PageSubType.DownloadLabyMod, ItemLabyMod));
        _items.Add((PageSubType.DownloadLiteLoader, ItemLiteLoader));

        foreach (var (sub, item) in _items)
        {
            item.Check += (_, _) => OnItemChecked(sub, item);
            item.Buttons = [CreateRefreshButton(sub)];
        }

        ItemInstall.Click += (_, _) =>
        {
            if (_current == PageSubType.DownloadInstall && ItemInstall.Checked)
                InstallItemReselected?.Invoke();
        };
    }

    public override void OnNavigatedTo(object? parameter)
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

    private static void ClearCompCachesIfNeeded(PageSubType sub)
    {
        if (sub is not (PageSubType.DownloadMod
            or PageSubType.DownloadPack
            or PageSubType.DownloadDataPack
            or PageSubType.DownloadResourcePack
            or PageSubType.DownloadShader
            or PageSubType.DownloadWorld))
            return;
        Comp.compProjectCache.Clear();
        Comp.compFilesCache.Clear();
    }

    protected override void RefreshLocalization()
    {
        foreach (var (_, item) in _items)
        {
            if (item.Buttons?.FirstOrDefault() is not { } button)
                continue;
            ToolTip.SetTip(button, Lang.Text("Common.Action.Refresh"));
        }
    }

    public event Action<PageSubType>? PageSelected;

    public event Action<PageSubType>? RefreshRequested;

    /// <summary>再次点击已经选中的「Minecraft」项时触发，用于退出安装配置阶段。</summary>
    public event Action? InstallItemReselected;

    public void Select(PageSubType sub)
    {
        _current = sub;
        _isSynchronizing = true;
        try
        {
            foreach (var (itemSub, item) in _items)
                item.SetChecked(itemSub == sub, false, false);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private void OnItemChecked(PageSubType sub, MyListItem item)
    {
        if (_isSynchronizing || !item.Checked)
            return;
        _current = sub;
        PageSelected?.Invoke(sub);
    }

    private MyIconButton CreateRefreshButton(PageSubType sub)
    {
        var button = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 0.85,
            Theme = MyIconButton.Themes.Color
        };
        ToolTip.SetTip(button, Lang.Text("Common.Action.Refresh"));
        ToolTip.SetPlacement(button, PlacementMode.Right);
        ToolTip.SetShowDelay(button, 200);
        ToolTip.SetVerticalOffset(button, -1d);
        button.Click += (_, _) => OnRefreshClicked(sub);
        return button;
    }

    private void OnRefreshClicked(PageSubType sub)
    {
        try
        {
            if (sub != _current)
                PageRouter.Navigate(PageType.Download, sub);
            RefreshRequested?.Invoke(sub);
            HintService.Show(Hints.Info(Lang.Text("Download.Left.Hint.Refreshing"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新下载子页失败");
        }
    }
}
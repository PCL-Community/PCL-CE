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
    }

    public override void OnNavigatedTo(object? parameter)
    {
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        RefreshRequested?.Invoke(_current);
    }

    public event Action<PageSubType>? PageSelected;

    public event Action<PageSubType>? RefreshRequested;

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
            Theme = MyIconButton.Themes.Custom
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
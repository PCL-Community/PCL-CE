using Avalonia.Controls;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档管理左栏：在「存档信息」与「数据包」两个子页之间切换，并提供打开存档文件夹入口。
///     数据包子页仅在存档数据版本支持数据包（17w47a 及以上）时显示；当前存档路径由
///     <see cref="CurrentSave" /> 记录，右栏详情页与数据包页从路由参数或该字段读取。
/// </summary>
public partial class PageInstanceSavesLeft : MyPageLeft, IRefreshable
{
    private readonly List<(PageSubType SubType, MyListItem Item)> _items = [];
    private bool _building;
    private MyIconButton? _datapackRefresh;

    public PageInstanceSavesLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        DataContext = ViewModel;

        ItemInfo.Tag = PageSubType.VersionSavesInfo;
        ItemDatapack.Tag = PageSubType.VersionSavesDatapack;
        _items.Add((PageSubType.VersionSavesInfo, ItemInfo));
        _items.Add((PageSubType.VersionSavesDatapack, ItemDatapack));

        ItemInfo.Check += (_, _) => NavigateIfNeeded(ItemInfo);
        ItemDatapack.Check += (_, _) => NavigateIfNeeded(ItemDatapack);
        BtnOpenFolder.Click += (_, _) => PageInstanceSavesLeftViewModel.OpenFolder();
        AttachDatapackButton();

        Current = this;
        ApplyDatapackVisibility();
        RefreshChecked();
    }

    public static string CurrentSave
    {
        get => PageInstanceSavesLeftViewModel.CurrentSave;
        set => PageInstanceSavesLeftViewModel.CurrentSave = value;
    }

    public static bool ShowDatapackTab
    {
        get => PageInstanceSavesLeftViewModel.ShowDatapackTab;
        set
        {
            PageInstanceSavesLeftViewModel.ShowDatapackTab = value;
            Current?.ApplyDatapackVisibility();
        }
    }

    public static PageInstanceSavesLeft? Current { get; private set; }

    public PageInstanceSavesLeftViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        PageInstanceSavesLeftViewModel.Refresh();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        Current = this;
        ViewModel.Attach();
        PageInstanceSavesLeftViewModel.UpdateNavigationParameter(parameter);
        ApplyDatapackVisibility();
        RefreshChecked();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (_datapackRefresh is not null)
            ToolTip.SetTip(_datapackRefresh, PageInstanceSavesLeftViewModel.RefreshToolTip);
        ApplyDatapackVisibility();
        RefreshChecked();
    }

    private void AttachDatapackButton()
    {
        _datapackRefresh = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 0.85d
        };
        ToolTip.SetTip(_datapackRefresh, PageInstanceSavesLeftViewModel.RefreshToolTip);
        _datapackRefresh.Click += (_, _) => PageInstanceSavesLeftViewModel.Refresh();
        ItemDatapack.Buttons = [_datapackRefresh];
    }

    private void NavigateIfNeeded(MyListItem item)
    {
        if (!_building && item.Tag is PageSubType target)
            PageInstanceSavesLeftViewModel.Navigate(target);
    }

    private void ApplyDatapackVisibility()
    {
        ItemDatapack.IsVisible = PageInstanceSavesLeftViewModel.IsDatapackTabVisible;
        PageInstanceSavesLeftViewModel.ApplyDatapackVisibility();
    }

    public void RefreshChecked()
    {
        _building = true;
        try
        {
            var current = PageInstanceSavesLeftViewModel.ResolveCurrentSubPage();
            foreach (var (subType, item) in _items)
                item.Checked = subType == current;
        }
        finally
        {
            _building = false;
        }
    }
}
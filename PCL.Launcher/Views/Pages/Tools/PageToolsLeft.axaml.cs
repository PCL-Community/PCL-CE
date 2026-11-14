using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Tools;

/// <summary>
///     联机与工具页左栏：联机大厅 / 测试工具两项，选中项与当前路由同步。
///     功能隐藏配置为真的行不显示；两项都被隐藏时分组标题一并隐藏。
/// </summary>
public partial class PageToolsLeft : MyPageLeft
{
    private readonly MyIconButton _btnRefresh;
    private readonly List<(PageSubType Sub, MyListItem Item)> _items = [];
    private bool _hiddenStateSubscribed;
    private bool _isSynchronizing;

    public PageToolsLeft()
    {
        InitializeComponent();
        DataContext = ViewModel;
        AnimatedControl = PanItem;

        _items.Add((PageSubType.ToolsGameLink, ItemGameLink));
        _items.Add((PageSubType.ToolsTest, ItemTest));

        foreach (var (sub, item) in _items)
            item.Check += (_, _) => OnItemChecked(sub, item);

        _btnRefresh = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 0.85
        };
        ToolTip.SetTip(_btnRefresh, Lang.Text("Common.Action.Refresh"));
        ToolTip.SetPlacement(_btnRefresh, PlacementMode.Right);
        _btnRefresh.Click += (_, _) => ViewModel.Refresh();
        ItemGameLink.Buttons = [_btnRefresh];

        ViewModel.RefreshRequested += OnRefreshRequested;

        AttachedToVisualTree += (_, _) =>
        {
            if (!_hiddenStateSubscribed)
            {
                HiddenFeatureState.Changed += ApplyHiddenState;
                _hiddenStateSubscribed = true;
            }

            ApplyHiddenState();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (!_hiddenStateSubscribed)
                return;
            HiddenFeatureState.Changed -= ApplyHiddenState;
            _hiddenStateSubscribed = false;
        };
    }

    public PageToolsLeftViewModel ViewModel { get; } = new();

    protected override void RefreshLocalization()
    {
        ToolTip.SetTip(_btnRefresh, Lang.Text("Common.Action.Refresh"));
        ApplyHiddenState();
    }

    public event Action<PageSubType>? PageSelected;

    public override void OnNavigatedTo(object? parameter)
    {
        ApplyHiddenState();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Select(PageSubType sub)
    {
        ViewModel.Select(sub);
        _isSynchronizing = true;
        try
        {
            foreach (var (itemSub, item) in _items)
                item.SetChecked(itemSub == ViewModel.SelectedPage, false, false);
        }
        finally
        {
            _isSynchronizing = false;
        }

        ApplyHiddenState();
    }

    public void ApplyHiddenState()
    {
        try
        {
            ViewModel.UpdateVisibility();
            ItemGameLink.IsVisible = ViewModel.GameLinkVisible;
            ItemTest.IsVisible = ViewModel.TestVisible;
            TextMultiplayerCategory.IsVisible = ViewModel.MultiplayerCategoryVisible;
            TextUtilitiesCategory.IsVisible = ViewModel.UtilitiesCategoryVisible;
        }
        catch (Exception)
        {
        }
    }

    public static PageSubType FirstVisible()
    {
        return PageToolsLeftViewModel.FirstVisible();
    }

    public static bool IsSubPageHidden(PageSubType sub)
    {
        return PageToolsLeftViewModel.IsSubPageHidden(sub);
    }

    private void OnRefreshRequested()
    {
        Select(ViewModel.SelectedPage);
        PageSelected?.Invoke(ViewModel.SelectedPage);
        PageToolsGameLink.Current?.Reload();
    }

    private void OnItemChecked(PageSubType sub, MyListItem item)
    {
        if (_isSynchronizing || !item.Checked)
            return;
        ViewModel.Select(sub);
        PageSelected?.Invoke(sub);
    }
}
using Avalonia.Controls;
using PCL.Core.App;
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
    private readonly List<(TextBlock Title, Func<bool> IsCategoryHidden)> _categories = [];
    private readonly List<(PageSubType Sub, MyListItem Item, Func<bool> IsHidden)> _items = [];
    private bool _isSynchronizing;

    private readonly MyIconButton _btnRefresh;

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;

        var hide = Config.Preference.Hide;
        _items.Add((PageSubType.ToolsGameLink, ItemGameLink, () => hide.ToolsGameLink));
        _items.Add((PageSubType.ToolsTest, ItemTest, () => hide.ToolsTest));

        _categories.Add((TextMultiplayerCategory, () => AllHidden(PageSubType.ToolsGameLink)));
        _categories.Add((TextUtilitiesCategory, () => AllHidden(PageSubType.ToolsTest)));

        foreach (var (sub, item, _) in _items)
            item.Check += (_, _) => OnItemChecked(sub, item);

        _btnRefresh = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 0.85
        };
        ToolTip.SetTip(_btnRefresh, Lang.Text("Common.Action.Refresh"));
        _btnRefresh.Click += (_, _) =>
        {
            Select(PageSubType.ToolsGameLink);
            PageSelected?.Invoke(PageSubType.ToolsGameLink);
            PageToolsGameLink.Current?.Reload();
        };
        ItemGameLink.Buttons = [_btnRefresh];

        HiddenFeatureState.Changed += ApplyHiddenState;
        Loaded += (_, _) => ApplyHiddenState();
    }

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
        _isSynchronizing = true;
        try
        {
            foreach (var (itemSub, item, _) in _items)
                item.SetChecked(itemSub == sub, false, false);
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
            foreach (var (sub, item, _) in _items)
                item.IsVisible = !IsSubPageHidden(sub);
            foreach (var (title, isCategoryHidden) in _categories)
                title.IsVisible = !isCategoryHidden();
        }
        catch (Exception)
        {
        }
    }

    public static PageSubType FirstVisible()
    {
        if (!IsSubPageHidden(PageSubType.ToolsGameLink)) return PageSubType.ToolsGameLink;
        if (!IsSubPageHidden(PageSubType.ToolsTest)) return PageSubType.ToolsTest;
        return PageSubType.ToolsGameLink;
    }

    public static bool IsSubPageHidden(PageSubType sub)
    {
        if (HiddenFeatureState.ForceShow)
            return false;
        var hide = Config.Preference.Hide;
        return sub switch
        {
            PageSubType.ToolsGameLink => hide.ToolsGameLink,
            PageSubType.ToolsTest => hide.ToolsTest,
            _ => false
        };
    }

    private void OnItemChecked(PageSubType sub, MyListItem item)
    {
        if (_isSynchronizing || !item.Checked)
            return;
        PageSelected?.Invoke(sub);
    }

    private static bool AllHidden(params PageSubType[] subs)
    {
        return subs.All(IsSubPageHidden);
    }
}
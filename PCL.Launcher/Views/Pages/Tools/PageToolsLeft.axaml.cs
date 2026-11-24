using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Controls.Navigation;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Tools;

/// <summary>联机与工具页左栏：由页面描述生成联机大厅 / 测试工具导航项。</summary>
public partial class PageToolsLeft : MyPageLeft
{
    private static readonly PageDescriptor Page = PageCatalog.RequirePage(PageIds.Tools);
    private readonly List<(SubPageId Sub, MyListItem Item)> _items = [];
    private bool _isSynchronizing;

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        BuildItems();
        AttachedToVisualTree += (_, _) =>
        {
            HiddenFeatureState.Changed += OnHiddenStateChanged;
            ApplyHiddenState();
        };
        DetachedFromVisualTree += (_, _) => HiddenFeatureState.Changed -= OnHiddenStateChanged;
    }

    public PageToolsLeftViewModel ViewModel { get; } = new();

    public event Action<SubPageId>? PageSelected;

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        ApplyHiddenState();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Select(SubPageId subPage)
    {
        ViewModel.Select(Page.NormalizeSubPage(subPage));
        _isSynchronizing = true;
        try
        {
            PageNavigationListBuilder.Select(_items, ViewModel.SelectedPage);
        }
        finally
        {
            _isSynchronizing = false;
        }

        ApplyHiddenState();
    }

    private void OnHiddenStateChanged()
    {
        BuildItems();
        Select(ViewModel.SelectedPage);
        ApplyHiddenState();
    }

    public void ApplyHiddenState()
    {
        var visible = Page
            .VisibleSubPages(HiddenFeatureState.ForceShow)
            .Select(item => item.Id)
            .ToHashSet();
        foreach (var (sub, item) in _items)
            item.IsVisible = visible.Contains(sub);
    }

    public static SubPageId FirstVisible()
    {
        return Page.FirstVisibleSubPage();
    }

    public static bool IsSubPageHidden(SubPageId subPage)
    {
        return Page.VisibleSubPages().All(item => item.Id != subPage);
    }

    private void BuildItems()
    {
        var result = PageNavigationListBuilder.Build(
            Page,
            PanItem,
            HiddenFeatureState.ForceShow,
            sub =>
            {
                if (_isSynchronizing) return;
                ViewModel.Select(sub);
                PageSelected?.Invoke(sub);
            },
            onRefresh: _ =>
            {
                Select(SubPageIds.ToolsGameLink);
                PageSelected?.Invoke(SubPageIds.ToolsGameLink);
                PageToolsGameLink.Current?.Reload();
            });
        _items.Clear();
        _items.AddRange(result.Items);
    }
}
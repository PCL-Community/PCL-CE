using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.Controls.Navigation;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Setup;

public partial class PageSetupLeft : MyPageLeft
{
    private static readonly PageDescriptor Page = PageCatalog.RequirePage(PageIds.Setup);
    private readonly List<(TextBlock Title, string GroupKey)> _groups = [];
    private readonly List<(SubPageId Sub, MyListItem Item)> _items = [];
    private readonly PageSetupLeftViewModel _viewModel;
    private bool _isSynchronizing;
    private Func<SubPageId, Control?>? _pageProvider;

    public PageSetupLeft(PageSetupLeftViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        AnimatedControl = PanItem;

        BuildItems();
        HiddenSettingChanged += OnHiddenStateChanged;
        HiddenFeatureState.Changed += OnHiddenStateChanged;
        Loaded += (_, _) => ApplyHiddenState();
    }

    public event Action<SubPageId>? PageSelected;

    public static event Action? HiddenSettingChanged;

    public void AttachPageProvider(Func<SubPageId, Control?> provider)
    {
        _pageProvider = provider;
        BuildItems();
        ApplyHiddenState();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        ApplyHiddenState();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Select(SubPageId subPage)
    {
        _viewModel.PageId = subPage;
        _isSynchronizing = true;
        try
        {
            foreach (var (itemSub, item) in _items)
                item.SetChecked(itemSub == subPage, false, false);
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
            var forceShow = HiddenFeatureState.ForceShow;
            foreach (var (_, item) in _items)
                item.IsVisible = true;
            foreach (var (title, _) in _groups)
                title.IsVisible = true;
            PanItem.IsVisible = forceShow || Page.VisibleSubPageCount() >= 2;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public static SubPageId FirstVisible()
    {
        return Page.FirstVisibleSubPage();
    }

    public static bool IsSubPageHidden(SubPageId subPage)
    {
        if (HiddenFeatureState.ForceShow)
            return false;
        var descriptor = Page.FindSubPage(subPage);
        return descriptor is not null && !PageDescriptor.IsVisible(descriptor);
    }

    public static void NotifyHiddenSettingChanged()
    {
        HiddenSettingChanged?.Invoke();
    }

    private void BuildItems()
    {
        var result = PageNavigationListBuilder.Build(
            Page,
            PanItem,
            HiddenFeatureState.ForceShow,
            subPage =>
            {
                if (!_isSynchronizing)
                    PageSelected?.Invoke(subPage);
            },
            _pageProvider,
            RefreshPage,
            resetSub => _ = ResetPageAsync(resetSub));
        _items.Clear();
        _items.AddRange(result.Items);
        _groups.Clear();
        _groups.AddRange(result.Groups);
        ApplyLocalizedText();
    }

    /// <summary>导航列表由公共构建器一次性生成文本；这里补充本地化绑定，使语言切换时无需重建列表。</summary>
    private void ApplyLocalizedText()
    {
        foreach (var (title, groupKey) in _groups)
        {
            var group = Page.Groups.FirstOrDefault(item =>
                string.Equals(item.Key, groupKey, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(group?.TitleKey))
                title.SetLocalizedValue(TextBlock.TextProperty, group.TitleKey);
        }

        foreach (var (subPage, item) in _items)
        {
            var descriptor = Page.FindSubPage(subPage);
            if (descriptor is null) continue;
            item.SetLocalizedValue(MyListItem.TitleProperty, descriptor.TitleKey);
            if (item.Buttons is null) continue;
            foreach (var button in item.Buttons)
                switch (button.SvgIcon)
                {
                    case "lucide/rotate-ccw":
                        button.SetLocalizedValue(
                            ToolTip.TipProperty,
                            "Common.Action.Initialize");
                        break;
                    case "lucide/refresh-cw":
                        button.SetLocalizedValue(
                            ToolTip.TipProperty,
                            "Common.Action.Refresh");
                        break;
                }
        }
    }

    private void OnHiddenStateChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnHiddenStateChanged);
            return;
        }

        BuildItems();
        ApplyHiddenState();
    }

    private void RefreshPage(SubPageId subPage)
    {
        try
        {
            var page = _pageProvider?.Invoke(subPage);
            if (page is IRefreshable refreshable)
                refreshable.Refresh();
            PageSelected?.Invoke(subPage);
            HintService.Show(Hints.Info(Lang.Text("Setup.Left.Refreshing"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "刷新设置子页面失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private async Task ResetPageAsync(SubPageId subPage)
    {
        if (!await _viewModel.ConfirmResetAsync(subPage)) return;

        var page = _pageProvider?.Invoke(subPage);
        if (page is ISetupResetPage resettable)
            resettable.Reset();
        PageSelected?.Invoke(subPage);
    }
}
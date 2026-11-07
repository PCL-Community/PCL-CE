using Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Setup;

public partial class PageSetupLeft : MyPageLeft
{
    private readonly List<(TextBlock Title, Func<bool> IsCategoryHidden)> _categories = [];
    private readonly List<(PageSubType Sub, MyListItem Item, Func<bool> IsHidden)> _items = [];
    private readonly PageSetupLeftViewModel _viewModel = new();
    private bool _isSynchronizing;
    private Func<PageSubType, Control?>? _pageProvider;

    public PageSetupLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;

        var hide = Config.Preference.Hide;
        _items.Add((PageSubType.SetupLaunch, ItemLaunch, () => hide.SetupLaunch));
        _items.Add((PageSubType.SetupJava, ItemJava, () => hide.SetupJava));
        _items.Add((PageSubType.SetupGameManage, ItemGameManage, () => hide.SetupGameManage));
        _items.Add((PageSubType.SetupGameLink, ItemGameLink, () => hide.SetupGameLink));
        _items.Add((PageSubType.SetupUi, ItemUI, () => hide.SetupUi));
        _items.Add(
            (PageSubType.SetupLauncherLanguage, ItemLauncherLanguage,
                () => hide.SetupLauncherLanguage));
        _items.Add((PageSubType.SetupLauncherMisc, ItemLauncherMisc, () => hide.SetupLauncherMisc));
        _items.Add((PageSubType.SetupAbout, ItemAbout, () => hide.SetupAbout));
        _items.Add((PageSubType.SetupUpdate, ItemUpdate, () => hide.SetupUpdate));
        _items.Add((PageSubType.SetupFeedback, ItemFeedback, () => hide.SetupFeedback));
        _items.Add((PageSubType.SetupLog, ItemLog, () => hide.SetupLog));

        _categories.Add(
            (TextGameCategory,
                () => AllHidden(
                    PageSubType.SetupLaunch,
                    PageSubType.SetupJava,
                    PageSubType.SetupGameManage)));
        _categories.Add((TextToolsCategory, () => AllHidden(PageSubType.SetupGameLink)));
        _categories.Add(
            (TextLauncherCategory,
                () => AllHidden(
                    PageSubType.SetupUi,
                    PageSubType.SetupLauncherLanguage,
                    PageSubType.SetupLauncherMisc)));
        _categories.Add(
            (TextAboutCategory,
                () => AllHidden(
                    PageSubType.SetupAbout,
                    PageSubType.SetupUpdate,
                    PageSubType.SetupFeedback,
                    PageSubType.SetupLog)));

        foreach (var (sub, item, _) in _items)
            item.Check += (_, _) => OnItemChecked(sub, item);

        AttachRowButtons();

        HiddenSettingChanged += ApplyHiddenState;
        HiddenFeatureState.Changed += ApplyHiddenState;
        Loaded += (_, _) => ApplyHiddenState();
    }

    public event Action<PageSubType>? PageSelected;

    public static event Action? HiddenSettingChanged;

    public void AttachPageProvider(Func<PageSubType, Control?> provider)
    {
        _pageProvider = provider;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        ApplyHiddenState();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Select(PageSubType sub)
    {
        _viewModel.PageId = sub;
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
            var forceShow = HiddenFeatureState.ForceShow;
            foreach (var (_, item, isHidden) in _items)
                item.IsVisible = forceShow || !isHidden();
            foreach (var (title, isCategoryHidden) in _categories)
                title.IsVisible = !isCategoryHidden();
            PanItem.IsVisible =
                HiddenFeatureState.ForceShow
                || PageSetupLeftViewModel.CountVisiblePages(false) >= 2;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    protected override void RefreshLocalization()
    {
        AttachRowButtons();
        ApplyHiddenState();
    }

    public static PageSubType FirstVisible()
    {
        return PageSetupLeftViewModel.FirstVisible();
    }

    public static bool IsSubPageHidden(PageSubType sub)
    {
        return !HiddenFeatureState.ForceShow && PageSetupLeftViewModel.IsSubPageHidden(sub);
    }

    public static void NotifyHiddenSettingChanged()
    {
        HiddenSettingChanged?.Invoke();
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

    #region 行尾按钮

    private void AttachRowButtons()
    {
        ItemLaunch.Buttons =
        [
            CreateResetButton(PageSubType.SetupLaunch)
        ];
        ItemGameManage.Buttons =
        [
            CreateResetButton(PageSubType.SetupGameManage)
        ];
        ItemGameLink.Buttons =
        [
            CreateResetButton(PageSubType.SetupGameLink)
        ];
        ItemUI.Buttons =
        [
            CreateResetButton(PageSubType.SetupUi)
        ];
        ItemLauncherLanguage.Buttons =
        [
            CreateResetButton(PageSubType.SetupLauncherLanguage)
        ];
        ItemLauncherMisc.Buttons =
        [
            CreateResetButton(PageSubType.SetupLauncherMisc)
        ];
        ItemJava.Buttons =
        [
            CreateRefreshButton(PageSubType.SetupJava)
        ];
        ItemFeedback.Buttons =
        [
            CreateRefreshButton(PageSubType.SetupFeedback)
        ];
    }

    private MyIconButton CreateResetButton(PageSubType sub)
    {
        var button = new MyIconButton
        {
            Height = 25,
            Width = 25,
            LogoScale = 0.9,
            SvgIcon = "lucide/rotate-ccw"
        };
        ToolTip.SetTip(button, Lang.Text("Common.Action.Initialize"));
        ToolTip.SetPlacement(button, PlacementMode.Right);
        ToolTip.SetShowDelay(button, 200);
        ToolTip.SetVerticalOffset(button, -1);
        button.Click += async (_, _) => await ResetPageAsync(sub, button);
        return button;
    }

    private MyIconButton CreateRefreshButton(PageSubType sub)
    {
        var button = new MyIconButton
        {
            Height = 25,
            Width = 25,
            LogoScale = 0.85,
            SvgIcon = "lucide/refresh-cw"
        };
        ToolTip.SetTip(button, Lang.Text("Common.Action.Refresh"));
        ToolTip.SetPlacement(button, PlacementMode.Right);
        ToolTip.SetShowDelay(button, 200);
        ToolTip.SetVerticalOffset(button, -1);
        button.Click += (_, _) => RefreshPage(sub);
        return button;
    }

    private async Task ResetPageAsync(PageSubType sub, MyIconButton button)
    {
        button.IsEnabled = false;
        try
        {
            if (!await _viewModel.ConfirmResetAsync(sub)) return;

            var page = _pageProvider?.Invoke(sub);
            if (page is ISetupResetPage resettable)
                resettable.Reset();
            PageSelected?.Invoke(sub);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "重置设置子页面失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void RefreshPage(PageSubType sub)
    {
        try
        {
            var page = _pageProvider?.Invoke(sub);
            if (page is IRefreshable refreshable)
                refreshable.Refresh();
            PageSelected?.Invoke(sub);
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

    #endregion
}
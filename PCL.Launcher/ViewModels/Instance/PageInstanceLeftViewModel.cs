using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Instance;
using PCL.Views.Shell;

namespace PCL;

/// <summary>实例左侧栏页的视图模型：维护可见项、分类标题、刷新状态与当前实例。</summary>
public partial class PageInstanceLeftViewModel : ViewModelBase
{
    private static readonly PageDescriptor InstancePage =
        PageCatalog.RequirePage(PageIds.InstanceSetup);

    [ObservableProperty] public partial bool ItemModVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemModDisabledVisible { get; set; } = true;

    [ObservableProperty] public partial bool TextResourceVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemInstallVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemExportVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemWorldVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemScreenshotVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemResourcePackVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemShaderVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemSchematicVisible { get; set; } = true;

    [ObservableProperty] public partial bool ItemServerVisible { get; set; } = true;

    /// <summary>当前页面实例；由页面构造与导航生命周期维护。</summary>
    public static PageInstanceLeftViewModel? Current { get; private set; }

    /// <summary>当前实例；页面导航与实例重载后同步到这里。</summary>
    public static McInstance? CurrentInstance { get; private set; }

    public bool IsRefreshed { get; private set; }

    public bool IsResetting { get; private set; }

    public static SubPageId CurrentSubPage =>
        PageRouter.Current.Page == PageIds.InstanceSetup
            ? PageRouter.Current.SubPage ?? SubPageIds.VersionOverall
            : SubPageIds.VersionOverall;

    public static void SetCurrentInstance(McInstance? instance)
    {
        CurrentInstance = instance;
    }

    public static McInstance? ResolveInstance()
    {
        return PageRouter.Current.Page == PageIds.InstanceSetup
            && PageRouter.Current.Parameter is McInstance routed
                ? routed
                : CurrentInstance ?? InstanceList.McMcInstanceSelected;
    }

    /// <summary>页面构造时登记当前视图模型。</summary>
    public void Attach()
    {
        Current = this;
    }

    /// <summary>页面离开可视树后允许下一次导航重新构建。</summary>
    public void InvalidateRefresh()
    {
        IsRefreshed = false;
    }

    /// <summary>处理导航参数并决定是否需要重建左栏。</summary>
    public bool NavigateTo(PageParameter? parameter)
    {
        if (parameter is McInstance instance)
        {
            if (!ReferenceEquals(CurrentInstance, instance))
                IsRefreshed = false;
            CurrentInstance = instance;
        }
        else if (CurrentInstance is null)
        {
            CurrentInstance = InstanceList.McMcInstanceSelected
                ?? InstanceList.mcInstanceList.SelectMany(card => card.Value).FirstOrDefault();
        }

        return TryBeginRefresh();
    }

    /// <summary>首次导航或显式刷新时返回 true；重复导航返回 false。</summary>
    public bool TryBeginRefresh()
    {
        if (IsRefreshed)
            return false;
        IsRefreshed = true;
        return true;
    }

    /// <summary>实例分类或 Mod 可用性变化后使左栏失效。</summary>
    public void RequestRefresh()
    {
        IsRefreshed = false;
    }

    /// <summary>按实例能力与隐藏开关计算所有条目的可见性。</summary>
    public void UpdateVisibility(McInstance? instance)
    {
        var hide = Config.Preference.Hide;

        if (instance?.Modable == true)
        {
            ItemModVisible = HiddenFeatureState.ForceShow || !hide.InstanceMod;
            ItemModDisabledVisible = false;
        }
        else
        {
            ItemModVisible = false;
            ItemModDisabledVisible = HiddenFeatureState.ForceShow || !hide.InstanceMod;
        }

        if (HiddenFeatureState.ForceShow)
        {
            TextResourceVisible = true;
        }
        else
        {
            var disableCount = 0;
            if (hide.InstanceSave) disableCount += 1;
            if (hide.InstanceScreenshot) disableCount += 1;
            if (hide.InstanceMod) disableCount += 1;
            if (hide.InstanceResourcePack) disableCount += 1;
            if (hide.InstanceShader) disableCount += 1;
            if (hide.InstanceSchematic) disableCount += 1;
            if (hide.InstanceServer) disableCount += 1;
            TextResourceVisible = disableCount != 7;
        }

        ItemInstallVisible = HiddenFeatureState.ForceShow || !hide.InstanceEdit;
        ItemExportVisible = HiddenFeatureState.ForceShow || !hide.InstanceExport;
        ItemWorldVisible = HiddenFeatureState.ForceShow || !hide.InstanceSave;
        ItemScreenshotVisible = HiddenFeatureState.ForceShow || !hide.InstanceScreenshot;
        ItemResourcePackVisible = HiddenFeatureState.ForceShow || !hide.InstanceResourcePack;
        ItemShaderVisible = HiddenFeatureState.ForceShow || !hide.InstanceShader;
        ItemSchematicVisible = HiddenFeatureState.ForceShow || !hide.InstanceSchematic;
        ItemServerVisible = HiddenFeatureState.ForceShow || !hide.InstanceServer;
    }

    /// <summary>构建左栏条目树；分类标题与条目按当前可见性过滤。</summary>
    public IReadOnlyList<PageInstanceLeftEntry> BuildEntries()
    {
        var instance = ResolveInstance();
        UpdateVisibility(instance);

        var result = new List<PageInstanceLeftEntry>
        {
            Section("game"),
            Item(SubPageIds.VersionOverall),
            Item(SubPageIds.VersionSetup, reset: true),
            Item(SubPageIds.VersionInstall, ItemInstallVisible, refresh: true),
            Item(SubPageIds.VersionExport, ItemExportVisible, refresh: true)
        };

        if (TextResourceVisible)
            result.Add(Section("resources"));

        if (ItemWorldVisible)
            result.Add(Item(SubPageIds.VersionWorld, refresh: true));
        if (ItemScreenshotVisible)
            result.Add(Item(SubPageIds.VersionScreenshot, refresh: true));

        if (ItemModVisible)
            result.Add(Item(SubPageIds.VersionMod, refresh: true));
        else if (ItemModDisabledVisible)
            result.Add(Item(SubPageIds.VersionModDisabled));

        if (ItemResourcePackVisible)
            result.Add(Item(SubPageIds.VersionResourcePack, refresh: true));
        if (ItemShaderVisible)
            result.Add(Item(SubPageIds.VersionShader, refresh: true));
        if (ItemSchematicVisible)
            result.Add(Item(SubPageIds.VersionSchematic, refresh: true));
        if (ItemServerVisible)
            result.Add(Item(SubPageIds.VersionServer, refresh: true));

        return result;
    }

    private static PageInstanceLeftEntry Section(string groupKey)
    {
        var group = InstancePage.Groups.FirstOrDefault(item => item.Key == groupKey);
        return PageInstanceLeftEntry.CreateSection(group?.TitleKey ?? string.Empty);
    }

    private static PageInstanceLeftEntry Item(
        SubPageId subPage,
        bool visible = true,
        bool reset = false,
        bool refresh = false)
    {
        var descriptor = InstancePage.FindSubPage(subPage);
        if (descriptor is null)
            throw new ArgumentOutOfRangeException(nameof(subPage), subPage, null);

        return PageInstanceLeftEntry.CreateItem(
            descriptor.Id,
            descriptor.TitleKey,
            descriptor.IconKey ?? string.Empty,
            descriptor.LogoScale,
            reset,
            visible,
            refresh ? descriptor.Id : null);
    }

    public static bool IsCurrentPage(SubPageId subPage)
    {
        return subPage == CurrentSubPage;
    }

    public static void ChangePage(SubPageId subPage)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        if (PageRouter.Current.Page == PageIds.InstanceSetup
            && PageRouter.Current.SubPage == subPage
            && PageRouter.Current.Parameter is McInstance routed
            && ReferenceEquals(routed, instance))
            return;
        PageRouter.Navigate(new PageRoute(PageIds.InstanceSetup, subPage, instance));
    }

    public static void RefreshSubPage(SubPageId subPage)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        PageRouter.Navigate(new PageRoute(PageIds.InstanceSetup, subPage, instance));
        if (PageRouter.CurrentPage is IRefreshable refreshable)
            refreshable.Refresh();
    }

    /// <summary>确认并重置实例设置；返回是否实际执行了重置。</summary>
    public async Task<bool> ResetInstanceSettingsAsync()
    {
        if (IsResetting)
            return false;
        IsResetting = true;
        try
        {
            var dialog = await Dialog.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle"),
                    Content = Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (dialog.Button != 1)
                return false;

            var targetInstance = ResolveInstance();
            PageRouter.Navigate(
                targetInstance is null
                    ? new PageRoute(PageIds.InstanceSetup, SubPageIds.VersionSetup)
                    : new PageRoute(
                        PageIds.InstanceSetup,
                        SubPageIds.VersionSetup,
                        targetInstance));
            if (PageRouter.CurrentPage is PageInstanceSetup setup)
                setup.Reset();
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "初始化实例设置失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
            return false;
        }
        finally
        {
            IsResetting = false;
        }
    }
}

/// <summary>实例左栏中的一条导航项。</summary>
public sealed record PageInstanceLeftItem(
    SubPageId SubPage,
    string TitleKey,
    string SvgIcon,
    double LogoScale,
    bool IsResetButton,
    SubPageId? RefreshType,
    bool IsVisible);

/// <summary>实例左栏条目：分类标题或可点击条目。</summary>
public sealed record PageInstanceLeftEntry(
    string? SectionTitleKey,
    PageInstanceLeftItem? Item)
{
    public bool IsSection => SectionTitleKey is not null;

    public static PageInstanceLeftEntry CreateSection(string titleKey)
    {
        return new PageInstanceLeftEntry(titleKey, null);
    }

    public static PageInstanceLeftEntry CreateItem(
        SubPageId subPage,
        string titleKey,
        string svgIcon,
        double logoScale = 1d,
        bool isResetButton = false,
        bool isVisible = true,
        SubPageId? refreshType = null)
    {
        return new PageInstanceLeftEntry(
            null,
            new PageInstanceLeftItem(
                subPage,
                titleKey,
                svgIcon,
                logoScale,
                isResetButton,
                refreshType,
                isVisible));
    }
}
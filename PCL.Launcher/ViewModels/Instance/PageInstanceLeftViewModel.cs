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

    public static PageSubType CurrentSubType =>
        PageRouter.Current.Page == PageType.InstanceSetup
            ? PageRouter.Current.SubType
            : PageSubType.VersionOverall;

    public static void SetCurrentInstance(McInstance? instance)
    {
        CurrentInstance = instance;
    }

    public static McInstance? ResolveInstance()
    {
        return PageRouter.Current.Page == PageType.InstanceSetup
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
    public bool NavigateTo(object? parameter)
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
            PageInstanceLeftEntry.CreateSection(Lang.Text("Instance.Left.Game")),
            PageInstanceLeftEntry.CreateItem(
                PageSubType.VersionOverall,
                "Instance.Left.Overview",
                "lucide/layout-dashboard",
                0.95),
            PageInstanceLeftEntry.CreateItem(
                PageSubType.VersionSetup,
                "Instance.Left.Settings",
                "lucide/settings",
                1.0,
                true),
            PageInstanceLeftEntry.CreateItem(
                PageSubType.VersionInstall,
                "Instance.Left.Install",
                "lucide/wrench",
                0.87,
                isVisible: ItemInstallVisible,
                refreshType: PageSubType.VersionInstall),
            PageInstanceLeftEntry.CreateItem(
                PageSubType.VersionExport,
                "Instance.Left.Export",
                "lucide/package",
                isVisible: ItemExportVisible,
                refreshType: PageSubType.VersionExport)
        };

        if (TextResourceVisible)
            result.Add(PageInstanceLeftEntry.CreateSection(Lang.Text("Instance.Left.Resources")));

        if (ItemWorldVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionWorld,
                    "Instance.Left.Saves",
                    "lucide/globe",
                    0.95,
                    refreshType: PageSubType.VersionWorld));
        if (ItemScreenshotVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionScreenshot,
                    "Instance.Left.Screenshots",
                    "lucide/image",
                    0.95,
                    refreshType: PageSubType.VersionScreenshot));

        if (ItemModVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionMod,
                    "Instance.Left.Mod",
                    "lucide/puzzle",
                    refreshType: PageSubType.VersionMod));
        else if (ItemModDisabledVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionModDisabled,
                    "Instance.Left.Mod",
                    "lucide/puzzle"));

        if (ItemResourcePackVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionResourcePack,
                    "Instance.Left.ResourcePacks",
                    "lucide/layers",
                    0.85,
                    refreshType: PageSubType.VersionResourcePack));
        if (ItemShaderVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionShader,
                    "Instance.Left.Shaders",
                    "lucide/sparkles",
                    1.15,
                    refreshType: PageSubType.VersionShader));
        if (ItemSchematicVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionSchematic,
                    "Instance.Left.Schematics",
                    "lucide/file-box",
                    0.85,
                    refreshType: PageSubType.VersionSchematic));
        if (ItemServerVisible)
            result.Add(
                PageInstanceLeftEntry.CreateItem(
                    PageSubType.VersionServer,
                    "Instance.Left.Servers",
                    "lucide/server",
                    0.85,
                    refreshType: PageSubType.VersionServer));

        return result;
    }

    public static bool IsCurrentPage(PageSubType subType)
    {
        return subType == CurrentSubType;
    }

    public static void ChangePage(PageSubType subType)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        if (PageRouter.Current.Page == PageType.InstanceSetup
            && PageRouter.Current.SubType == subType
            && ReferenceEquals(PageRouter.Current.Parameter, instance))
            return;
        PageRouter.Navigate(new PageStackData(PageType.InstanceSetup, subType, instance));
    }

    public static void RefreshSubPage(PageSubType subType)
    {
        var instance = ResolveInstance();
        if (instance is null)
            return;
        PageRouter.Navigate(new PageStackData(PageType.InstanceSetup, subType, instance));
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

            PageRouter.Navigate(
                new PageStackData(
                    PageType.InstanceSetup,
                    PageSubType.VersionSetup,
                    ResolveInstance()));
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
    PageSubType SubType,
    string TitleKey,
    string SvgIcon,
    double LogoScale,
    bool IsResetButton,
    PageSubType? RefreshType,
    bool IsVisible);

/// <summary>实例左栏条目：分类标题或可点击条目。</summary>
public sealed record PageInstanceLeftEntry(
    string? SectionTitle,
    PageInstanceLeftItem? Item)
{
    public bool IsSection => SectionTitle is not null;

    public static PageInstanceLeftEntry CreateSection(string title)
    {
        return new PageInstanceLeftEntry(title, null);
    }

    public static PageInstanceLeftEntry CreateItem(
        PageSubType subType,
        string titleKey,
        string svgIcon,
        double logoScale = 1d,
        bool isResetButton = false,
        bool isVisible = true,
        PageSubType? refreshType = null)
    {
        return new PageInstanceLeftEntry(
            null,
            new PageInstanceLeftItem(
                subType,
                titleKey,
                svgIcon,
                logoScale,
                isResetButton,
                refreshType,
                isVisible));
    }
}
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>存档左侧栏页的视图模型。</summary>
public partial class PageInstanceSavesLeftViewModel : ViewModelBase
{
    private static readonly PageDescriptor VersionSavesPage =
        PageCatalog.RequirePage(PageIds.VersionSaves);

    public static PageInstanceSavesLeftViewModel? Current { get; private set; }

    public static string CurrentSave { get; set; } = string.Empty;

    public static bool ShowDatapackTab { get; set; } = true;

    public static bool IsDatapackTabVisible => ShowDatapackTab;

    public IReadOnlyList<PageInstanceSavesLeftItem> Items { get; } =
    [
        .. VersionSavesPage.SubPages.Select(descriptor => new PageInstanceSavesLeftItem(
            descriptor.Id,
            descriptor.TitleKey,
            descriptor.IconKey ?? string.Empty))
    ];

    public void Attach()
    {
        Current = this;
    }

    public static void UpdateNavigationParameter(PageParameter? parameter)
    {
        if (parameter is string path && !string.IsNullOrEmpty(path))
            CurrentSave = path;
    }

    public static SubPageId ResolveCurrentSubPage()
    {
        return PageRouter.Current.Page == PageIds.VersionSaves
            ? PageRouter.Current.SubPage ?? SubPageIds.VersionSavesInfo
            : VersionSavesPage.DefaultSubPage ?? SubPageIds.VersionSavesInfo;
    }

    [RelayCommand]
    public static void Refresh()
    {
        PageRouter.Navigate(
            new PageRoute(PageIds.VersionSaves, ResolveCurrentSubPage(), CurrentSave));
        if (PageRouter.CurrentPage is IRefreshable refreshable)
            refreshable.Refresh();
        HintService.Show(
            Hints.Info(Lang.Text("Instance.Saves.Left.Refreshing"), false));
    }

    [RelayCommand]
    public static void OpenFolder()
    {
        InstancePageHelper.OpenFolder(CurrentSave);
    }

    public static void Navigate(SubPageId subPage)
    {
        PageRouter.Navigate(new PageRoute(PageIds.VersionSaves, subPage, CurrentSave));
    }

    /// <summary>数据包子页被隐藏时退回存档信息页。</summary>
    public static void ApplyDatapackVisibility()
    {
        if (ShowDatapackTab
            || PageRouter.Current.Page != PageIds.VersionSaves
            || PageRouter.Current.SubPage != SubPageIds.VersionSavesDatapack)
            return;

        PageRouter.Navigate(
            new PageRoute(
                PageIds.VersionSaves,
                SubPageIds.VersionSavesInfo,
                CurrentSave));
    }
}

/// <summary>存档左栏中的一条导航项。</summary>
public sealed record PageInstanceSavesLeftItem(
    SubPageId SubPage,
    string TitleKey,
    string SvgIcon);
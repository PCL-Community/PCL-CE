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
    public static PageInstanceSavesLeftViewModel? Current { get; private set; }

    /// <summary>当前存档文件夹；由路由参数与存档列表同步。</summary>
    public static string CurrentSave { get; set; } = string.Empty;

    /// <summary>存档数据版本是否支持数据包子页。</summary>
    public static bool ShowDatapackTab { get; set; } = true;

    public static bool IsDatapackTabVisible => ShowDatapackTab;

    public IReadOnlyList<PageInstanceSavesLeftItem> Items { get; } =
    [
        new(PageSubType.VersionSavesInfo, "Instance.Saves.Left.Info", "lucide/info"),
        new(PageSubType.VersionSavesDatapack, "Instance.Saves.Left.Datapack", "lucide/file-archive")
    ];

    public static string QuickActionsTitle => Lang.Text("Instance.Saves.Left.QuickActions");

    public static string OpenFolderTitle => Lang.Text("Common.Action.OpenFolder");

    public static string RefreshToolTip => Lang.Text("Common.Action.Refresh");

    public void Attach()
    {
        Current = this;
    }

    public static void UpdateNavigationParameter(object? parameter)
    {
        if (parameter is string path && !string.IsNullOrEmpty(path))
            CurrentSave = path;
    }

    public static PageSubType ResolveCurrentSubPage()
    {
        return PageRouter.Current.Page == PageType.VersionSaves
            ? PageRouter.Current.SubType
            : PageSubType.VersionSavesInfo;
    }

    [RelayCommand]
    public static void Refresh()
    {
        PageRouter.Navigate(
            new PageStackData(PageType.VersionSaves, ResolveCurrentSubPage(), CurrentSave));
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

    public static void Navigate(PageSubType subType)
    {
        PageRouter.Navigate(new PageStackData(PageType.VersionSaves, subType, CurrentSave));
    }

    /// <summary>数据包子页被隐藏时退回存档信息页。</summary>
    public static void ApplyDatapackVisibility()
    {
        if (ShowDatapackTab
            || PageRouter.Current.Page != PageType.VersionSaves
            || PageRouter.Current.SubType != PageSubType.VersionSavesDatapack)
            return;

        PageRouter.Navigate(
            new PageStackData(
                PageType.VersionSaves,
                PageSubType.VersionSavesInfo,
                CurrentSave));
    }
}

/// <summary>存档左栏中的一条导航项。</summary>
public sealed record PageInstanceSavesLeftItem(
    PageSubType SubType,
    string TitleKey,
    string SvgIcon);
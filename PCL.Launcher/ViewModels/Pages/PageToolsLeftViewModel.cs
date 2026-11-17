using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Shell;

namespace PCL;

/// <summary>工具页面左侧导航栏的视图模型。</summary>
public partial class PageToolsLeftViewModel : ViewModelBase
{
    [ObservableProperty] public partial bool GameLinkVisible { get; set; } = true;

    [ObservableProperty] public partial bool TestVisible { get; set; } = true;

    [ObservableProperty] public partial bool MultiplayerCategoryVisible { get; set; } = true;

    [ObservableProperty] public partial bool UtilitiesCategoryVisible { get; set; } = true;

    [ObservableProperty]
    public partial SubPageId SelectedPage { get; set; } = SubPageIds.ToolsGameLink;

    /// <summary>请求页面重新选中联机大厅并刷新其内容。</summary>
    public event Action? RefreshRequested;

    public static bool IsSubPageHidden(SubPageId subPage)
    {
        if (HiddenFeatureState.ForceShow)
            return false;

        var page = PageCatalog.RequirePage(PageIds.Tools);
        return page.SubPages.FirstOrDefault(item => item.Id == subPage) is { } descriptor
            && !PageDescriptor.IsVisible(descriptor);
    }

    public static bool IsHiddenPage(
        bool isGameLinkChecked,
        bool isTestChecked,
        bool isForceShow)
    {
        var hide = Config.Preference.Hide;
        var isHidden = (isGameLinkChecked && hide.ToolsGameLink)
            || (isTestChecked && hide.ToolsTest);
        return !isForceShow && isHidden;
    }

    public static SubPageId FirstVisible()
    {
        return PageCatalog.RequirePage(PageIds.Tools).FirstVisibleSubPage();
    }

    public static SubPageId ResolveInitialPageId()
    {
        return FirstVisible();
    }

    /// <summary>按当前“功能隐藏”状态计算条目与分类标题的可见性。</summary>
    public void UpdateVisibility()
    {
        GameLinkVisible = !IsSubPageHidden(SubPageIds.ToolsGameLink);
        TestVisible = !IsSubPageHidden(SubPageIds.ToolsTest);
        MultiplayerCategoryVisible = GameLinkVisible;
        UtilitiesCategoryVisible = TestVisible;
    }

    public void Select(SubPageId subPage)
    {
        SelectedPage = subPage;
    }

    [RelayCommand]
    public void Refresh()
    {
        Select(SubPageIds.ToolsGameLink);
        RefreshRequested?.Invoke();
    }
}
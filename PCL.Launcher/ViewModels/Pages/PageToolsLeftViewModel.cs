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
    public partial PageSubType SelectedPage { get; set; } = PageSubType.ToolsGameLink;

    /// <summary>请求页面重新选中联机大厅并刷新其内容。</summary>
    public event Action? RefreshRequested;

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

    public static PageSubType FirstVisible()
    {
        return !IsSubPageHidden(PageSubType.ToolsGameLink)
            ? PageSubType.ToolsGameLink
            : !IsSubPageHidden(PageSubType.ToolsTest)
                ? PageSubType.ToolsTest
                : PageSubType.ToolsGameLink;
    }

    public static PageSubType ResolveInitialPageId()
    {
        return FirstVisible();
    }

    /// <summary>按当前“功能隐藏”状态计算条目与分类标题的可见性。</summary>
    public void UpdateVisibility()
    {
        GameLinkVisible = !IsSubPageHidden(PageSubType.ToolsGameLink);
        TestVisible = !IsSubPageHidden(PageSubType.ToolsTest);
        MultiplayerCategoryVisible = GameLinkVisible;
        UtilitiesCategoryVisible = TestVisible;
    }

    public void Select(PageSubType sub)
    {
        SelectedPage = sub;
    }

    [RelayCommand]
    public void Refresh()
    {
        Select(PageSubType.ToolsGameLink);
        RefreshRequested?.Invoke();
    }
}
namespace PCL.Navigation;

/// <summary>路由层向标题栏与外壳发布的当前导航状态。</summary>
public sealed record PageNavigationState(
    PageStackData Current,
    string Title,
    bool IsSecondary,
    MainTab Tab,
    bool CanGoBack,
    double LeftWidth);
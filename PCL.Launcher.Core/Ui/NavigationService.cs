namespace PCL;

/// <summary>业务层页面导航。</summary>
public interface ICompNavigationService
{
    /// <summary>跳转到 Comp 详情页。</summary>
    void ShowCompDetail(Comp.CompProject project);

    void ShowTaskManager();
    void ShowInstanceSelect();
}

public static class CompNavigation
{
    public static ICompNavigationService? Current { get; set; }

    public static void ShowTaskManager()
    {
        Current?.ShowTaskManager();
    }

    public static void ShowInstanceSelect()
    {
        Current?.ShowInstanceSelect();
    }
}
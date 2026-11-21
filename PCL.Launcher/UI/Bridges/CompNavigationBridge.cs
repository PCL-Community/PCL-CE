using PCL.Navigation;

namespace PCL.UI.Bridges;

/// <summary>Comp 详情与二级页面的导航桥接。</summary>
public sealed class CompNavigationBridge : ICompNavigationService
{
    public void ShowCompDetail(Comp.CompProject project)
    {
        PageRouter.Navigate(PageIds.CompDetail, null, project);
    }

    public void ShowTaskManager()
    {
        PageRouter.Navigate(PageIds.TaskManager);
    }

    public void ShowInstanceSelect()
    {
        PageRouter.Navigate(PageIds.InstanceSelect);
    }
}
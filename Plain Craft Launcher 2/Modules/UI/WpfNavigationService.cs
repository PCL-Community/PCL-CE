namespace PCL;

/// <summary>将业务导航桥接到 WPF 主窗口。</summary>
public sealed class WpfCompNavigationService : ICompNavigationService
{
    public void ShowCompDetail(object project)
    {
        if (project is not Comp.CompProject compProject) return;
        Launcher.frmMain?.PageChange(
            new FormMain.PageStackData
            {
                page = FormMain.PageType.CompDetail,
                additional = (
                    compProject,
                    [],
                    string.Empty,
                    Comp.CompLoaderType.Any,
                    Comp.CompType.Any,
                    string.Empty)
            });
    }

    public void ShowTaskManager()
    {
        Launcher.frmMain?.PageChange(FormMain.PageType.TaskManager);
    }

    public void ShowInstanceSelect()
    {
        Launcher.frmMain?.PageChange(FormMain.PageType.InstanceSelect);
    }
}
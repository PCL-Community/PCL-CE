using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Speed;

/// <summary>任务管理页宿主：左栏为任务统计，右栏为任务卡片列表。</summary>
public sealed class TaskManagerHost : IPageHost
{
    private readonly PageSpeedLeft _left;
    private PageSpeedRight? _right;

    public TaskManagerHost()
    {
        _left = new PageSpeedLeft();
    }

    public PageId Page => PageIds.TaskManager;

    public MainTab? Tab => null;

    public double LeftWidth => 200d;

    public string GetTitle(PageRoute stack)
    {
        return Lang.Text("Main.Title.TaskManager");
    }

    public Control? GetLeft(PageRoute stack)
    {
        return _left;
    }

    public Control GetRight(PageRoute stack)
    {
        return _right ??= new PageSpeedRight();
    }

    public static void OnTaskAdded(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RefreshNow();
    }

    public static void OnTaskRemoved(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RemoveTask(loader);
    }

    public static void OnTaskRefreshed(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RefreshNow();
    }
}
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

    public PageType Page => PageType.TaskManager;

    public MainTab? Tab => null;

    public double LeftWidth => 200d;

    public string GetTitle(PageStackData stack)
    {
        return Lang.Text("Main.Title.TaskManager");
    }

    public Control? GetLeft(PageStackData stack)
    {
        return _left;
    }

    public Control GetRight(PageStackData stack)
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
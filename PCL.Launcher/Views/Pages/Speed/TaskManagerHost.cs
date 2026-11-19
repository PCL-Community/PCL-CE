using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Speed;

/// <summary>任务管理页宿主：左栏为任务统计，右栏为任务卡片列表。</summary>
public sealed class TaskManagerHost(
    PageSpeedLeft left,
    PageSpeedRight right) : IPageHost
{
    public PageId Page => PageIds.TaskManager;

    public MainTab? Tab => null;

    public double LeftWidth => 200d;

    public string GetTitle(PageRoute stack)
    {
        return Lang.Text("Main.Title.TaskManager");
    }

    public Control? GetLeft(PageRoute stack)
    {
        return left;
    }

    public Control GetRight(PageRoute stack)
    {
        return right;
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
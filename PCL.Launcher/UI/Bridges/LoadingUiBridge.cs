using Avalonia.Threading;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages.Speed;
using PCL.Views.Shell;

namespace PCL.UI.Bridges;

/// <summary>加载框架通知：下载按钮、任务栏进度与任务管理页事件。</summary>
public sealed class LoadingUiBridge : ILoadingUi
{
    private static bool _taskHostWarned;

    public void TaskAdded(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskAdded(loader), nameof(TaskAdded));
        RefreshDownloadButtonDelayed();
    }

    public void TaskRemoved(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskRemoved(loader), nameof(TaskRemoved));
        RefreshDownloadButtonDelayed();
    }

    public void TaskRefreshed(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskRefreshed(loader), nameof(TaskRefreshed));
        RefreshDownloadButtonDelayed();
    }

    public void RefreshDownloadButton()
    {
        ExtraButtonsPanel.Current?.RefreshDownloadButton();
    }

    public void UpdateButtonProgress(double progress)
    {
        ExtraButtonsPanel.Current?.UpdateButtonProgress(progress);
    }

    public void UpdateTaskbar(TaskbarProgressState state, double progress)
    {
        TaskbarProgress.Update(state, progress);
    }

    /// <summary>任务列表事件在业务线程触发，统一投递到 UI 线程，确保页面读到最新的任务栏列表。</summary>
    private static void NotifyTaskPage(Action<PageSpeedLeft> action, string feature)
    {
        var host = PageSpeedLeft.Current;
        if (host is null)
        {
            if (_taskHostWarned) return;
            _taskHostWarned = true;
            UiInteraction.WarnMissing(feature, "TASK");
            return;
        }

        Dispatcher.UIThread.Post(() => action(host));
    }

    private static void RefreshDownloadButtonDelayed()
    {
        Dispatcher.UIThread.Post(() => { ExtraButtonsPanel.Current?.RefreshDownloadButton(); });
    }
}
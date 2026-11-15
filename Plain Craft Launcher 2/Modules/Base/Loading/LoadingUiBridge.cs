using System.Windows.Shell;
using PCL.Core.Platform;

namespace PCL;

/// <summary>将加载框架的 UI 通知桥接到 WPF 主窗口。</summary>
public sealed class LoadingUiBridge : ILoadingUi
{
    public void TaskAdded(Loading.LoaderBase loader)
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmSpeedLeft?.TaskRemove(loader));
    }

    public void TaskRemoved(Loading.LoaderBase loader)
    {
    }

    public void TaskRefreshed(Loading.LoaderBase loader)
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmSpeedLeft?.TaskRefresh(loader));
    }

    public void RefreshDownloadButton()
    {
        PlatformServices.UiDispatcher?.Post(() => Launcher.frmMain?.BtnExtraDownload.ShowRefresh());
    }

    public void UpdateButtonProgress(double progress)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is { } main) main.BtnExtraDownload.Progress = progress;
        });
    }

    public void UpdateTaskbar(TaskbarProgressState state, double progress)
    {
        PlatformServices.UiDispatcher?.Post(() =>
        {
            if (Launcher.frmMain is not { } main) return;
            main.TaskbarItemInfo ??= new TaskbarItemInfo();
            main.TaskbarItemInfo.ProgressState = state switch
            {
                TaskbarProgressState.Normal => TaskbarItemProgressState.Normal,
                TaskbarProgressState.Error => TaskbarItemProgressState.Error,
                TaskbarProgressState.Paused => TaskbarItemProgressState.Paused,
                TaskbarProgressState.Indeterminate => TaskbarItemProgressState.Indeterminate,
                _ => TaskbarItemProgressState.None
            };
            if (state == TaskbarProgressState.Normal)
                main.TaskbarItemInfo.ProgressValue = progress;
            main.BtnExtraDownload.ShowRefresh();
        });
    }
}
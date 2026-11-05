using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Speed;

/// <summary>
///     任务管理页左栏：汇总当前任务列表的进度、下载速度、剩余文件数与连接数。
///     每 300 ms 从 <see cref="Loading.loaderTaskbar" /> 与 <see cref="NetFacade.NetManager" /> 取值刷新。
/// </summary>
public partial class PageSpeedLeft : MyPageLeft, IRefreshable
{
    private const int WatcherInterval = 300;

    private readonly DispatcherTimer _timer;

    public static PageSpeedLeft? Current { get; private set; }

    public PageSpeedLeft()
    {
        InitializeComponent();
        Current = this;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(WatcherInterval)
        };
        _timer.Tick += (_, _) => RefreshStatistics();
        ApplyDebugRowVisibility();
    }

    public void Refresh()
    {
        RefreshStatistics();
    }

    public void OnTaskAdded(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RefreshNow();
    }

    public void OnTaskRemoved(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RemoveTask(loader);
    }

    public void OnTaskRefreshed(Loading.LoaderBase loader)
    {
        PageSpeedRight.Current?.RefreshNow();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshStatistics();
        _timer.Start();
    }

    public override void OnNavigatedFrom()
    {
        _timer.Stop();
    }

    protected override void RefreshLocalization()
    {
        RefreshStatistics();
    }

    /// <summary>刷新左栏统计数据；可在任意线程调用。</summary>
    public void RefreshStatistics()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshStatistics);
            return;
        }

        try
        {
            if (!Loading.loaderTaskbar.Any())
            {
                LabProgress.Text = Lang.Number(1d, "P0");
                LabSpeed.Text = $"{LauncherCommon.GetString(0)}/s";
                LabFile.Text = Lang.Number(0, "N0");
                LabThread.Text =
                    $"{Lang.Number(0, "N0")} / {Lang.Number(NetFacade.NetTaskConnectionLimit, "N0")}";
                return;
            }

            var tasks = Loading.loaderTaskbar.Where(loader => loader.show).ToList();
            var rawPercent = tasks.Count > 0
                ? Math.Clamp(tasks.Average(loader => loader.Progress), 0d, 1d)
                : 1d;
            LabProgress.Text = rawPercent > 0.999999d
                ? Lang.Number(1d, "P0")
                : Lang.Number(rawPercent, "P2");
            LabSpeed.Text = $"{LauncherCommon.GetString(NetFacade.NetManager.Speed)}/s";
            LabFile.Text = NetFacade.NetManager.FileRemain < 0
                ? "0*"
                : Lang.Number(NetFacade.NetManager.FileRemain, "N0");
            LabThread.Text =
                $"{Lang.Number(NetFacade.NetManager.ConnectionCount, "N0")} / {Lang.Number(NetFacade.NetTaskConnectionLimit, "N0")}";
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "任务管理左栏监视出错",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Speed.Error.OperationFailed"));
        }
    }

    private void ApplyDebugRowVisibility()
    {
        if (LauncherCommon.ModeDebug)
            return;

        for (var index = 12; index <= 15 && index < RowDefinitions.Count; index++)
            RowDefinitions[index].Height = new GridLength(0d);
    }
}
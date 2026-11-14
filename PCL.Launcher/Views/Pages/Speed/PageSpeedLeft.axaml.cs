using Avalonia.Controls;
using Avalonia.Threading;
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

    public PageSpeedLeft()
    {
        InitializeComponent();
        Current = this;
        DataContext = ViewModel;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(WatcherInterval)
        };
        _timer.Tick += (_, _) => RefreshStatistics();
        ApplyDebugRowVisibility();
    }

    public static PageSpeedLeft? Current { get; private set; }

    public PageSpeedLeftViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        RefreshStatistics();
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

        ViewModel.Refresh();
    }

    private void ApplyDebugRowVisibility()
    {
        if (LauncherCommon.ModeDebug)
            return;

        for (var index = 12; index <= 15 && index < RowDefinitions.Count; index++)
            RowDefinitions[index].Height = new GridLength(0d);
        LabThreadTitle.IsVisible = false;
        LineThread.IsVisible = false;
        LabThread.IsVisible = false;
    }
}
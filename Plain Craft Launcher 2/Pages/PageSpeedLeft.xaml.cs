using PCL.Core.App.Localization;
using PCL.Core.App.Tasks;
using PCL.Core.IO;
using PCL.Network;
using System.Collections.Specialized;
using System.Windows;

namespace PCL;

public partial class PageSpeedLeft
{
    public PageSpeedLeft()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        TaskCenter.Tasks.CollectionChanged += OnTasksCollectionChanged;
        RefreshFromTaskCenter();

        // 非调试模式隐藏线程数
        if (!ModBase.ModeDebug)
        {
            RowDefinitions[11].Height = new GridLength(0d);
            RowDefinitions[12].Height = new GridLength(0d);
            RowDefinitions[13].Height = new GridLength(0d);
        }
    }

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFromTaskCenter();
    }

    private void RefreshFromTaskCenter()
    {
        UpdateLeftPanel();
        TryReturnToHome();
    }

    private void UpdateLeftPanel()
    {
        var tasks = TaskCenter.Tasks;

        if (tasks.Count == 0)
        {
            LabProgress.Text = Lang.Number(1d, "P0");
        }
        else
        {
            var progressive = tasks.Where(t => t.SupportProgress).ToList();
            var rawPercent = progressive.Count > 0
                ? Math.Clamp(progressive.Average(t => t.Progress), 0, 1)
                : 1d;
            LabProgress.Text = rawPercent > 0.999999d
                ? Lang.Number(1d, "P0")
                : Lang.Number(rawPercent, "P2");
        }

        LabSpeed.Text = ByteStream.GetReadableLength(ModNet.NetManager.Speed, 3) + "/s";
        LabFile.Text = ModNet.NetManager.FileRemain < 0 ? "0*" : Lang.Number(ModNet.NetManager.FileRemain, "N0");
        LabThread.Text = $@"{Lang.Number(ModNet.NetManager.ThreadCount, "N0")} / {Lang.Number(ModNet.NetTaskThreadLimit, "N0")}";
    }

    /// <summary>
    /// 若没有任务，尝试返回主页。
    /// </summary>
    private void TryReturnToHome()
    {
        var frmMain = ModMain.FrmMain;
        if (TaskCenter.Tasks.Count == 0 &&
            frmMain is not null && frmMain.PageCurrent == FormMain.PageType.TaskManager)
            frmMain.PageBack();
    }
}

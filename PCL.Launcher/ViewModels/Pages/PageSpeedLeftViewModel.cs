using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>任务管理页左栏的视图模型：汇总加载器任务、下载速度、剩余文件与连接数。</summary>
public partial class PageSpeedLeftViewModel : ViewModelBase
{
    [ObservableProperty] public partial string FileText { get; set; } = Lang.Number(0, "N0");

    [ObservableProperty]
    public partial IReadOnlyList<Loading.LoaderBase> Loaders { get; set; } = [];

    [ObservableProperty] public partial string ProgressText { get; set; } = Lang.Number(1d, "P0");

    [ObservableProperty]
    public partial string SpeedText { get; set; } = $"{LauncherCommon.GetString(0)}/s";

    [ObservableProperty]
    public partial string ThreadText { get; set; } =
        $"{Lang.Number(0, "N0")} / {Lang.Number(NetFacade.NetTaskConnectionLimit, "N0")}";

    public void Refresh()
    {
        try
        {
            Loaders = Loading.loaderTaskbar.Where(loader => loader.show).ToList();
            if (Loading.loaderTaskbar.Count == 0)
            {
                ProgressText = Lang.Number(1d, "P0");
                SpeedText = $"{LauncherCommon.GetString(0)}/s";
                FileText = Lang.Number(0, "N0");
                ThreadText =
                    $"{Lang.Number(0, "N0")} / {Lang.Number(NetFacade.NetTaskConnectionLimit, "N0")}";
                return;
            }

            var tasks = Loaders;
            var rawPercent = tasks.Count > 0
                ? Math.Clamp(tasks.Average(loader => loader.Progress), 0d, 1d)
                : 1d;
            ProgressText = rawPercent > 0.999999d
                ? Lang.Number(1d, "P0")
                : Lang.Number(rawPercent, "P2");
            SpeedText = $"{LauncherCommon.GetString(NetFacade.NetManager.Speed)}/s";
            FileText = NetFacade.NetManager.FileRemain < 0
                ? "0*"
                : Lang.Number(NetFacade.NetManager.FileRemain, "N0");
            ThreadText =
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
}
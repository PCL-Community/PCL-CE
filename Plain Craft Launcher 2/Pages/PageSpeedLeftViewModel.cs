using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Network;

namespace PCL;

/// <summary>任务管理左侧栏的视图模型。</summary>
public partial class PageSpeedLeftViewModel : ViewModelBase
{
    [ObservableProperty] private string _fileText = "123456789";

    [ObservableProperty] private string _progressText = "00.00 %";

    [ObservableProperty] private string _speedText = "0 B/s";

    [ObservableProperty] private string _threadText = "123456789";

    public void Refresh()
    {
        try
        {
            if (!ModLoader.loaderTaskbar.Any())
            {
                ProgressText = Lang.Number(1d, "P0");
                SpeedText = ModBase.GetString(0) + "/s";
                FileText = Lang.Number(0, "N0");
                ThreadText = Lang.Number(0, "N0") + " / " + Lang.Number(ModNet.NetTaskConnectionLimit, "N0");
            }
            else
            {
                var tasks = ModLoader.loaderTaskbar.Where(l => l.show).ToList();
                var rawPercent = tasks.Any()
                    ? ModBase.MathClamp(
                        tasks.Average(l => l.Progress),
                        0, 1)
                    : 1d;
                var predictText = Lang.Number(rawPercent, "P2");
                ProgressText = rawPercent > 0.999999d ? Lang.Number(1d, "P0") : predictText;
                SpeedText = ModBase.GetString(ModNet.NetManager.Speed) + "/s";
                FileText = ModNet.NetManager.FileRemain < 0 ? "0*" : Lang.Number(ModNet.NetManager.FileRemain, "N0");
                ThreadText = Lang.Number(ModNet.NetManager.ConnectionCount, "N0") + " / " +
                             Lang.Number(ModNet.NetTaskConnectionLimit, "N0");
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "任务管理左栏监视出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Speed.Error.OperationFailed"));
        }
    }
}

using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实时日志左侧栏页的视图模型。</summary>
public partial class PageLogLeftViewModel : ViewModelBase
{
    public string InstancesTitle => Lang.Text("LogPage.Left.InstancesTitle");

    public bool HasLogs => ModMain.frmLogLeft is not null && ModMain.frmLogLeft.shownLogs.Count > 0;

    public bool ShouldShowMaxLinesHint => !States.Hint.MaxGameLog;

    public string GetItemInfo(ModWatcher.Watcher watcher)
    {
        return $"{watcher.version.Info} - {Lang.Date(watcher.gameProcess.StartTime, "T")}";
    }

    public ulong GetMaxLogCount()
    {
        var maxLog = (ulong)Config.System.MaxGameLog;
        switch (maxLog)
        {
            case <= 5UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 10m + 50m);
                break;
            }
            case <= 13UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 50m - 150m);
                break;
            }
            case <= 28UL:
            {
                maxLog = (ulong)Math.Round(maxLog * 100m - 800m);
                break;
            }
            default:
            {
                maxLog = 18446744073709551615UL;
                break;
            }
        }

        return maxLog;
    }
}

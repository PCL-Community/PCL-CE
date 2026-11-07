using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Log;

namespace PCL;

/// <summary>
///     游戏实时日志页左栏的视图模型：维护游戏监视器列表、各自的日志缓冲区与当前选中项。
/// </summary>
public partial class PageLogLeftViewModel : ViewModelBase
{
    private const int MaxLinesDefault = 500;

    public Dictionary<int, PageLogLeft.LogBuffer> LogBuffers { get; } = [];

    public List<KeyValuePair<int, GameWatcher.Watcher>> ShownLogs { get; } = [];

    [ObservableProperty] public partial GameWatcher.Watcher? CurrentLog { get; set; }

    [ObservableProperty] public partial int CurrentUuid { get; set; } = -1;

    [ObservableProperty] public partial bool HasLogs { get; set; }

    public static string InstancesTitle => Lang.Text("LogPage.Left.InstancesTitle");

    /// <summary>监视器列表或当前选择发生变化；由页面负责重建列表并刷新右栏。</summary>
    public event Action? Changed;

    /// <summary>当前监视器产生新日志行；由页面转交给右栏滚动渲染。</summary>
    public event Action<PageLogLeft.LogBuffer>? LogAppended;

    /// <summary>登记一个游戏监视器并切换到它的日志。可在任意线程调用。</summary>
    public void AttachWatcher(GameWatcher.Watcher watcher)
    {
        if (watcher is null)
            return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AttachWatcher(watcher));
            return;
        }

        var uuid = LauncherCommon.GetUuid();
        ShownLogs.Add(new KeyValuePair<int, GameWatcher.Watcher>(uuid, watcher));
        watcher.LogOutput += OnLogOutput;
        LogBuffers[uuid] = new PageLogLeft.LogBuffer();
        HasLogs = ShownLogs.Count > 0;
        SelectionChange(uuid);
    }

    private void OnLogOutput(GameWatcher.Watcher sender, GameWatcher.LogOutputEventArgs e)
    {
        var uuid = -1;
        foreach (var item in ShownLogs.Where(item => ReferenceEquals(item.Value, sender)))
        {
            uuid = item.Key;
            break;
        }

        if (uuid < 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!LogBuffers.TryGetValue(uuid, out var buffer))
                return;
            buffer.Lines.Add(new PageLogLeft.LogLine(e.logText, e.Level));
            TrimBuffer(buffer);
            if (uuid == CurrentUuid)
                LogAppended?.Invoke(buffer);
        });
    }

    private static void TrimBuffer(PageLogLeft.LogBuffer buffer)
    {
        var max = GetMaxLogCount();
        while ((ulong)buffer.Lines.Count > max)
            buffer.Lines.RemoveAt(0);
    }

    public static ulong GetMaxLogCount()
    {
        var maxLog = (ulong)Config.System.MaxGameLog;
        return maxLog switch
        {
            <= 5UL => (ulong)Math.Round(maxLog * 10m + 50m),
            <= 13UL => (ulong)Math.Round(maxLog * 50m - 150m),
            <= 28UL => (ulong)Math.Round(maxLog * 100m - 800m),
            _ => ulong.MaxValue
        };
    }

    public void SelectionChange(int uuid)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SelectionChange(uuid));
            return;
        }

        if (uuid <= 0)
        {
            CurrentUuid = -1;
            CurrentLog = null;
        }
        else
        {
            foreach (var item in ShownLogs.Where(item => item.Key == uuid))
            {
                CurrentUuid = uuid;
                CurrentLog = item.Value;
                break;
            }
        }

        Changed?.Invoke();
    }

    public void RemoveItem(int uuid)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RemoveItem(uuid));
            return;
        }

        for (var i = ShownLogs.Count - 1; i >= 0; i--)
        {
            var item = ShownLogs[i];
            if (item.Key != uuid)
                continue;

            item.Value.LogOutput -= OnLogOutput;
            ShownLogs.RemoveAt(i);
            LogBuffers.Remove(uuid);
            HasLogs = ShownLogs.Count > 0;

            if (CurrentUuid == uuid)
            {
                if (ShownLogs.Count == 0)
                    SelectionChange(-1);
                else
                    SelectionChange(ShownLogs[Math.Min(i, ShownLogs.Count - 1)].Key);
            }
            else
            {
                Changed?.Invoke();
            }

            break;
        }
    }

    /// <summary>按当前监视器列表生成左栏条目。</summary>
    public IReadOnlyList<PageLogLeftEntry> BuildEntries()
    {
        var result = new List<PageLogLeftEntry>(ShownLogs.Count);
        foreach (var (uuid, watcher) in ShownLogs)
            result.Add(
                new PageLogLeftEntry(uuid, SafeGetVersionName(watcher), GetItemInfo(watcher)));
        return result;
    }

    /// <summary>首次显示日志列表时给出最大行数提示。</summary>
    public static void ShowMaxLinesHintIfNeeded()
    {
        if (States.Hint.MaxGameLog)
            return;
        States.Hint.MaxGameLog = true;
        HintService.Show(Hints.Info(Lang.Text("LogPage.MaxLines.Hint", MaxLinesDefault)));
    }

    private static string SafeGetVersionName(GameWatcher.Watcher watcher)
    {
        try
        {
            return watcher.version?.Name ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string GetItemInfo(GameWatcher.Watcher watcher)
    {
        try
        {
            var info = watcher.version?.Info?.ToString() ?? string.Empty;
            var startTime = watcher.gameProcess?.StartTime ?? DateTime.MinValue;
            return $"{info} - {Lang.Date(startTime, "T")}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}

/// <summary>游戏实时日志左栏中的一条监视器条目。</summary>
public sealed record PageLogLeftEntry(
    int Uuid,
    string Title,
    string Info);
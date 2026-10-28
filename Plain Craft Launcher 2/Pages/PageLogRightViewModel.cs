using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实时日志右侧栏页的视图模型。</summary>
public partial class PageLogRightViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isKillEnabled;

    [ObservableProperty] private bool _isExportStackDumpEnabled;

    [ObservableProperty] private string _fatalCountText = $"0 {Lang.Text("LogPage.Level.Fatal")}";

    [ObservableProperty] private string _errorCountText = $"0 {Lang.Text("LogPage.Level.Error")}";

    [ObservableProperty] private string _warnCountText = $"0 {Lang.Text("LogPage.Level.Warn")}";

    [ObservableProperty] private string _infoCountText = $"0 {Lang.Text("LogPage.Level.Info")}";

    [ObservableProperty] private string _debugCountText = $"0 {Lang.Text("LogPage.Level.Debug")}";

    public bool CanShow()
    {
        return ModMain.frmLogLeft is not null && ModMain.frmLogLeft.currentLog is not null &&
               ModMain.frmLogLeft.currentUuid > 0 && ModMain.frmLogLeft.shownLogs.Count > 0;
    }

    public void RefreshOperationButtons()
    {
        var log = ModMain.frmLogLeft?.currentLog;
        if (log is null)
        {
            IsKillEnabled = false;
            IsExportStackDumpEnabled = false;
            return;
        }

        IsKillEnabled = !log.gameProcess.HasExited;
        IsExportStackDumpEnabled = !log.gameProcess.HasExited && !string.IsNullOrWhiteSpace(log.jStackPath);
    }

    public void RefreshCounters()
    {
        var log = ModMain.frmLogLeft?.currentLog;
        var fatal = log?.countFatal ?? 0u;
        var error = log?.countError ?? 0u;
        var warn = log?.countWarn ?? 0u;
        var info = log?.countInfo ?? 0u;
        var debug = log?.countDebug ?? 0u;
        FatalCountText = $"{fatal} {Lang.Text("LogPage.Level.Fatal")}";
        ErrorCountText = $"{error} {Lang.Text("LogPage.Level.Error")}";
        WarnCountText = $"{warn} {Lang.Text("LogPage.Level.Warn")}";
        InfoCountText = $"{info} {Lang.Text("LogPage.Level.Info")}";
        DebugCountText = $"{debug} {Lang.Text("LogPage.Level.Debug")}";
    }

    public static object GetMaxLogHintText(object value)
    {
        return value switch
        {
            _ when (int)value <= 5 => ((int)value * 10 + 50).ToString(),
            _ when (int)value <= 13 => ((int)value * 50 - 150).ToString(),
            _ when (int)value <= 28 => ((int)value * 100 - 800).ToString(),
            _ => Lang.Text("LogPage.MaxLines.Unlimited")
        };
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class PageSetupLog
{
    public PageSetupLog()
    {
        InitializeComponent();
        Loaded += PageOtherLog_Loaded;
    }

    private static string LogDirectory => LogService.Logger.Configuration.StoreFolder;

    private static List<string> CurrentLogs
    {
        get
        {
            var logs = LogService.Logger.CurrentLogFiles;
            return logs.Select(item => Path.GetFullPath(item)).ToList();
        }
    }

    private async void PageOtherLog_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        try
        {
            await LoadListAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Log.ExportFailed"), ModBase.LogLevel.Hint);
        }
        // 非重复加载部分
        if (IsLoaded)
            return;
    }

    private async Task LoadListAsync()
    {
        PanList.Children.Clear();
        var current = CurrentLogs;
        var logFiles = await FileSystemService.GetFilesAsync(LogDirectory);
        foreach (var snapshot in logFiles.OrderByDescending(file => file.LastWriteTimeUtc))
        {
            var fullPath = snapshot.FullPath;
            var title = snapshot.Name;
            if (title.StartsWith("Launch"))
            {
                title = title.Substring(7, title.Length - 11);
                DateTime dt;
                var r = DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt);
                if (r)
                    title = Lang.Date(dt, "G");
                if (current.Any(log => log.Equals(fullPath)))
                    title = Lang.Text("Setup.Misc.Log.CurrentSuffix", title);
            }
            else if (title.StartsWith("LastPending"))
            {
                title = title.Substring(11, title.Length - 15);
                if (title.Length > 1)
                    title = Lang.Text("Setup.Misc.Log.TempStored", title.Substring(1));
                else
                    title = Lang.Text("Setup.Misc.Log.TempUnoutput");
            }

            var ele = new MyListItem
            {
                Type = MyListItem.CheckType.Clickable,
                Title = title,
                Info = fullPath,
                Tag = fullPath
            };
            ele.Click += (sender, e) =>
            {
                var s = (MyListItem)sender;
                var file = (string)s.Tag;
                Basics.OpenPath(file);
            };
            PanList.Children.Add(ele);
        }
    }

    private static async Task ExportLogAsync(IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Misc.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName = "PCL_N_Logs_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var fileName = baseName + ".zip";
        var selectedPath = SystemDialogs.SelectSaveFile(Lang.Text("Setup.Misc.Log.ExportSaveTitle"), fileName, filter, desktopPath);
        if (string.IsNullOrEmpty(selectedPath))
            return;
        try
        {
            await FileSystemService.CreateZipAsync(selectedPath, sourceFiles);
            ModMain.Hint(Lang.Text("Setup.Misc.Log.ExportSuccess"), ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Log.ExportFailed"), ModBase.LogLevel.Hint);
        }
    }

    private void ButtonOpenDir_OnClick(object sender, MouseButtonEventArgs e)
    {
        Basics.OpenPath(LogDirectory);
    }

    private async void ButtonClean_OnClick(object sender, MouseButtonEventArgs e)
    {
        var r = ModMain.MyMsgBox(Lang.Text("Setup.Misc.Log.Clear.Confirm.Message"), Lang.Text("Setup.Misc.Log.Clear.Confirm.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true);
        if (r != 1)
            return;
        try
        {
            await FileSystemService.DeleteFilesExceptAsync(LogDirectory, CurrentLogs);
            ModMain.Hint(Lang.Text("Setup.Misc.Log.Clear.Success"), ModMain.HintType.Finish);
            await LoadListAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Log.Clear.Confirm.Title"), ModBase.LogLevel.Hint);
        }
    }

    private async void ButtonExportAll_OnClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var files = await FileSystemService.GetFilesAsync(LogDirectory);
            await ExportLogAsync(files.Select(file => file.FullPath));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Log.ExportFailed"), ModBase.LogLevel.Hint);
        }
    }

    private async void ButtonExport_OnClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var files = await FileSystemService.GetFilesAsync(LogDirectory);
            var pendingLogs = files
                .Select(file => file.FullPath)
                .Where(path => path.IsMatch(RegexPatterns.LastPendingLogPath));
            await ExportLogAsync(CurrentLogs.Concat(pendingLogs));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Misc.Log.ExportFailed"), ModBase.LogLevel.Hint);
        }
    }
}

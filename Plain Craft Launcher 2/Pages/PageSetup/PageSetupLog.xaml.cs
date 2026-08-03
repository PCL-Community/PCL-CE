using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.App.Localization;

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

    private void PageOtherLog_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        LoadList();
        // 非重复加载部分
        if (IsLoaded)
            return;
    }

    public void LoadList()
    {
        PanList.Children.Clear();
        var current = CurrentLogs;
        var logFiles = Directory.GetFiles(LogDirectory).OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
        foreach (var item in logFiles)
        {
            var fullPath = Path.GetFullPath(item);
            var title = Path.GetFileName(item);
            if (title.StartsWith("Launch"))
            {
                title = title.Substring(7, title.Length - 11);
                DateTime dt;
                var r = DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt);
                if (r)
                    title = Lang.Date(dt, "G");
                if (current.Any(log => log.Equals(fullPath)))
                    title = Lang.Text("Setup.Log.CurrentSuffix", title);
            }
            else if (title.StartsWith("LastPending"))
            {
                title = title.Substring(11, title.Length - 15);
                if (title.Length > 1)
                    title = Lang.Text("Setup.Log.TempStored", title.Substring(1));
                else
                    title = Lang.Text("Setup.Log.TempUnoutput");
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

    private static void ExportLog(IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName = "PCL_CE_Logs_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempDirName = baseName + ".tmp";
        var fileName = baseName + ".zip";
        var selectedPath = SystemDialogs.SelectSaveFile(Lang.Text("Setup.Log.ExportSaveTitle"), fileName, filter, desktopPath);
        if (string.IsNullOrEmpty(selectedPath))
            return;
        try
        {
            Directory.CreateDirectory(tempDirName);
            if (File.Exists(selectedPath))
                File.Delete(selectedPath);
            using (var zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create))
            {
                foreach (var item in sourceFiles)
                {
                    var itemFileName = Path.GetFileName(item);
                    var tempPath = Path.Combine(tempDirName, itemFileName);
                    File.Copy(item, tempPath);
                    zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest);
                    File.Delete(tempPath);
                }
            }

            HintService.Hint(Lang.Text("Setup.Log.ExportSuccess"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
        finally
        {
            if (Directory.Exists(tempDirName))
                Directory.Delete(tempDirName);
        }
    }

    private void ButtonOpenDir_OnClick(object sender, MouseButtonEventArgs e)
    {
        Basics.OpenPath(LogDirectory);
    }

    private void ButtonClean_OnClick(object sender, MouseButtonEventArgs e)
    {
        var r = ModMain.MyMsgBox(Lang.Text("Setup.Log.Clear.Confirm.Message"), Lang.Text("Setup.Log.Clear.Confirm.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel"), isWarn: true);
        if (r != 1)
            return;
        var currentSet = new HashSet<string>(CurrentLogs);
        foreach (var item in Directory.GetFiles(LogDirectory))
            if (!currentSet.Contains(item))
                File.Delete(item);
        HintService.Hint(Lang.Text("Setup.Log.Clear.Success"), HintType.Success);
        LoadList();
    }

    private void ButtonExportAll_OnClick(object sender, MouseButtonEventArgs e)
    {
        ExportLog(Directory.GetFiles(LogDirectory));
    }

    private void ButtonExport_OnClick(object sender, MouseButtonEventArgs e)
    {
        var pendingLogs = Array.FindAll(Directory.GetFiles(LogDirectory),
            s => s.IsMatch(RegexPatterns.LastPendingLogPath));
        ExportLog(CurrentLogs.Concat(pendingLogs));
    }
}

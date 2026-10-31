using System.Globalization;
using System.IO;
using System.IO.Compression;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「日志」页的视图模型。</summary>
public partial class PageSetupLogViewModel : ViewModelBase
{
    public IReadOnlyList<SetupLogEntry> Entries { get; private set; } = [];

    public string LogDirectory => LogService.Logger.Configuration.StoreFolder;

    public IReadOnlyList<string> CurrentLogs =>
        LogService.Logger.CurrentLogFiles.Select(Path.GetFullPath).ToList();

    public async Task LoadListAsync()
    {
        var entries = await RunAsync(ct => Task.Run(BuildEntries, ct));
        if (entries is null)
            return;
        Entries = entries;
    }

    public async Task ExportAllAsync()
    {
        var files = await RunAsync(ct => Task.Run(() => Directory.GetFiles(LogDirectory), ct));
        if (files is null)
            return;
        await ExportAsync(files);
    }

    public async Task ExportCurrentAsync()
    {
        var files = await RunAsync(ct => Task.Run(() => CurrentLogs.Concat(Array.FindAll(
            Directory.GetFiles(LogDirectory), s => s.IsMatch(RegexPatterns.LastPendingLogPath))).ToList(), ct));
        if (files is null)
            return;
        await ExportAsync(files);
    }

    public async Task<bool> CleanAsync()
    {
        var result = (await Dialog.ShowAsync(new TextDialogRequest
        {
            Title = Lang.Text("Setup.Log.Clear.Confirm.Title"),
            Content = Lang.Text("Setup.Log.Clear.Confirm.Message"),
            Buttons = DialogButtons.OkCancel,
            IsWarn = true
        })).Button;
        if (result != 1)
            return false;
        var currentSet = new HashSet<string>(CurrentLogs);
        var cleaned = await RunAsync(ct => Task.Run(() => DeleteInactiveLogs(currentSet), ct));
        if (cleaned is not true)
            return false;
        HintService.Hint(Lang.Text("Setup.Log.Clear.Success"), HintType.Success);
        return true;
    }

    public static void OpenLogDirectory() => Basics.OpenPath(LogService.Logger.Configuration.StoreFolder);

    private async Task ExportAsync(IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName = "PCL_CE_Logs_" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var tempDirName = baseName + ".tmp";
        var fileName = baseName + ".zip";
        var selectedPath = SystemDialogs.SelectSaveFile(Lang.Text("Setup.Log.ExportSaveTitle"), fileName, filter,
            desktopPath);
        if (string.IsNullOrEmpty(selectedPath))
            return;
        var written = await RunAsync(ct => Task.Run(() => WriteZip(selectedPath, tempDirName, sourceFiles), ct));
        if (written is true)
            HintService.Hint(Lang.Text("Setup.Log.ExportSuccess"), HintType.Success);
    }

    private List<SetupLogEntry> BuildEntries()
    {
        var result = new List<SetupLogEntry>();
        var current = CurrentLogs;
        var logFiles = Directory.GetFiles(LogDirectory).OrderByDescending(f => File.GetLastWriteTime(f)).ToArray();
        foreach (var item in logFiles)
        {
            var fullPath = Path.GetFullPath(item);
            var title = Path.GetFileName(item);
            if (title.StartsWith("Launch"))
            {
                title = title.Substring(7, title.Length - 11);
                if (DateTime.TryParseExact(title, "yyyy-M-d-HHmmssfff", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
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

            result.Add(new SetupLogEntry(title, fullPath));
        }

        return result;
    }

    private static bool DeleteInactiveLogs(HashSet<string> currentSet)
    {
        try
        {
            foreach (var item in Directory.GetFiles(LogService.Logger.Configuration.StoreFolder))
                if (!currentSet.Contains(item))
                    File.Delete(item);
            return true;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "删除非当前日志文件失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
            return false;
        }
    }

    private static bool WriteZip(string selectedPath, string tempDirName, IEnumerable<string> sourceFiles)
    {
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

            return true;
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Setup.Log.ExportFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
            return false;
        }
        finally
        {
            if (Directory.Exists(tempDirName))
                Directory.Delete(tempDirName);
        }
    }
}

/// <summary>「日志」页列表中的一条日志文件。</summary>
public class SetupLogEntry(string title, string filePath)
{
    public string Title { get; } = title;

    public string FilePath { get; } = filePath;
}

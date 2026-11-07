using System.Globalization;
using System.IO.Compression;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面中「日志」页的视图模型。</summary>
public class PageSetupLogViewModel : ViewModelBase
{
    public IReadOnlyList<SetupLogEntry> Entries { get; private set; } = [];

    public string LogDirectory => LogService.Logger.Configuration.StoreFolder;

    public IReadOnlyList<string> CurrentLogs =>
        [.. LogService.Logger.CurrentLogFiles.Select(Path.GetFullPath)];

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
        var files = await RunAsync(ct => Task.Run(
            () => CurrentLogs
                .Concat(
                    Directory
                        .GetFiles(LogDirectory)
                        .Where(s => s.IsMatch(RegexPatterns.LastPendingLogPath)))
                .ToList(),
            ct));
        if (files is null)
            return;
        await ExportAsync(files);
    }

    public async Task<bool> CleanAsync()
    {
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Setup.Log.Clear.Confirm.Title"),
                Content = Lang.Text("Setup.Log.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        if (result.Button != 1)
            return false;

        var currentSet = new HashSet<string>(CurrentLogs);
        var cleaned = await RunAsync(ct => Task.Run(() => DeleteInactiveLogs(currentSet), ct));
        if (cleaned is not true)
            return false;
        HintService.Show(Hints.Success(Lang.Text("Setup.Log.Clear.Success")));
        return true;
    }

    public static void OpenLogDirectory()
    {
        Basics.OpenPath(LogService.Logger.Configuration.StoreFolder);
    }

    private async Task ExportAsync(IEnumerable<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName =
            $"PCL_CE_Logs_{DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
        var tempDirName = $"{baseName}.tmp";
        var fileName = $"{baseName}.zip";
        var selectedPath = SystemDialogs.SelectSaveFile(
            Lang.Text("Setup.Log.ExportSaveTitle"),
            fileName,
            filter,
            desktopPath);
        if (string.IsNullOrEmpty(selectedPath))
            return;

        var written = await RunAsync(ct => Task.Run(
            () => WriteZip(selectedPath, tempDirName, sourceFiles),
            ct));
        if (written)
            HintService.Show(Hints.Success(Lang.Text("Setup.Log.ExportSuccess")));
    }

    private List<SetupLogEntry> BuildEntries()
    {
        var result = new List<SetupLogEntry>();
        var current = CurrentLogs;
        var logFiles = Directory
            .GetFiles(LogDirectory)
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();
        foreach (var item in logFiles)
        {
            var fullPath = Path.GetFullPath(item);
            var title = Path.GetFileName(item);
            if (title.StartsWith("Launch"))
            {
                title = title.Substring(7, title.Length - 11);
                if (DateTime.TryParseExact(
                        title,
                        "yyyy-M-d-HHmmssfff",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                    title = Lang.Date(dt);
                if (current.Any(log => log.Equals(fullPath)))
                    title = Lang.Text("Setup.Log.CurrentSuffix", title);
            }
            else if (title.StartsWith("LastPending"))
            {
                title = title.Substring(11, title.Length - 15);
                title = title.Length > 1
                    ? Lang.Text("Setup.Log.TempStored", title.Substring(1))
                    : Lang.Text("Setup.Log.TempUnoutput");
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
            LauncherCommon.Log(
                ex,
                "删除非当前日志文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
            return false;
        }
    }

    private static bool WriteZip(
        string selectedPath,
        string tempDirName,
        IEnumerable<string> sourceFiles)
    {
        try
        {
            Directory.CreateDirectory(tempDirName);
            if (File.Exists(selectedPath))
                File.Delete(selectedPath);
            using var zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create);
            foreach (var item in sourceFiles)
            {
                var itemFileName = Path.GetFileName(item);
                var tempPath = Path.Combine(tempDirName, itemFileName);
                File.Copy(item, tempPath, true);
                zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest);
                File.Delete(tempPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
            return false;
        }
        finally
        {
            if (Directory.Exists(tempDirName))
                Directory.Delete(tempDirName, true);
        }
    }
}

/// <summary>「日志」页列表中的一条日志文件。</summary>
public sealed record SetupLogEntry(string Title, string FilePath);
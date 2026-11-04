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
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>日志设置页：列出日志文件、导出当前/全部日志、打开日志目录与清理非活动日志。</summary>
public partial class PageSetupLog : MyPageRight
{
    private bool _loaded;

    public PageSetupLog()
    {
        InitializeComponent();
        PanScroll = PanBack;

        BtnOpenDir.Click += (_, _) => Basics.OpenPath(LogService.Logger.Configuration.StoreFolder);
        BtnExport.Click += async (_, _) => await ExportCurrentAsync();
        BtnExportAll.Click += async (_, _) => await ExportAllAsync();
        BtnClear.Click += async (_, _) => await CleanAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            LoadList();
        };
    }

    private static IReadOnlyList<string> CurrentLogs =>
        LogService.Logger.CurrentLogFiles.Select(Path.GetFullPath).ToList();

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        LoadList();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        if (_loaded) LoadList();
    }

    private void LoadList()
    {
        try
        {
            PanList.Children.Clear();
            foreach (var entry in BuildEntries())
            {
                var item = new MyListItem
                {
                    Type = MyListItem.CheckType.Clickable,
                    Title = entry.Title,
                    Info = entry.FilePath
                };
                var file = entry.FilePath;
                item.Click += (_, _) => Basics.OpenPath(file);
                PanList.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "加载日志列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private static List<SetupLogEntry> BuildEntries()
    {
        var result = new List<SetupLogEntry>();
        var current = CurrentLogs;
        var storeFolder = LogService.Logger.Configuration.StoreFolder;
        if (!Directory.Exists(storeFolder)) return result;
        var logFiles = Directory
            .GetFiles(storeFolder)
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

    private async Task CleanAsync()
    {
        var result = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Setup.Log.Clear.Confirm.Title"),
                Content = Lang.Text("Setup.Log.Clear.Confirm.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        if (result.Button != 1) return;
        try
        {
            var currentSet = new HashSet<string>(CurrentLogs);
            var storeFolder = LogService.Logger.Configuration.StoreFolder;
            foreach (var item in Directory.GetFiles(storeFolder))
                if (!currentSet.Contains(item))
                    File.Delete(item);
            HintService.Show(Hints.Success(Lang.Text("Setup.Log.Clear.Success")));
            LoadList();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "删除非当前日志文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private async Task ExportAllAsync()
    {
        try
        {
            var files = Directory.GetFiles(LogService.Logger.Configuration.StoreFolder);
            await ExportAsync(files);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }

    private async Task ExportCurrentAsync()
    {
        try
        {
            var storeFolder = LogService.Logger.Configuration.StoreFolder;
            var files = CurrentLogs
                .Concat(
                    Directory
                        .GetFiles(storeFolder)
                        .Where(s => s.IsMatch(RegexPatterns.LastPendingLogPath)))
                .ToList();
            await ExportAsync(files);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }

    private async Task ExportAsync(IReadOnlyList<string> sourceFiles)
    {
        var filter = Lang.Text("Setup.Log.ExportFilter");
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var baseName = "PCL_CE_Logs_"
            + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var fileName = baseName + ".zip";
        var selectedPath = SystemDialogs.SelectSaveFile(
            Lang.Text("Setup.Log.ExportSaveTitle"),
            fileName,
            filter,
            desktopPath);
        if (string.IsNullOrEmpty(selectedPath)) return;

        await Task.Run(() =>
        {
            var tempDirName = baseName + ".tmp";
            try
            {
                Directory.CreateDirectory(tempDirName);
                if (File.Exists(selectedPath)) File.Delete(selectedPath);
                using var zip = ZipFile.Open(selectedPath, ZipArchiveMode.Create);
                foreach (var item in sourceFiles)
                {
                    var itemFileName = Path.GetFileName(item);
                    var tempPath = Path.Combine(tempDirName, itemFileName);
                    File.Copy(item, tempPath, true);
                    zip.CreateEntryFromFile(tempPath, itemFileName, CompressionLevel.Fastest);
                    File.Delete(tempPath);
                }
            }
            finally
            {
                if (Directory.Exists(tempDirName)) Directory.Delete(tempDirName, true);
            }
        });
        HintService.Show(Hints.Success(Lang.Text("Setup.Log.ExportSuccess")));
    }
}

/// <summary>日志列表中的一条日志文件。</summary>
internal sealed class SetupLogEntry(string title, string filePath)
{
    public string Title { get; } = title;

    public string FilePath { get; } = filePath;
}
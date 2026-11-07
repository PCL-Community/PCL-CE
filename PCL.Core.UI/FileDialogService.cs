using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于 Avalonia <see cref="IStorageProvider" /> 的文件与文件夹对话框实现。</summary>
public sealed class FileDialogService : IFileDialogService
{
    public bool IsAvailable => _GetStorageProvider() is not null || _HasLinuxDialogTool();

    public string SelectSaveFile(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null)
    {
        var provider = _GetStorageProvider();
        if (provider is null)
        {
            var fallback = _TryExternalSaveFile(title, fileName, fileFilter, initialDirectory);
            return fallback ?? "";
        }

        var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
        var options = new FilePickerSaveOptions
        {
            Title = dialogTitle,
            SuggestedFileName =
                string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName),
            FileTypeChoices =
                _ParseFilter(fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter")),
            ShowOverwritePrompt = true,
            SuggestedStartLocation = _TryGetFolder(provider, initialDirectory)
        };

        LogWrapper.Info("Dialog", $"打开保存文件对话框：{dialogTitle}");
        var file = SyncHelper.Wait(provider.SaveFilePickerAsync(options));
        if (file is null)
        {
            LogWrapper.Info("Dialog", "选择文件被取消");
            return "";
        }

        var selectedPath = _GetLocalPath(file);
        LogWrapper.Info("Dialog", $"选择文件返回：{selectedPath}");
        return string.IsNullOrEmpty(selectedPath) ? "" : Path.GetFullPath(selectedPath);
    }

    public string SelectFile(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null)
    {
        var result = SelectFiles(fileFilter, title, initialDirectory, false);
        return result.Length == 0 ? "" : result[0];
    }

    public string[] SelectFiles(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true)
    {
        var provider = _GetStorageProvider();
        if (provider is null)
            return _TryExternalOpenFiles(
                title,
                fileFilter,
                initialDirectory,
                allowMultiSelect);

        var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
        var options = new FilePickerOpenOptions
        {
            Title = dialogTitle,
            AllowMultiple = allowMultiSelect,
            FileTypeFilter =
                _ParseFilter(fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter")),
            SuggestedStartLocation = _TryGetFolder(provider, initialDirectory)
        };

        var num = allowMultiSelect ? "多" : "单";
        LogWrapper.Info("Dialog", $"打开选择{num}个文件对话框: {dialogTitle}");
        var files = SyncHelper.Wait(provider.OpenFilePickerAsync(options));
        if (files is null || files.Count == 0)
        {
            LogWrapper.Info("Dialog", "选择文件被取消");
            return [];
        }

        var selectedFiles = files
            .Select(_GetLocalPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => Path.GetFullPath(path!))
            .ToArray();
        LogWrapper.Info("Dialog", $"选择{num}个文件返回: {string.Join(",", selectedFiles)}");
        return selectedFiles;
    }

    public string SelectFolder(string? title = null, string? initialDirectory = null)
    {
        var provider = _GetStorageProvider();
        if (provider is null)
        {
            var fallback = _TryExternalFolder(title, initialDirectory);
            return fallback ?? "";
        }

        var dialogTitle = title ?? Lang.Text("SystemDialog.Folder.SelectTitle");
        var options = new FolderPickerOpenOptions
        {
            Title = dialogTitle,
            AllowMultiple = false,
            SuggestedStartLocation = _TryGetFolder(provider, initialDirectory)
        };

        LogWrapper.Info("Dialog", $"打开选择文件夹对话框: {dialogTitle}");
        var folders = SyncHelper.Wait(provider.OpenFolderPickerAsync(options));
        var selectedPath = folders is { Count: > 0 } ? _GetLocalPath(folders[0]) : null;
        if (string.IsNullOrEmpty(selectedPath))
        {
            LogWrapper.Info("Dialog", "选择文件夹被取消");
            return "";
        }

        var normalizedPath = Path.GetFullPath(selectedPath!).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        LogWrapper.Info("Dialog", $"选择文件夹返回: {normalizedPath}");
        return normalizedPath;
    }

    private static IStorageProvider? _GetStorageProvider()
    {
        var window = _GetMainWindow();
        return window is null ? null : TopLevel.GetTopLevel(window)?.StorageProvider;
    }

    private static Window? _GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            lifetime)
            return lifetime.MainWindow ?? lifetime.Windows.FirstOrDefault();
        return null;
    }

    private static string? _GetLocalPath(IStorageItem? item)
    {
        var uri = item?.Path;
        return uri is { IsAbsoluteUri: true, Scheme: "file" } ? uri.LocalPath : null;
    }

    private static IStorageFolder? _TryGetFolder(
        IStorageProvider provider,
        string? initialDirectory)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
            return null;

        try
        {
            return SyncHelper.Wait(
                provider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(initialDirectory))));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>把 WPF 风格的文件过滤器（<c>说明|*.ext;*.ext2|...</c>）转换为 Avalonia 的文件类型。</summary>
    private static IReadOnlyList<FilePickerFileType>? _ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;

        var parts = filter.Split('|');
        var result = new List<FilePickerFileType>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (patterns.Length == 0) continue;

            result.Add(
                new FilePickerFileType(parts[i])
                {
                    Patterns = patterns
                });
        }

        return result.Count == 0 ? null : result;
    }

    #region Linux 无 portal 回退

    private static string? _TryExternalSaveFile(
        string? title,
        string fileName,
        string? fileFilter,
        string? initialDirectory)
    {
        if (!OperatingSystem.IsLinux()) return null;

        var suggested = Path.Combine(
            string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory,
            string.IsNullOrWhiteSpace(fileName) ? "" : Path.GetFileName(fileName));

        if (_CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getsavefilename");
            args.Add(string.IsNullOrWhiteSpace(suggested) ? initialDirectory ?? "" : suggested);
            var filter = _ToKDialogFilter(fileFilter);
            if (filter is not null) args.Add(filter);
            var result = _RunDialog("kdialog", args);
            if (result.Length > 0) return result[0];
        }

        if (_CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            args.Add("--save");
            args.Add("--confirm-overwrite");
            if (!string.IsNullOrWhiteSpace(suggested)) args.Add("--filename=" + suggested);
            foreach (var filter in _ToZenityFilters(fileFilter))
                args.Add("--file-filter=" + filter);
            var result = _RunDialog("zenity", args);
            if (result.Length > 0) return result[0];
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return null;
    }

    private static string[] _TryExternalOpenFiles(
        string? title,
        string? fileFilter,
        string? initialDirectory,
        bool allowMultiSelect)
    {
        if (!OperatingSystem.IsLinux()) return [];

        if (_CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getopenfilename");
            args.Add(string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory);
            var filter = _ToKDialogFilter(fileFilter);
            if (filter is not null) args.Add(filter);
            if (allowMultiSelect)
            {
                args.Add("--multiple");
                args.Add("--separate-output");
            }

            var result = _RunDialog("kdialog", args);
            if (result.Length > 0) return result;
        }

        if (_CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            if (allowMultiSelect) args.Add("--multiple");
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                args.Add("--filename=" + initialDirectory + Path.DirectorySeparatorChar);
            foreach (var filter in _ToZenityFilters(fileFilter))
                args.Add("--file-filter=" + filter);
            var result = _RunDialog("zenity", args);
            if (result.Length > 0) return result;
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return [];
    }

    private static string? _TryExternalFolder(string? title, string? initialDirectory)
    {
        if (!OperatingSystem.IsLinux()) return null;

        if (_CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getexistingdirectory");
            args.Add(string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory);
            var result = _RunDialog("kdialog", args);
            if (result.Length > 0) return result[0];
        }

        if (_CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            args.Add("--directory");
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                args.Add("--filename=" + initialDirectory + Path.DirectorySeparatorChar);
            var result = _RunDialog("zenity", args);
            if (result.Length > 0) return result[0];
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件夹选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return null;
    }

    private static void AddTitle(ICollection<string> args, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        args.Add("--title");
        args.Add(title);
    }

    private static string? _ToKDialogFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;
        var parts = filter.Split('|');
        if (parts.Length < 2) return null;
        var patterns = parts[1].Replace(';', ' ').Trim();
        return patterns.Length == 0 ? null : $"{patterns}|{parts[0]}";
    }

    private static IEnumerable<string> _ToZenityFilters(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) yield break;
        var parts = filter.Split('|');
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1].Replace(';', ' ').Trim();
            if (patterns.Length > 0)
                yield return $"{parts[i]} | {patterns}";
        }
    }

    private static bool _HasLinuxDialogTool()
    {
        return OperatingSystem.IsLinux()
            && (_CommandExists("kdialog") || _CommandExists("zenity"));
    }

    private static bool _CommandExists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return false;
        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, command))
            .Any(File.Exists);
    }

    private static string[] _RunDialog(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null) return [];
            var errorTask = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            _ = errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0) return [];
            return output.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Dialog", $"{fileName} 文件选择器不可用：{ex.Message}");
            return [];
        }
    }

    #endregion
}
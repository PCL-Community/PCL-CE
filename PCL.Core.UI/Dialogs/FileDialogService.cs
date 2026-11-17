using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Avalonia StorageProvider 的共享文件对话框逻辑。</summary>
public class FileDialogService : IFileDialogService
{
    public virtual bool IsAvailable => GetStorageProvider() is not null;

    public virtual Task<string> SelectSaveFileAsync(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = GetStorageProvider();
            if (provider is null) return "";

            var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
            var options = new FilePickerSaveOptions
            {
                Title = dialogTitle,
                SuggestedFileName =
                    string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName),
                FileTypeChoices =
                    ParseFilter(fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter")),
                ShowOverwritePrompt = true,
                SuggestedStartLocation =
                    await TryGetFolderAsync(provider, initialDirectory, cancellationToken)
            };

            LogWrapper.Info("Dialog", $"打开保存文件对话框：{dialogTitle}");
            var file = await provider.SaveFilePickerAsync(options);
            cancellationToken.ThrowIfCancellationRequested();
            if (file is null)
            {
                LogWrapper.Info("Dialog", "选择文件被取消");
                return "";
            }

            var selectedPath = GetLocalPath(file);
            LogWrapper.Info("Dialog", $"选择文件返回：{selectedPath}");
            return string.IsNullOrEmpty(selectedPath) ? "" : Path.GetFullPath(selectedPath);
        });
    }

    public virtual async Task<string> SelectFileAsync(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var result = await SelectFilesAsync(
                fileFilter,
                title,
                initialDirectory,
                false,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Length == 0 ? "" : result[0];
    }

    public virtual Task<string[]> SelectFilesAsync(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true,
        CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = GetStorageProvider();
            if (provider is null) return [];

            var dialogTitle = title ?? Lang.Text("SystemDialog.File.SelectTitle");
            var options = new FilePickerOpenOptions
            {
                Title = dialogTitle,
                AllowMultiple = allowMultiSelect,
                FileTypeFilter =
                    ParseFilter(fileFilter ?? Lang.Text("SystemDialog.File.AllFilesFilter")),
                SuggestedStartLocation =
                    await TryGetFolderAsync(provider, initialDirectory, cancellationToken)
            };

            var countText = allowMultiSelect ? "多" : "单";
            LogWrapper.Info("Dialog", $"打开选择{countText}个文件对话框: {dialogTitle}");
            var files = await provider.OpenFilePickerAsync(options);
            cancellationToken.ThrowIfCancellationRequested();
            if (files is null || files.Count == 0)
            {
                LogWrapper.Info("Dialog", "选择文件被取消");
                return [];
            }

            var selectedFiles = files
                .Select(GetLocalPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFullPath(path!))
                .ToArray();
            LogWrapper.Info(
                "Dialog",
                $"选择{countText}个文件返回: {string.Join(",", selectedFiles)}");
            return selectedFiles;
        });
    }

    public virtual Task<string> SelectFolderAsync(
        string? title = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        return RunOnUiAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provider = GetStorageProvider();
            if (provider is null) return "";

            var dialogTitle = title ?? Lang.Text("SystemDialog.Folder.SelectTitle");
            var options = new FolderPickerOpenOptions
            {
                Title = dialogTitle,
                AllowMultiple = false,
                SuggestedStartLocation =
                    await TryGetFolderAsync(provider, initialDirectory, cancellationToken)
            };

            LogWrapper.Info("Dialog", $"打开选择文件夹对话框: {dialogTitle}");
            var folders = await provider.OpenFolderPickerAsync(options);
            cancellationToken.ThrowIfCancellationRequested();
            var selectedPath = folders is { Count: > 0 } ? GetLocalPath(folders[0]) : null;
            if (string.IsNullOrEmpty(selectedPath))
            {
                LogWrapper.Info("Dialog", "选择文件夹被取消");
                return "";
            }

            var normalizedPath = Path
                    .GetFullPath(selectedPath!)
                    .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            LogWrapper.Info("Dialog", $"选择文件夹返回: {normalizedPath}");
            return normalizedPath;
        });
    }

    /// <summary>确保整个 StorageProvider 交互都在 UI 线程执行。</summary>
    private static async Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return await action();

        return await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            lifetime)
            return null;
        var window = lifetime.MainWindow ?? lifetime.Windows.FirstOrDefault();
        return window is null ? null : TopLevel.GetTopLevel(window)?.StorageProvider;
    }

    private static string? GetLocalPath(IStorageItem? item)
    {
        var uri = item?.Path;
        return uri is { IsAbsoluteUri: true, Scheme: "file" } ? uri.LocalPath : null;
    }

    private static async Task<IStorageFolder?> TryGetFolderAsync(
        IStorageProvider provider,
        string? initialDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(initialDirectory) || !Directory.Exists(initialDirectory))
            return null;

        try
        {
            return await provider.TryGetFolderFromPathAsync(
                new Uri(Path.GetFullPath(initialDirectory)));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>把 WPF 风格的文件过滤器转换为 Avalonia 文件类型。</summary>
    protected static IReadOnlyList<FilePickerFileType>? ParseFilter(string? filter)
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
}
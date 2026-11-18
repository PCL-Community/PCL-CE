using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Linux 文件对话框：优先 portal，缺失时回退 kdialog/zenity。</summary>
public sealed class LinuxFileDialogService(IExternalCommandRunner commandRunner) : FileDialogService
{
    private readonly IExternalCommandRunner _commandRunner =
        commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));

    public override bool IsAvailable =>
        GetStorageProvider() is not null
        || CommandExists("kdialog")
        || CommandExists("zenity");

    public override async Task<string> SelectSaveFileAsync(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (GetStorageProvider() is not null)
            return await base.SelectSaveFileAsync(
                title,
                fileName,
                fileFilter,
                initialDirectory,
                cancellationToken);

        var suggested = Path.Combine(
            string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory,
            string.IsNullOrWhiteSpace(fileName) ? "" : Path.GetFileName(fileName));

        if (CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getsavefilename");
            args.Add(string.IsNullOrWhiteSpace(suggested) ? initialDirectory ?? "" : suggested);
            var filter = ToKDialogFilter(fileFilter);
            if (filter is not null) args.Add(filter);
            var result = await RunDialogAsync("kdialog", args, cancellationToken);
            if (result.Length > 0) return result[0];
        }

        if (CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            args.Add("--save");
            args.Add("--confirm-overwrite");
            if (!string.IsNullOrWhiteSpace(suggested)) args.Add("--filename=" + suggested);
            args.AddRange(
                ToZenityFilters(fileFilter).Select(filter => "--file-filter=" + filter));
            var result = await RunDialogAsync("zenity", args, cancellationToken);
            if (result.Length > 0) return result[0];
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return "";
    }

    public override async Task<string[]> SelectFilesAsync(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true,
        CancellationToken cancellationToken = default)
    {
        if (GetStorageProvider() is not null)
            return await base.SelectFilesAsync(
                fileFilter,
                title,
                initialDirectory,
                allowMultiSelect,
                cancellationToken);

        if (CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getopenfilename");
            args.Add(string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory);
            var filter = ToKDialogFilter(fileFilter);
            if (filter is not null) args.Add(filter);
            if (allowMultiSelect)
            {
                args.Add("--multiple");
                args.Add("--separate-output");
            }

            var result = await RunDialogAsync("kdialog", args, cancellationToken);
            if (result.Length > 0) return result;
        }

        if (CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            if (allowMultiSelect) args.Add("--multiple");
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                args.Add("--filename=" + initialDirectory + Path.DirectorySeparatorChar);
            args.AddRange(
                ToZenityFilters(fileFilter).Select(filter => "--file-filter=" + filter));
            var result = await RunDialogAsync("zenity", args, cancellationToken);
            if (result.Length > 0) return result;
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return [];
    }

    public override async Task<string> SelectFolderAsync(
        string? title = null,
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (GetStorageProvider() is not null)
            return await base.SelectFolderAsync(
                title,
                initialDirectory,
                cancellationToken);

        if (CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getexistingdirectory");
            args.Add(string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory);
            var result = await RunDialogAsync("kdialog", args, cancellationToken);
            if (result.Length > 0) return result[0];
        }

        if (CommandExists("zenity"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--file-selection");
            args.Add("--directory");
            if (!string.IsNullOrWhiteSpace(initialDirectory))
                args.Add("--filename=" + initialDirectory + Path.DirectorySeparatorChar);
            var result = await RunDialogAsync("zenity", args, cancellationToken);
            if (result.Length > 0) return result[0];
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件夹选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return "";
    }

    private static void AddTitle(ICollection<string> args, string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        args.Add("--title");
        args.Add(title);
    }

    private static string? ToKDialogFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return null;
        var parts = filter.Split('|');
        if (parts.Length < 2) return null;
        var patterns = parts[1].Replace(';', ' ').Trim();
        return patterns.Length == 0 ? null : $"{patterns}|{parts[0]}";
    }

    private static IEnumerable<string> ToZenityFilters(string? filter)
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

    private bool CommandExists(string command)
    {
        return _commandRunner.ExistsOnPath(command);
    }

    private async Task<string[]> RunDialogAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _commandRunner.RunAsync(
                fileName,
                arguments,
                TimeSpan.Zero,
                cancellationToken);
            if (!result.Succeeded) return [];
            return result.StandardOutput.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Dialog", $"{fileName} 文件选择器不可用：{ex.Message}");
            return [];
        }
    }
}
using System.Diagnostics;
using PCL.Core.Logging;

namespace PCL.Core.UI;

/// <summary>Linux 文件对话框：优先 portal，缺失时回退 kdialog/zenity。</summary>
public sealed class LinuxFileDialogService : FileDialogService
{
    public override bool IsAvailable =>
        GetStorageProvider() is not null
        || CommandExists("kdialog")
        || CommandExists("zenity");

    public override string SelectSaveFile(
        string? title,
        string fileName,
        string? fileFilter = null,
        string? initialDirectory = null)
    {
        if (GetStorageProvider() is not null)
            return base.SelectSaveFile(title, fileName, fileFilter, initialDirectory);

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
            var result = RunDialog("kdialog", args);
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
            args.AddRange(ToZenityFilters(fileFilter).Select(filter => "--file-filter=" + filter));
            var result = RunDialog("zenity", args);
            if (result.Length > 0) return result[0];
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return "";
    }

    public override string[] SelectFiles(
        string? fileFilter = null,
        string? title = null,
        string? initialDirectory = null,
        bool allowMultiSelect = true)
    {
        if (GetStorageProvider() is not null)
            return base.SelectFiles(fileFilter, title, initialDirectory, allowMultiSelect);

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

            var result = RunDialog("kdialog", args);
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
            args.AddRange(ToZenityFilters(fileFilter).Select(filter => "--file-filter=" + filter));
            var result = RunDialog("zenity", args);
            if (result.Length > 0) return result;
        }

        LogWrapper.Warn("Dialog", "没有可用的 Linux 文件选择器（Avalonia portal/kdialog/zenity 均不可用）");
        return [];
    }

    public override string SelectFolder(string? title = null, string? initialDirectory = null)
    {
        if (GetStorageProvider() is not null)
            return base.SelectFolder(title, initialDirectory);

        if (CommandExists("kdialog"))
        {
            var args = new List<string>();
            AddTitle(args, title);
            args.Add("--getexistingdirectory");
            args.Add(string.IsNullOrWhiteSpace(initialDirectory) ? "" : initialDirectory);
            var result = RunDialog("kdialog", args);
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
            var result = RunDialog("zenity", args);
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

    private static bool CommandExists(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return false;
        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, command))
            .Any(File.Exists);
    }

    private static string[] RunDialog(string fileName, IReadOnlyList<string> arguments)
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
}
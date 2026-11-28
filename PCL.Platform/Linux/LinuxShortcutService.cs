using System.Diagnostics;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class LinuxShortcutService : IShortcutService
{
    public string GetPreferredExtension(string target)
    {
        return ".desktop";
    }

    public string GetDesktopPath(string fileName)
    {
        return Path.Combine(XdgPaths.DesktopDirectory, fileName);
    }

    public string GetStartMenuPath(string fileName)
    {
        return Path.Combine(XdgPaths.DataHome, "applications", fileName);
    }

    public void Create(
        string shortcutPath,
        string target,
        string? arguments = null,
        string? workingDirectory = null,
        string? description = null,
        string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath))
            throw new ArgumentException("shortcutPath 不能为空", nameof(shortcutPath));
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("targetFilePath 不能为空", nameof(target));

        var desktopFile = shortcutPath.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)
            ? shortcutPath
            : Path.ChangeExtension(shortcutPath, ".desktop");
        var desktopDir = Path.GetDirectoryName(desktopFile);
        if (!string.IsNullOrEmpty(desktopDir)) Directory.CreateDirectory(desktopDir);

        var workingDir = string.IsNullOrEmpty(workingDirectory)
            ? Path.GetDirectoryName(target)
            ?? Path.GetPathRoot(target) ?? Environment.CurrentDirectory
            : workingDirectory;
        var name = Path.GetFileNameWithoutExtension(shortcutPath);
        if (string.IsNullOrWhiteSpace(name)) name = "PCL CE";

        var content = LinuxDesktopEntryFile.Build(
            name,
            description,
            target,
            arguments,
            workingDir,
            string.IsNullOrWhiteSpace(icon) ? null : icon.Trim(),
            LinuxDesktopEntryFile.PclCeAppId);
        File.WriteAllText(desktopFile, content, new UTF8Encoding(false));
        File.SetUnixFileMode(
            desktopFile,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute);
        TryMarkTrusted(desktopFile);
        LogWrapper.Info("Files", $"已创建桌面快捷方式：{desktopFile}");
    }

    private static void TryMarkTrusted(string desktopFile)
    {
        try
        {
            if (!_IsGnomeDesktop() || !CommandLocator.ExistsOnPath("gio")) return;
            var desktopDir = Path.TrimEndingDirectorySeparator(XdgPaths.DesktopDirectory);
            var fileDir = Path.GetDirectoryName(Path.GetFullPath(desktopFile));
            if (!string.Equals(desktopDir, fileDir, StringComparison.Ordinal)) return;

            var info = new ProcessStartInfo("gio")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            info.ArgumentList.Add("set");
            info.ArgumentList.Add(desktopFile);
            info.ArgumentList.Add("metadata::trusted");
            info.ArgumentList.Add("true");

            using var process = Process.Start(info);
            if (process is null) return;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // 进程可能刚好退出。
                }

                return;
            }

            _ = outputTask.GetAwaiter().GetResult();
            if (process.ExitCode == 0) return;
            var error = errorTask.GetAwaiter().GetResult().Trim();
            if (error.Length > 0)
                LogWrapper.Debug("Files", $"设置桌面快捷方式信任状态失败：{error}");
        }
        catch (Exception ex)
        {
            LogWrapper.Debug("Files", $"设置桌面快捷方式信任状态失败：{ex.Message}");
        }
    }

    private static bool _IsGnomeDesktop()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (string.IsNullOrWhiteSpace(desktop))
            desktop = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        return !string.IsNullOrWhiteSpace(desktop)
            && (desktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase)
                || desktop.Contains("Unity", StringComparison.OrdinalIgnoreCase)
                || desktop.Contains("Cinnamon", StringComparison.OrdinalIgnoreCase));
    }
}
using System;
using System.IO;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class LinuxShortcutService : IShortcutService
{
    public string GetPreferredExtension(string target)
    {
        return ".desktop";
    }

    public string GetDesktopPath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            fileName);
    }

    public string GetStartMenuPath(string fileName)
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        return Path.Combine(dataHome, "applications", fileName);
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
        var name = string.IsNullOrWhiteSpace(description)
            ? Path.GetFileNameWithoutExtension(shortcutPath)
            : description;

        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Type=Application");
        builder.Append("Name=").AppendLine(_EscapeValue(name));
        if (!string.IsNullOrWhiteSpace(description))
            builder.Append("Comment=").AppendLine(_EscapeValue(description));
        builder.Append("Exec=").AppendLine(_BuildExec(target, arguments));
        builder.Append("Path=").AppendLine(_EscapeValue(workingDir));
        if (!string.IsNullOrWhiteSpace(icon))
        {
            var iconPath = icon.Split(',')[0].Trim();
            if (iconPath.Length > 0) builder.Append("Icon=").AppendLine(_EscapeValue(iconPath));
        }

        builder.AppendLine("Terminal=false");
        File.WriteAllText(desktopFile, builder.ToString());
        File.SetUnixFileMode(
            desktopFile,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute);
        LogWrapper.Info("Files", $"已创建桌面快捷方式：{desktopFile}");
    }

    private static string _EscapeValue(string value)
    {
        return value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string _BuildExec(string target, string? arguments)
    {
        var escaped = target
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("$", "\\$")
            .Replace("`", "\\`");
        return string.IsNullOrWhiteSpace(arguments)
            ? $"\"{escaped}\""
            : $"\"{escaped}\" {arguments}";
    }
}
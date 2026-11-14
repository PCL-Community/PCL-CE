using System;
using System.IO;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class WindowsShortcutService : IShortcutService
{
    public string GetPreferredExtension(string target)
    {
        return ".lnk";
    }

    public string GetDesktopPath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            fileName);
    }

    public string GetStartMenuPath(string fileName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            fileName);
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

        var dir = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var shellType = Type.GetTypeFromProgID("WScript.Shell", true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        var link = shell.CreateShortcut(shortcutPath)!;
        link.TargetPath = target;
        if (!string.IsNullOrEmpty(arguments)) link.Arguments = arguments;
        link.WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
            ? Path.GetDirectoryName(target) ?? Path.GetPathRoot(target)
            : workingDirectory;
        if (!string.IsNullOrEmpty(description)) link.Description = description;
        if (!string.IsNullOrEmpty(icon)) link.IconLocation = icon;
        link.Save();
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using PCL.Core.Logging;

namespace PCL.Core.IO;

public static partial class Files
{
    /// <summary>
    ///     在指定路径创建一个指向目标文件的快捷方式：Windows 为 .lnk，Linux 为 .desktop。
    /// </summary>
    /// <param name="shortcut">要创建的快捷方式完整路径，Windows 建议以 ".lnk" 结尾</param>
    /// <param name="target">被指向的目标文件或可执行程序路径</param>
    /// <param name="arguments">启动时的命令行参数</param>
    /// <param name="workingDirectory">快捷方式的起始目录</param>
    /// <param name="description">快捷方式说明</param>
    /// <param name="icon">自定义图标，格式 "图标文件路径,索引"</param>
    public static void CreateShortcut(
        string shortcut,
        string target,
        string? arguments = null,
        string? workingDirectory = null,
        string? description = null,
        string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            throw new ArgumentException("shortcutPath 不能为空", nameof(shortcut));
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("targetFilePath 不能为空", nameof(target));

        if (!OperatingSystem.IsWindows())
        {
            if (OperatingSystem.IsMacOS())
            {
                _CreateMacShortcut(
                    shortcut,
                    target,
                    arguments,
                    workingDirectory,
                    description);
                return;
            }

            _CreateLinuxDesktopShortcut(
                shortcut,
                target,
                arguments,
                workingDirectory,
                description,
                icon);
            return;
        }

        // 确保目录存在
        var dir = Path.GetDirectoryName(shortcut);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // 与 WshShell 交互
        var shellType = Type.GetTypeFromProgID("WScript.Shell", true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        var link = shell.CreateShortcut(shortcut)!;

        // 设置属性
        link.TargetPath = target;
        if (!string.IsNullOrEmpty(arguments)) link.Arguments = arguments;
        if (!string.IsNullOrEmpty(workingDirectory)) link.WorkingDirectory = workingDirectory;
        else link.WorkingDirectory = Path.GetDirectoryName(target) ?? Path.GetPathRoot(target);
        if (!string.IsNullOrEmpty(description)) link.Description = description;
        if (!string.IsNullOrEmpty(icon)) link.IconLocation = icon;

        // 保存 .lnk 文件
        link.Save();
    }

    /// <summary>
    ///     macOS 快捷方式：优先为 .app 创建 Finder alias；裸可执行文件则写可执行 .command 启动器。
    /// </summary>
    [UnsupportedOSPlatform("windows")]
    private static void _CreateMacShortcut(
        string shortcut,
        string target,
        string? arguments,
        string? workingDirectory,
        string? description)
    {
        var appBundle = FindMacAppBundle(target);
        if (appBundle is not null)
        {
            var destinationDirectory = Path.GetDirectoryName(shortcut);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
                destinationDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);
            Directory.CreateDirectory(destinationDirectory);

            var aliasName = Path.GetFileName(shortcut);
            var script =
                "tell application \"Finder\"\n"
                + $"  set theAlias to make alias file to POSIX file \"{_EscapeAppleScript(appBundle)}\" "
                + $"at POSIX file \"{_EscapeAppleScript(destinationDirectory)}\"\n"
                + $"  set name of theAlias to \"{_EscapeAppleScript(aliasName)}\"\n"
                + "end tell";

            var startInfo = new ProcessStartInfo
            {
                FileName = "osascript",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("-e");
            startInfo.ArgumentList.Add(script);

            using var process = Process.Start(startInfo);
            if (process is null)
                throw new PlatformNotSupportedException("无法启动 osascript 创建 Finder alias");

            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // 忽略超时后的清理失败
                }

                throw new IOException("创建 Finder alias 超时");
            }

            if (process.ExitCode != 0)
                throw new IOException(
                    $"创建 Finder alias 失败：{process.StandardError.ReadToEnd().Trim()}");
            LogWrapper.Info("Files", $"已创建 macOS Finder alias：{shortcut}");
            return;
        }

        var commandFile = shortcut.EndsWith(".command", StringComparison.OrdinalIgnoreCase)
            ? shortcut
            : Path.ChangeExtension(shortcut, ".command");
        var commandDirectory = Path.GetDirectoryName(commandFile);
        if (!string.IsNullOrEmpty(commandDirectory))
            Directory.CreateDirectory(commandDirectory);

        var builder = new StringBuilder();
        builder.AppendLine("#!/bin/sh");
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            builder.Append("cd ").AppendLine(_QuoteShell(workingDirectory));
        builder.Append("exec ").Append(_QuoteShell(target));
        if (!string.IsNullOrWhiteSpace(arguments))
            builder.Append(' ').Append(arguments);
        builder.AppendLine(" \"$@\"");
        File.WriteAllText(commandFile, builder.ToString());
        File.SetUnixFileMode(
            commandFile,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute);
        LogWrapper.Info("Files", $"已创建 macOS .command 快捷方式：{commandFile}");
    }

    /// <summary>从可执行文件路径向上查找所在的 .app bundle；找不到返回 null。</summary>
    public static string? FindMacAppBundle(string target)
    {
        if (!OperatingSystem.IsMacOS()) return null;
        try
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(target))!);
            while (directory is not null)
            {
                if (directory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }
        catch
        {
            // 路径异常时按裸可执行文件处理
        }

        return null;
    }

    private static string _EscapeAppleScript(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string _QuoteShell(string value)
    {
        return "\""
            + value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("$", "\\$", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal)
            + "\"";
    }

    /// <summary>在 Linux 写一个 XDG 桌面项（.desktop）。</summary>
    [UnsupportedOSPlatform("windows")]
    private static void _CreateLinuxDesktopShortcut(
        string shortcut,
        string target,
        string? arguments,
        string? workingDirectory,
        string? description,
        string? icon)
    {
        var desktopFile = shortcut.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase)
            ? shortcut
            : Path.ChangeExtension(shortcut, ".desktop");
        var desktopDir = Path.GetDirectoryName(desktopFile);
        if (!string.IsNullOrEmpty(desktopDir)) Directory.CreateDirectory(desktopDir);

        var workingDir = string.IsNullOrEmpty(workingDirectory)
            ? Path.GetDirectoryName(target)
            ?? Path.GetPathRoot(target) ?? Environment.CurrentDirectory
            : workingDirectory;
        var name = string.IsNullOrWhiteSpace(description)
            ? Path.GetFileNameWithoutExtension(shortcut)
            : description;

        var builder = new StringBuilder();
        builder.AppendLine("[Desktop Entry]");
        builder.AppendLine("Type=Application");
        builder.Append("Name=").AppendLine(_EscapeDesktopValue(name));
        if (!string.IsNullOrWhiteSpace(description))
            builder.Append("Comment=").AppendLine(_EscapeDesktopValue(description));
        builder.Append("Exec=").AppendLine(_BuildDesktopExec(target, arguments));
        builder.Append("Path=").AppendLine(_EscapeDesktopValue(workingDir));
        if (!string.IsNullOrWhiteSpace(icon))
        {
            // Windows 的图标格式是 "路径,索引"，desktop 项只接受路径
            var iconPath = icon.Split(',')[0].Trim();
            if (iconPath.Length > 0)
                builder.Append("Icon=").AppendLine(_EscapeDesktopValue(iconPath));
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

    private static string _EscapeDesktopValue(string value)
    {
        return value.Replace('\n', ' ').Replace('\r', ' ').Trim();
    }

    private static string _BuildDesktopExec(string target, string? arguments)
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
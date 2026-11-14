using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class MacOsShortcutService : IShortcutService
{
    public string GetPreferredExtension(string target)
    {
        return FindAppBundle(target) is not null ? ".app" : ".command";
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
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Applications",
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

        var appBundle = FindAppBundle(target);
        if (appBundle is not null)
        {
            var destinationDirectory = Path.GetDirectoryName(shortcutPath)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            Directory.CreateDirectory(destinationDirectory);

            var aliasName = Path.GetFileName(shortcutPath);
            var script = "tell application \"Finder\"\n"
                + $"  set theAlias to make alias file to POSIX file \"{_EscapeAppleScript(appBundle)}\" "
                + $"at POSIX file \"{_EscapeAppleScript(destinationDirectory)}\"\n"
                + $"  set name of theAlias to \"{_EscapeAppleScript(aliasName)}\"\n"
                + "end tell";
            var info = new ProcessStartInfo
            {
                FileName = "osascript",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            info.ArgumentList.Add("-e");
            info.ArgumentList.Add(script);

            using var process = Process.Start(info)
                ?? throw new PlatformNotSupportedException("无法启动 osascript 创建 Finder alias");

            if (!process.WaitForExit(10_000))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                    // ignored
                }

                throw new IOException("创建 Finder alias 超时");
            }

            if (process.ExitCode != 0)
                throw new IOException(
                    $"创建 Finder alias 失败：{process.StandardError.ReadToEnd().Trim()}");

            LogWrapper.Info("Files", $"已创建 macOS Finder alias：{shortcutPath}");
            return;
        }

        var commandFile = shortcutPath.EndsWith(".command", StringComparison.OrdinalIgnoreCase)
            ? shortcutPath
            : Path.ChangeExtension(shortcutPath, ".command");
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

    public static string? FindAppBundle(string target)
    {
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
}
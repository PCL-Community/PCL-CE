using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL.Platform;

public sealed class MacOsShortcutService : IShortcutService
{
    private static readonly ObjCSel DefaultManager =
        ObjCSel.FromString("defaultManager");

    private static readonly ObjCSel CreateSymbolicLink =
        ObjCSel.FromString("createSymbolicLinkAtPath:withDestinationPath:error:");

    private static readonly ObjCSel Release = ObjCSel.FromString("release");
    private static bool _frameworksLoaded;

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

            if (!TryCreateSymbolicLink(shortcutPath, appBundle))
                throw new IOException($"创建 macOS 快捷方式失败：{shortcutPath}");

            LogWrapper.Info("Files", $"已创建 macOS 应用快捷方式：{shortcutPath}");
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


    private static bool TryCreateSymbolicLink(string linkPath, string destinationPath)
    {
        EnsureFrameworks();

        ObjCString? linkString = null;
        ObjCString? destinationString = null;
        try
        {
            linkString = ObjCString.FromString(linkPath);
            destinationString = ObjCString.FromString(destinationPath);

            var fileManager = ObjCObject
                .GetClass("NSFileManager")
                .SendMessage<ObjCObject>(DefaultManager);
            if (fileManager.IsNull) return false;

            return fileManager.SendMessage<ObjCString, ObjCString, nint, bool>(
                CreateSymbolicLink,
                linkString.Value,
                destinationString.Value,
                0);
        }
        finally
        {
            if (linkString is { } linkValue) ReleaseString(linkValue);
            if (destinationString is { } destinationValue) ReleaseString(destinationValue);
        }
    }

    private static void EnsureFrameworks()
    {
        if (_frameworksLoaded) return;

        NativeLibrary.Load(
            "/System/Library/Frameworks/Foundation.framework/Foundation");
        NativeLibrary.Load(
            "/System/Library/Frameworks/AppKit.framework/AppKit");
        _frameworksLoaded = true;
    }

    private static void ReleaseString(ObjCString value)
    {
        if (!value.IsNull)
            new ObjCObject(value.Value).SendMessage(Release);
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
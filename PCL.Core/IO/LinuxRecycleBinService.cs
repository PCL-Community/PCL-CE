using System;
using System.IO;
using System.Text;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class LinuxRecycleBinService : IRecycleBinService
{
    public bool IsSupported(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && (File.Exists(path) || Directory.Exists(path))
            && _IsUnderHome(path);
    }

    public RecycleBinOutcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            error = "路径不存在";
            return RecycleBinOutcome.Failed;
        }

        if (!IsSupported(path)) return RecycleBinOutcome.Unsupported;

        var isDirectory = Directory.Exists(path);
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
            dataHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        var trashRoot = Path.Combine(dataHome, "Trash");
        var filesDir = Path.Combine(trashRoot, "files");
        var infoDir = Path.Combine(trashRoot, "info");
        Directory.CreateDirectory(filesDir);
        Directory.CreateDirectory(infoDir);

        var full = Path.GetFullPath(path);
        var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(name)) name = "item";
        var destination = _UniqueDestination(filesDir, name);
        var destinationName = Path.GetFileName(destination);

        try
        {
            if (isDirectory) Directory.Move(full, destination);
            else File.Move(full, destination);
        }
        catch (IOException ex)
        {
            error = ex.Message;
            return RecycleBinOutcome.Failed;
        }

        var infoPath = Path.Combine(infoDir, $"{destinationName}.trashinfo");
        try
        {
            var content = new StringBuilder()
                .AppendLine("[Trash Info]")
                .Append("Path=")
                .AppendLine(Uri.EscapeDataString(full).Replace("%2F", "/"))
                .Append("DeletionDate=")
                .AppendLine(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"))
                .ToString();
            File.WriteAllText(infoPath, content, new UTF8Encoding(false));
            return RecycleBinOutcome.Moved;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            _TryRollback(destination, full, isDirectory);
            try
            {
                if (File.Exists(infoPath)) File.Delete(infoPath);
            }
            catch
            {
                // ignored
            }

            return RecycleBinOutcome.Failed;
        }
    }

    private static bool _IsUnderHome(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return false;
        var full = Path.GetFullPath(path);
        return full.Length == home.Length
            ? string.Equals(full, home, StringComparison.Ordinal)
            : full.StartsWith(
                home.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal);
    }

    private static string _UniqueDestination(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var index = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = Path.Combine(directory, $"{name}.{index++}");
        return candidate;
    }

    private static void _TryRollback(string movedPath, string originalPath, bool isDirectory)
    {
        try
        {
            if (isDirectory) Directory.Move(movedPath, originalPath);
            else File.Move(movedPath, originalPath);
        }
        catch
        {
            // ignore
        }
    }
}
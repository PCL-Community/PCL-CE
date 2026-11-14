using System;
using System.IO;
using PCL.Core.Platform;

namespace PCL.Core.IO;

public sealed class MacOsRecycleBinService : IRecycleBinService
{
    public bool IsSupported(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
    }

    public RecycleBinOutcome Move(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            error = "路径不存在";
            return RecycleBinOutcome.Failed;
        }

        var isDirectory = Directory.Exists(path);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var trash = Path.Combine(home, ".Trash");
        Directory.CreateDirectory(trash);
        var destination = _UniqueDestination(
            trash,
            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));
        try
        {
            if (isDirectory) Directory.Move(path, destination);
            else File.Move(path, destination);
            return RecycleBinOutcome.Moved;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return RecycleBinOutcome.Failed;
        }
    }

    private static string _UniqueDestination(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var index = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = Path.Combine(directory, $"{name}.{index++}");
        return candidate;
    }
}
using System;
using System.IO;
using System.Linq;

namespace PCL.Core.Utils.OS;

/// <summary>按路径查找所在卷的可用空间。</summary>
public static class DiskSpaceInfo
{
    public static Volume? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            var drive = DriveInfo
                .GetDrives()
                .Where(candidate => candidate.IsReady)
                .OrderByDescending(candidate => candidate.RootDirectory.FullName.Length)
                .FirstOrDefault(candidate => _IsUnderRoot(
                    fullPath,
                    candidate.RootDirectory.FullName));
            if (drive is null)
            {
                var root = Path.GetPathRoot(fullPath);
                if (root is null) return null;
                drive = new DriveInfo(root);
            }

            return new Volume(
                drive.RootDirectory.FullName,
                drive.TotalSize,
                drive.AvailableFreeSpace);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool _IsUnderRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    public sealed record Volume(
        string Root,
        long TotalBytes,
        long FreeBytes);
}
using System;
using System.IO;
using System.Runtime.InteropServices;
using PCL.Core.App;

namespace PCL.Core.IO;

public static partial class Files
{
    public static bool ArePathsEqual(string path1, string path2)
    {
        var fullPath1 = Path
            .GetFullPath(path1)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath2 = Path
            .GetFullPath(path2)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath1, fullPath2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     检查指定路径是否在指定文件夹中
    /// </summary>
    /// <param name="childPath">预检查路径</param>
    /// <param name="baseDirectory">应存在文件夹</param>
    /// <returns></returns>
    public static bool IsPathWithinDirectory(string childPath, string baseDirectory)
    {
        var baseDir = Path.GetFullPath(baseDirectory);
        var child = Path.GetFullPath(childPath);

        return child.StartsWith(
            baseDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal
        );
    }

    /// <summary>
    ///     获取文件的完整路径。
    /// </summary>
    public static string GetFullPath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(Paths.DefaultDirectory, filePath);
    }
}
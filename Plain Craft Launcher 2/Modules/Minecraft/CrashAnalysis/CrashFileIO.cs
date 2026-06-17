using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.IO;
using PCL.Core.Utils.Codecs;

namespace PCL;

internal static class CrashFileIO
{
    public static byte[] ReadBytes(string filePath)
    {
        return Files.ReadAllBytesOrEmptyAsync(filePath).GetAwaiter().GetResult();
    }

    public static string ReadText(string filePath, Encoding? encoding = null)
    {
        var bytes = ReadBytes(filePath);
        return encoding is null
            ? EncodingUtils.DecodeBytes(bytes)
            : encoding.GetString(bytes);
    }

    public static void WriteText(string filePath, string text, Encoding? encoding = null)
    {
        Files.WriteFileAsync(filePath, text, encoding: encoding).GetAwaiter().GetResult();
    }

    public static void CopyFile(string fromPath, string toPath)
    {
        Files.CopyFileAsync(fromPath, toPath).GetAwaiter().GetResult();
    }

    public static void DeleteDirectory(string directoryPath)
    {
        Directories.DeleteDirectoryAsync(directoryPath, true).GetAwaiter().GetResult();
    }

    public static void ExtractFile(string archivePath, string destinationDirectory)
    {
        if (!archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("崩溃日志导入仅支持 zip 压缩包。");

        _EnsureZipEntriesStayInDirectory(archivePath, destinationDirectory);
        Files.ExtractFileAsync(archivePath, destinationDirectory).GetAwaiter().GetResult();
    }

    private static void _EnsureZipEntriesStayInDirectory(
        string archivePath,
        string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("压缩包中存在不安全的文件路径：" + entry.FullName);
        }
    }
}
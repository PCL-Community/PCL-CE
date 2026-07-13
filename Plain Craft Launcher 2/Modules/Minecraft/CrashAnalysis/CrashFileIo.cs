using System.IO;
using System.Text;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils.Codecs;

namespace PCL;

internal static class CrashFileIo
{
    public static byte[] ReadBytes(string filePath)
    {
        try
        {
            // 使用 FileShare.ReadWrite 以读取正在被 Logger 写入的文件
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyToAsync(ms).GetAwaiter().GetResult();
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash IO", "读取与游戏崩溃关联的日志文件时出错，");
            return Files.ReadAllBytesOrEmptyAsync(filePath).GetAwaiter().GetResult();
        }
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

    public static bool CanExtractArchive(string archivePath)
    {
        var fileName = Path.GetFileName(archivePath);
        return fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase);
    }

    public static void ExtractFile(string archivePath, string destinationDirectory)
    {
        if (!CanExtractArchive(archivePath))
            throw new NotSupportedException("崩溃日志导入不支持该压缩格式。支持 zip、gz、bz2、tar、tgz 与 tar.gz。");

        Files.ExtractFileAsync(archivePath, destinationDirectory).GetAwaiter().GetResult();
    }
}
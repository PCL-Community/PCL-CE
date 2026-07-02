using System.Diagnostics;
using System.IO;
using System.Text;
using PCL.Core.Utils.Codecs;
using CoreDirectories = PCL.Core.IO.Directories;
using CoreFiles = PCL.Core.IO.Files;

namespace PCL;

/// <summary>
///     历史文件 API 的同步兼容层。
/// </summary>
public static class LegacyFileFacade
{
    public static string ResolvePath(string filePath)
    {
        return LauncherPaths.ResolveLegacyFilePath(filePath);
    }

    public static string GetPathFromFullPath(string filePath)
    {
        return PathUtils.GetDirectoryPart(filePath);
    }

    public static string GetFileNameFromPath(string filePath)
    {
        return PathUtils.GetFileNameFromUrlOrPath(filePath);
    }

    public static string GetFileNameWithoutExtensionFromPath(string filePath)
    {
        return PathUtils.GetFileNameWithoutExtensionFromUrlOrPath(filePath);
    }

    public static string GetFolderNameFromPath(string folderPath)
    {
        return PathUtils.GetDirectoryNameLeaf(folderPath);
    }

    public static void CopyFile(string fromPath, string toPath)
    {
        CoreFiles.CopyFileAsync(ResolvePath(fromPath), ResolvePath(toPath)).GetAwaiter().GetResult();
    }

    public static byte[] ReadBytes(string filePath, Encoding? encoding = null)
    {
        return CoreFiles.ReadAllBytesOrEmptyAsync(ResolvePath(filePath)).GetAwaiter().GetResult();
    }

    public static string ReadText(string filePath, Encoding? encoding = null)
    {
        return CoreFiles.ReadAllTextOrEmptyAsync(ResolvePath(filePath), encoding).GetAwaiter().GetResult();
    }

    public static string ReadText(Stream stream, Encoding? encoding = null)
    {
        return CoreFiles.ReadAllTextOrEmptyAsync(stream, encoding).GetAwaiter().GetResult();
    }


    public static void WriteFile(string filePath, string text, bool append = false, Encoding? encoding = null)
    {
        WriteText(filePath, text, append, encoding);
    }

    public static void WriteFile(string filePath, byte[] content, bool append = false)
    {
        WriteBytes(filePath, content, append);
    }

    public static bool WriteFile(string filePath, Stream stream)
    {
        return WriteStream(filePath, stream);
    }

    public static void WriteText(string filePath, string text, bool append = false, Encoding? encoding = null)
    {
        CoreFiles.WriteFileAsync(ResolvePath(filePath), text, append, encoding).GetAwaiter().GetResult();
    }

    public static void WriteBytes(string filePath, byte[] content, bool append = false)
    {
        CoreFiles.WriteFileAsync(ResolvePath(filePath), content, append).GetAwaiter().GetResult();
    }

    public static bool WriteStream(string filePath, Stream stream)
    {
        return CoreFiles.WriteFileAsync(ResolvePath(filePath), stream).GetAwaiter().GetResult();
    }

    public static string DecodeBytes(byte[] bytes)
    {
        return EncodingUtils.DecodeBytes(bytes);
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        return BinaryEncoding.ToHexLower(bytes.Span);
    }

    public static string GetFileMd5(string filePath)
    {
        return CoreFiles.GetFileMD5Async(filePath).GetAwaiter().GetResult();
    }

    public static string GetFileSha512(string filePath)
    {
        return CoreFiles.GetFileSHA512Async(filePath).GetAwaiter().GetResult();
    }

    public static string GetFileSha256(string filePath)
    {
        return CoreFiles.GetFileSHA256Async(filePath).GetAwaiter().GetResult();
    }

    public static string GetFileSha1(string filePath)
    {
        return CoreFiles.GetFileSHA1Async(filePath).GetAwaiter().GetResult();
    }

    public static string GetStreamSha1(Stream inputStream)
    {
        try
        {
            return (string)GetHexString(SHA1Provider.Instance.ComputeHash(inputStream));
        }
        catch (Exception ex)
        {
            LauncherLog.Log(ex, "获取流 SHA1 失败");
            return "";
        }
    }

    public static string CheckFile(
        string localPath,
        long minSize,
        long actualSize,
        string hash,
        bool isJson)
    {
        return CoreFiles.CheckAsync(localPath, minSize, actualSize, hash, isJson).GetAwaiter().GetResult();
    }

    public static void WaitForFileReady(
        string filePath,
        int timeoutMs = 2000,
        bool requireJson = false)
    {
        filePath = ResolvePath(filePath);
        var start = Environment.TickCount;
        long lastSize = -1;
        while (Environment.TickCount - start < timeoutMs)
        {
            if (File.Exists(filePath))
                try
                {
                    var info = new FileInfo(filePath);
                    var size = info.Length;
                    if (size <= 0)
                        continue;
                    if (!requireJson)
                    {
                        if (size == lastSize)
                            return;
                        lastSize = size;
                    }
                    else
                    {
                        var content = ReadText(filePath);
                        if (!string.IsNullOrEmpty(content) && content.Trim().StartsWith("{"))
                            return;
                    }
                }
                catch (IOException)
                {
                }

            Thread.Sleep(50);
        }
    }

    public static void ExtractFile(
        string compressFilePath,
        string destDirectory,
        Encoding? encode = null,
        Action<double>? progressIncrementHandler = null)
    {
        CoreFiles.ExtractFileAsync(compressFilePath, destDirectory, progressIncrementHandler).GetAwaiter().GetResult();
    }

    public static int DeleteDirectory(string path, bool ignoreIssue = false)
    {
        return CoreDirectories.DeleteDirectoryAsync(path, ignoreIssue).GetAwaiter().GetResult();
    }

    public static void CopyDirectory(
        string fromPath,
        string toPath,
        Action<double>? progressIncrementHandler = null)
    {
        CoreDirectories.CopyDirectoryAsync(fromPath, toPath, progressIncrementHandler).GetAwaiter().GetResult();
    }

    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        try
        {
            return CoreDirectories.EnumerateFilesAsync(ShortenPath(directory)).GetAwaiter().GetResult();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    public static string ShortenPath(string longPath, int shortenThreshold = 247)
    {
        return PathUtils.ShortenPath(longPath, shortenThreshold);
    }

    public static void MoveDirectory(string sourceDir, string targetDir)
    {
        CoreDirectories.MoveDirectoryAsync(sourceDir, targetDir).GetAwaiter().GetResult();
    }

    public static void CreateSymbolicLink(string linkPath, string targetPath, int flags)
    {
        var cmdProcess = new Process();
        var linkDPath = ModLaunch.ExtractLinkD();
        var startInfo = cmdProcess.StartInfo;
        startInfo.FileName = linkDPath;
        startInfo.Arguments = $"\"{linkPath}\" \"{targetPath}\"";
        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = false;
        cmdProcess.Start();
        while (!cmdProcess.HasExited)
        {
        }
    }

    public static bool CheckPermission(string path)
    {
        return CoreDirectories.CheckPermissionAsync(path).GetAwaiter().GetResult();
    }

    public static void CheckPermissionWithException(string path)
    {
        CoreDirectories.CheckPermissionWithExceptionAsync(path).GetAwaiter().GetResult();
    }
}
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;
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
        string getPathFromFullPathRet;
        if (!(filePath.Contains('\\') || filePath.Contains('/')))
            throw new Exception("不包含路径：" + filePath);
        if (filePath.EndsWithF(@"\") || filePath.EndsWithF("/"))
        {
            var isRight = filePath.EndsWithF(@"\");
            filePath = filePath[..^1];
            getPathFromFullPathRet = filePath[..filePath.LastIndexOfAny(['\\', '/'])] + (isRight ? @"\" : "/");
        }
        else
        {
            getPathFromFullPathRet = filePath[..(filePath.LastIndexOfAny(['\\', '/']) + 1)];
            if (string.IsNullOrEmpty(getPathFromFullPathRet))
                throw new Exception("不包含路径：" + filePath);
        }

        return getPathFromFullPathRet;
    }

    public static string GetFileNameFromPath(string filePath)
    {
        filePath = filePath.Replace("/", @"\");
        if (filePath.EndsWithF(@"\"))
            throw new Exception("不包含文件名：" + filePath);
        if (filePath.Contains('?'))
            filePath = filePath[..filePath.IndexOfF("?")];
        if (filePath.Contains('\\'))
            filePath = filePath[(filePath.LastIndexOfF(@"\") + 1)..];

        var length = filePath.Length;
        return length switch
        {
            0 => throw new Exception("不包含文件名：" + filePath),
            > 250 => throw new PathTooLongException("文件名过长：" + filePath),
            _ => filePath
        };
    }

    public static string GetFileNameWithoutExtensionFromPath(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    public static string GetFolderNameFromPath(string folderPath)
    {
        if (folderPath.EndsWithF(@":\") || folderPath.EndsWithF(@":\\"))
            return folderPath[..1];
        if (folderPath.EndsWithF(@"\") || folderPath.EndsWithF("/"))
            folderPath = folderPath[..^1];
        return GetFileNameFromPath(folderPath);
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
        return Convert.ToHexString(bytes.Span).ToLowerInvariant();
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
        if (longPath.Length <= shortenThreshold)
            return longPath;
        var shortPath = new StringBuilder(260);
        GetShortPathName(longPath, shortPath, 260);
        return shortPath.ToString();
    }

    public static void MoveDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);
        foreach (var filePath in Directory.GetFiles(sourceDir))
        {
            var fileName = GetFileNameFromPath(filePath);
            File.Move(filePath, Path.Combine(targetDir, fileName));
        }

        foreach (var dirPath in Directory.GetDirectories(sourceDir))
        {
            var dirName = GetFolderNameFromPath(dirPath);
            MoveDirectory(dirPath, Path.Combine(targetDir, dirName));
        }
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

    [DllImport("kernel32", EntryPoint = "GetShortPathNameA", CharSet = CharSet.Unicode)]
    private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);
}
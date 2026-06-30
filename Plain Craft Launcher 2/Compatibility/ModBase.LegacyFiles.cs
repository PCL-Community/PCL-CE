using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;
using CoreDirectories = PCL.Core.IO.Directories;
using CoreFiles = PCL.Core.IO.Files;

namespace PCL;

public static partial class ModBase
{
    #region LegacyFiles

    private static string ResolveLegacyFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return filePath;
        return filePath.Contains(@":\") ? filePath : exePath + filePath;
    }

    // 路径处理
    /// <summary>
    ///     从文件路径或者 Url 获取不包含文件名的路径，或获取文件夹的父文件夹路径。
    ///     取决于原路径格式，路径以 / 或 \ 结尾。
    ///     不包含路径将会抛出异常。
    /// </summary>
    public static string GetPathFromFullPath(string filePath)
    {
        string getPathFromFullPathRet = default;
        if (!(filePath.Contains(@"\") || filePath.Contains("/")))
            throw new Exception("不包含路径：" + filePath);
        if (filePath.EndsWithF(@"\") || filePath.EndsWithF("/"))
        {
            // 是文件夹路径
            var isRight = filePath.EndsWithF(@"\");
            filePath = filePath[..^1];
            getPathFromFullPathRet = filePath[..filePath.LastIndexOfAny(
                ['\\', '/'])] + (isRight ? @"\" : "/");
        }
        else
        {
            // 是文件路径
            getPathFromFullPathRet = filePath[..(filePath.LastIndexOfAny(['\\', '/']) + 1)];
            if (string.IsNullOrEmpty(getPathFromFullPathRet))
                throw new Exception("不包含路径：" + filePath);
        }

        return getPathFromFullPathRet;
    }

    /// <summary>
    ///     从文件路径或者 Url 获取不包含路径的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameFromPath(string filePath)
    {
        filePath = filePath.Replace("/", @"\");
        if (filePath.EndsWithF(@"\"))
            throw new Exception("不包含文件名：" + filePath);
        if (filePath.Contains('?'))
            filePath = filePath[..filePath.IndexOfF("?")]; // 去掉网络参数后的 ?
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

    /// <summary>
    ///     从文件路径或者 Url 获取不包含路径与扩展名的文件名。不包含文件名将会抛出异常。
    /// </summary>
    public static string GetFileNameWithoutExtentionFromPath(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    ///     从文件夹路径获取文件夹名。
    /// </summary>
    public static string GetFolderNameFromPath(string folderPath)
    {
        if (folderPath.EndsWithF(@":\") || folderPath.EndsWithF(@":\\"))
            return folderPath[..1];
        if (folderPath.EndsWithF(@"\") || folderPath.EndsWithF("/"))
            folderPath = folderPath[..^1];
        return GetFileNameFromPath(folderPath);
    }

    // 读取、写入、复制文件
    /// <summary>
    ///     复制文件。会自动创建文件夹、会覆盖已有的文件。
    /// </summary>
    public static void CopyFile(string fromPath, string toPath)
    {
        CoreFiles.CopyFileAsync(ResolveLegacyFilePath(fromPath), ResolveLegacyFilePath(toPath))
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     读取文件，如果失败则返回空数组。
    /// </summary>
    public static byte[] ReadFileBytes(string filePath, Encoding encoding = null)
    {
        return CoreFiles.ReadAllBytesOrEmptyAsync(ResolveLegacyFilePath(filePath))
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     读取文件，如果失败则返回空字符串。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static string ReadFile(string filePath, Encoding encoding = null)
    {
        return CoreFiles.ReadAllTextOrEmptyAsync(ResolveLegacyFilePath(filePath), encoding)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     读取流中的所有文本。
    /// </summary>
    public static string ReadFile(Stream stream, Encoding encoding = null)
    {
        return CoreFiles.ReadAllTextOrEmptyAsync(stream, encoding)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     写入文件。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    /// <param name="text">文件内容。</param>
    /// <param name="append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
    public static void WriteFile(
        string filePath,
        string text,
        bool append = false,
        Encoding? encoding = null)
    {
        CoreFiles.WriteFileAsync(ResolveLegacyFilePath(filePath), text, append, encoding)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     写入文件。
    ///     如果 CanThrow 设置为 False，返回是否写入成功。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    /// <param name="content">文件内容。</param>
    /// <param name="append">是否将文件内容追加到当前文件，而不是覆盖它。</param>
    public static void WriteFile(string filePath, byte[] content, bool append = false)
    {
        CoreFiles.WriteFileAsync(ResolveLegacyFilePath(filePath), content, append)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     将流写入文件。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static bool WriteFile(string filePath, Stream stream)
    {
        return CoreFiles.WriteFileAsync(ResolveLegacyFilePath(filePath), stream)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     解码 Bytes。
    /// </summary>
    public static string DecodeBytes(byte[] bytes)
    {
        return EncodingUtils.DecodeBytes(bytes);
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        return Convert.ToHexString(bytes.Span).ToLowerInvariant();
    }

    // 文件校验
    /// <summary>
    ///     获取文件 MD5，若失败则返回空字符串。
    /// </summary>
    public static string GetFileMD5(string filePath)
    {
        return CoreFiles.GetFileMD5Async(filePath).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     获取文件 SHA512，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA512(string filePath)
    {
        return CoreFiles.GetFileSHA512Async(filePath).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     获取文件 SHA256，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA256(string filePath)
    {
        return CoreFiles.GetFileSHA256Async(filePath).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     获取文件 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA1(string filePath)
    {
        return CoreFiles.GetFileSHA1Async(filePath).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     获取流的 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetAuthSHA1(Stream inputStream)
    {
        try
        {
            return (string)GetHexString(SHA1Provider.Instance.ComputeHash(inputStream));
        }
        catch (Exception ex)
        {
            Log(ex, "获取流 SHA1 失败");
            return "";
        }
    }

    /// <summary>
    ///     文件的校验规则。
    /// </summary>
    public class FileChecker
    {
        /// <summary>
        ///     文件的准确大小。
        ///     不检查则为 -1。
        /// </summary>
        public long actualSize = -1;

        /// <summary>
        ///     是否可以使用已经存在的文件。
        /// </summary>
        public bool canUseExistsFile = true;

        /// <summary>
        ///     文件的 MD5、SHA1 或 SHA256。会根据输入字符串的长度自动判断种类。
        ///     不检查则为 Nothing。
        /// </summary>
        public string hash;

        /// <summary>
        ///     是否要求为 JSON 文件。
        ///     即，开头结尾必须为 {} 或 []。
        /// </summary>
        public bool isJson;

        /// <summary>
        ///     文件的最小大小。
        ///     不检查则为 -1。
        /// </summary>
        public long minSize = -1;

        public FileChecker(
            long minSize = -1,
            long actualSize = -1,
            string hash = null,
            bool canUseExistsFile = true,
            bool isJson = false)
        {
            this.actualSize = actualSize;
            this.minSize = minSize;
            this.hash = hash;
            this.canUseExistsFile = canUseExistsFile;
            this.isJson = isJson;
        }

        /// <summary>
        ///     检查文件。若成功则返回 Nothing，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
        /// </summary>
        public string Check(string localPath)
        {
            return CoreFiles.CheckAsync(localPath, minSize, actualSize, hash, isJson)
                .GetAwaiter()
                .GetResult();
        }
    }

    /// <summary>
    ///     等待文件就绪可读，在指定超时时间内轮询检查文件是否存在且内容非空。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <param name="timeoutMs">超时时间（毫秒）。</param>
    public static void WaitForFileReady(string filePath, int timeoutMs = 2000)
    {
        WaitForFileReady(filePath, timeoutMs, false);
    }

    /// <summary>
    ///     等待文件就绪可读，在指定超时时间内轮询检查文件是否存在且内容非空。
    /// </summary>
    /// <param name="filePath">文件路径。</param>
    /// <param name="timeoutMs">超时时间（毫秒）。</param>
    /// <param name="requireJson">是否要求文件为合法 JSON。</param>
    public static void WaitForFileReady(string filePath, int timeoutMs, bool requireJson)
    {
        filePath = ResolveLegacyFilePath(filePath);
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
                        var content = ReadFile(filePath);
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

    /// <summary>
    ///     尝试根据后缀名判断文件种类并解压文件，支持 gz 与 zip，会尝试将 Jar 以 zip 方式解压。
    ///     会尝试创建，但不会清空目标文件夹。
    /// </summary>
    public static void ExtractFile(string compressFilePath, string destDirectory, Encoding encode = null,
        Action<double> progressIncrementHandler = null)
    {
        CoreFiles.ExtractFileAsync(compressFilePath, destDirectory, progressIncrementHandler)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     删除文件夹，返回删除的文件个数。通过参数选择是否抛出异常。
    /// </summary>
    public static int DeleteDirectory(string path, bool ignoreIssue = false)
    {
        return CoreDirectories.DeleteDirectoryAsync(path, ignoreIssue)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     复制文件夹，失败会抛出异常。
    /// </summary>
    public static void CopyDirectory(string fromPath, string toPath, Action<double> progressIncrementHandler = null)
    {
        CoreDirectories.CopyDirectoryAsync(fromPath, toPath, progressIncrementHandler)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    ///     遍历文件夹中的所有文件。
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        try
        {
            return CoreDirectories.EnumerateFilesAsync(ShortenPath(directory))
                .GetAwaiter()
                .GetResult();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    /// <summary>
    ///     若路径长度大于指定值，则将长路径转换为短路径。
    /// </summary>
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

    [DllImport("kernel32", EntryPoint = "GetShortPathNameA")]
    private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

    public static void CreateSymbolicLink(string linkPath, string targetPath, int flags)
    {
        var cMDProcess = new Process();
        var linkDPath = ModLaunch.ExtractLinkD();
        {
            var withBlock = cMDProcess.StartInfo;
            withBlock.FileName = linkDPath;
            withBlock.Arguments = $"\"{linkPath}\" \"{targetPath}\"";
            withBlock.CreateNoWindow = true;
            withBlock.UseShellExecute = false;
        }
        cMDProcess.Start();
        while (!cMDProcess.HasExited)
        {
        }
    }


    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果文件夹不存在，会返回 False。
    /// </summary>
    public static bool CheckPermission(string path)
    {
        return CoreDirectories.CheckPermissionAsync(path).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
    /// </summary>
    public static void CheckPermissionWithException(string path)
    {
        CoreDirectories.CheckPermissionWithExceptionAsync(path).GetAwaiter().GetResult();
    }

    #endregion
}
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Hash;

namespace PCL;

public static partial class ModBase
{
    #region LegacyFiles

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
        try
        {
            // 还原文件路径
            if (!fromPath.Contains(@":\"))
                fromPath = exePath + fromPath;
            if (!toPath.Contains(@":\"))
                toPath = exePath + toPath;
            // 如果复制同一个文件则跳过
            if ((fromPath ?? "") == (toPath ?? ""))
                return;
            // 确保目录存在
            Directory.CreateDirectory(GetPathFromFullPath(toPath));
            // 复制文件
            File.Copy(fromPath, toPath, true);
        }
        catch (Exception ex)
        {
            throw new Exception("复制文件出错：" + fromPath + " → " + toPath, ex);
        }
    }

    /// <summary>
    ///     读取文件，如果失败则返回空数组。
    /// </summary>
    public static byte[] ReadFileBytes(string filePath, Encoding encoding = null)
    {
        try
        {
            // 还原文件路径
            if (!filePath.Contains(@":\"))
                filePath = exePath + filePath;
            if (File.Exists(filePath))
            {
                using var readStream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var ms = new MemoryStream();
                readStream.CopyTo(ms);
                return ms.ToArray();
            }

            Log("[System] 欲读取的文件不存在，已返回空内容：" + filePath);
            return [];
        }
        catch (Exception ex)
        {
            Log(ex, "读取文件出错：" + filePath);
            return [];
        }
    }

    /// <summary>
    ///     读取文件，如果失败则返回空字符串。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static string ReadFile(string filePath, Encoding encoding = null)
    {
        var fileBytes = ReadFileBytes(filePath);
        var readFileRet = encoding is null ? DecodeBytes(fileBytes) : encoding.GetString(fileBytes);
        return readFileRet;
    }

    /// <summary>
    ///     读取流中的所有文本。
    /// </summary>
    public static string ReadFile(Stream stream, Encoding encoding = null)
    {
        try
        {
            var readedContent = new MemoryStream();
            stream.CopyTo(readedContent);
            var bts = readedContent.ToArray();
            return (encoding ?? EncodingDetector.DetectEncoding(bts)).GetString(bts);
        }
        catch (Exception ex)
        {
            Log(ex, "读取流出错");
            return "";
        }
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
        // 处理相对路径
        if (!filePath.Contains(@":\"))
            filePath = exePath + filePath;
        // 确保目录存在
        Directory.CreateDirectory(GetPathFromFullPath(filePath));
        // 写入文件
        if (append)
            // 追加目前文件
        {
            using var writer = new StreamWriter(
                filePath,
                true,
                encoding ?? EncodingDetector.DetectEncoding(ReadFileBytes(filePath)));
            writer.Write(text);
        }
        else
        {
            // 直接写入字节
            var bytes = encoding is null
                ? new UTF8Encoding(false).GetBytes(text)
                : encoding.GetBytes(text);
            var tempPath = filePath + ".pcltmp." + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, filePath, true);
        }
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
        // 处理相对路径
        if (!filePath.Contains(@":\"))
            filePath = exePath + filePath;
        // 确保目录存在
        Directory.CreateDirectory(GetPathFromFullPath(filePath));
        // 写入文件
        File.WriteAllBytes(filePath, content);
    }

    /// <summary>
    ///     将流写入文件。
    /// </summary>
    /// <param name="filePath">文件完整或相对路径。</param>
    public static bool WriteFile(string filePath, Stream stream)
    {
        try
        {
            // 还原文件路径
            if (!filePath.Contains(@":\"))
                filePath = exePath + filePath;
            // 确保目录存在
            Directory.CreateDirectory(GetPathFromFullPath(filePath));
            // 读取流
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.SetLength(0L);
            stream.CopyTo(fs);

            return true;
        }
        catch (Exception ex)
        {
            Log(ex, "保存流出错");
            return false;
        }
    }

    /// <summary>
    ///     解码 Bytes。
    /// </summary>
    public static string DecodeBytes(byte[] bytes)
    {
        var length = bytes.Length;
        if (length < 3)
            return Encoding.UTF8.GetString(bytes);
        // 根据 BOM 判断编码
        if (bytes[0] >= 0xEF)
        {
            // 有 BOM 类型
            if (bytes[0] == 0xEF && bytes[1] == 0xBB)
                return Encoding.UTF8.GetString(bytes, 3, length - 3);

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 3, length - 3);

            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 3, length - 3);

            return Encoding.GetEncoding("GB18030").GetString(bytes, 3, length - 3);
        }

        // 无 BOM 文件：GB18030（ANSI）或 UTF8
        var uTF8 = Encoding.UTF8.GetString(bytes);
        var errorChar = Encoding.UTF8.GetString("\ufffd"u8.ToArray()).ToCharArray()[0];
        return uTF8.Contains(errorChar)
            ? Encoding.GetEncoding("GB18030").GetString(bytes)
            : uTF8;
    }

    public static object GetHexString(Memory<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var c in bytes.Span)
            sb.Append(c.ToString("x2"));

        return sb.ToString();
    }

    // 文件校验
    /// <summary>
    ///     获取文件 MD5，若失败则返回空字符串。
    /// </summary>
    public static string GetFileMD5(string filePath)
    {
        var retry = false;
        Re: ;

        try
        {
            // 获取 MD5
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (string)GetHexString(MD5Provider.Instance.ComputeHash(fs));
        }
        catch (Exception ex)
        {
            if (retry || ex is FileNotFoundException)
            {
                Log(ex, "获取文件 MD5 失败：" + filePath);
                return "";
            }

            retry = true;
            Log(ex, "获取文件 MD5 可重试失败：" + filePath, LogLevel.Normal);
            Thread.Sleep(RandomUtils.NextInt(200, 500));
            goto Re;
        }
    }

    /// <summary>
    ///     获取文件 SHA512，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA512(string filePath)
    {
        var retry = false;
        Re: ;

        try
        {
            // '检测该文件是否在下载中，若在下载则放弃检测
            // If IgnoreOnDownloading AndAlso NetManage.Files.ContainsKey(FilePath) AndAlso NetManage.Files(FilePath).State <= NetState.Merge Then Return ""
            // 获取 SHA512
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (string)GetHexString(SHA512Provider.Instance.ComputeHash(fs));
        }
        catch (Exception ex)
        {
            if (retry || ex is FileNotFoundException)
            {
                Log(ex, "获取文件 SHA512 失败：" + filePath);
                return "";
            }

            retry = true;
            Log(ex, "获取文件 SHA512 可重试失败：" + filePath, LogLevel.Normal);
            Thread.Sleep(RandomUtils.NextInt(200, 500));
            goto Re;
        }
    }

    /// <summary>
    ///     获取文件 SHA256，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA256(string filePath)
    {
        var retry = false;
        Re: ;

        try
        {
            // '检测该文件是否在下载中，若在下载则放弃检测
            // If IgnoreOnDownloading AndAlso NetManage.Files.ContainsKey(FilePath) AndAlso NetManage.Files(FilePath).State <= NetState.Merge Then Return ""
            // 获取 SHA256
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (string)GetHexString(SHA256Provider.Instance.ComputeHash(fs));
        }
        catch (Exception ex)
        {
            if (retry || ex is FileNotFoundException)
            {
                Log(ex, "获取文件 SHA256 失败：" + filePath);
                return "";
            }

            retry = true;
            Log(ex, "获取文件 SHA256 可重试失败：" + filePath, LogLevel.Normal);
            Thread.Sleep(RandomUtils.NextInt(200, 500));
            goto Re;
        }
    }

    /// <summary>
    ///     获取文件 SHA1，若失败则返回空字符串。
    /// </summary>
    public static string GetFileSHA1(string filePath)
    {
        var retry = false;
        Re: ;

        try
        {
            // 获取 SHA1
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return (string)GetHexString(SHA1Provider.Instance.ComputeHash(fs));
        }
        catch (Exception ex)
        {
            if (retry || ex is FileNotFoundException)
            {
                Log(ex, "获取文件 SHA1 失败：" + filePath);
                return "";
            }

            retry = true;
            Log(ex, "获取文件 SHA1 可重试失败：" + filePath, LogLevel.Normal);
            Thread.Sleep(RandomUtils.NextInt(200, 500));
            goto Re;
        }
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
            try
            {
                Log($"[Checker] 开始校验文件 {localPath}", LogLevel.Developer);
                var info = new FileInfo(localPath);
                if (!info.Exists)
                    return "文件不存在：" + localPath;
                var fileSize = info.Length;
                var errorMessage = new List<string>();
                var allowIgnore = false; // 允许相信哈希正确但是大小不正确
                if (!string.IsNullOrEmpty(hash))
                {
                    switch (hash.Length)
                    {
                        // MD5
                        case < 35:
                        {
                            var computedHash = GetFileMD5(localPath);
                            if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                                errorMessage.Add("文件 MD5 应为 " + hash + "，实际为 " + computedHash);
                            break;
                        }
                        // SHA256
                        case 64:
                        {
                            var computedHash = GetFileSHA256(localPath);
                            if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                                errorMessage.Add("文件 SHA256 应为 " + hash + "，实际为 " + computedHash);
                            break;
                        }
                        // SHA1 (40)
                        default:
                        {
                            var computedHash = GetFileSHA1(localPath);
                            if ((hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                                errorMessage.Add("文件 SHA1 应为 " + hash + "，实际为 " + computedHash);
                            break;
                        }
                    }

                    allowIgnore = errorMessage.Count == 0;
                }

                if (actualSize >= 0L && actualSize != fileSize && !allowIgnore) // 不允许忽略大小不正确的情况
                    errorMessage.Add($"文件大小应为 {actualSize} B，实际为 {fileSize} B" +
                                     (fileSize < 2000L ? "，内容为" + ReadFile(localPath) : ""));

                if (minSize >= 0L && minSize > fileSize)
                    errorMessage.Add($"文件大小应大于 {minSize} B，实际为 {fileSize} B" +
                                     (fileSize < 2000L ? "，内容为：" + ReadFile(localPath) : ""));

                if (isJson)
                {
                    var content = ReadFile(localPath);
                    if (string.IsNullOrEmpty(content))
                        throw new Exception("读取到的文件为空");
                    try
                    {
                        GetJson(content);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(Lang.Text("Common.Error.InvalidJson"), ex);
                    }
                }

                if (errorMessage.Count == 0) return null;

                errorMessage.Insert(0, $"实际校验地址：{localPath}");
                return errorMessage.Join(";");
            }
            catch (Exception ex)
            {
                Log(ex, "检查文件出错");
                return ex.ToString();
            }
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
        filePath = filePath.Contains(@":\") ? filePath : exePath + filePath;
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
        Directory.CreateDirectory(destDirectory);
        destDirectory = Path.GetFullPath(destDirectory);
        if (!destDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            destDirectory += Path.DirectorySeparatorChar.ToString();
        if (compressFilePath.EndsWithF(".gz", true))
            // 以 gz 方式解压
        {
            using var compressedFile = new FileStream(compressFilePath, FileMode.Open, FileAccess.Read);
            using var decompressStream = new GZipStream(compressedFile, CompressionMode.Decompress);
            using var extractFileStream = new FileStream(
                Path.Combine(
                    destDirectory,
                    GetFileNameFromPath(compressFilePath)
                        .ToLower()
                        .Replace(".tar", "")
                        .Replace(".gz", "")),
                FileMode.OpenOrCreate, FileAccess.Write);
            decompressStream.CopyTo(extractFileStream);
        }
        else
            // 以 zip 方式解压
        {
            using var archive = ZipFile.Open(compressFilePath, ZipArchiveMode.Read,
                encode ?? Encoding.GetEncoding("GB18030"));
            var totalCount = archive.Entries.Count;
            foreach (var entry in archive.Entries)
            {
                progressIncrementHandler?.Invoke(1d / totalCount);
                var destinationPath = Path.GetFullPath(Path.Combine(destDirectory, entry.FullName));
                if (!destinationPath.StartsWithF(destDirectory))
                    throw new Exception(
                        $"解压文件 {entry.FullName} 错误：解压文件路径 {destinationPath} 不在目标目录 {destDirectory} 内");
                if (destinationPath.EndsWithF(@"\") || destinationPath.EndsWithF("/")) continue;
                Directory.CreateDirectory(GetPathFromFullPath(destinationPath));
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }

    /// <summary>
    ///     删除文件夹，返回删除的文件个数。通过参数选择是否抛出异常。
    /// </summary>
    public static int DeleteDirectory(string path, bool ignoreIssue = false)
    {
        if (!Directory.Exists(path))
            return 0;
        var deletedCount = 0;
        string[] files;
        try
        {
            files = Directory.GetFiles(path);
        }
        catch (DirectoryNotFoundException ex) // #4549
        {
            Log(ex, $"疑似为孤立符号链接，尝试直接删除（{path}）", LogLevel.Developer);
            Directory.Delete(path);
            return 0;
        }

        foreach (var filePath in files)
        {
            var retriedFile = false;
            RetryFile: ;

            try
            {
                File.Delete(filePath);
                deletedCount += 1;
            }
            catch (Exception ex)
            {
                if (!retriedFile)
                {
                    retriedFile = true;
                    Log(ex, $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                    Thread.Sleep(300);
                    goto RetryFile;
                }

                if (ignoreIssue)
                    Log(ex, "删除单个文件可忽略地失败");
                else
                    throw;
            }
        }

        foreach (var str in Directory.GetDirectories(path))
            DeleteDirectory(str, ignoreIssue);
        var retriedDir = false;
        RetryDir: ;

        try
        {
            Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            if (!retriedDir && !RunInUi())
            {
                retriedDir = true;
                Log(ex, $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                Thread.Sleep(300);
                goto RetryDir;
            }

            if (ignoreIssue)
                Log(ex, "删除单个文件夹可忽略地失败");
            else
                throw;
        }

        return deletedCount;
    }

    /// <summary>
    ///     复制文件夹，失败会抛出异常。
    /// </summary>
    public static void CopyDirectory(string fromPath, string toPath, Action<double> progressIncrementHandler = null)
    {
        fromPath = fromPath.Replace("/", @"\");
        if (!fromPath.EndsWithF(@"\"))
            fromPath += @"\";
        toPath = toPath.Replace("/", @"\");
        if (!toPath.EndsWithF(@"\"))
            toPath += @"\";
        var allFiles = EnumerateFiles(fromPath).ToList();
        var fileCount = allFiles.Count;
        foreach (var file in allFiles)
        {
            CopyFile(file.FullName, file.FullName.Replace(fromPath, toPath));
            if (progressIncrementHandler is not null)
                progressIncrementHandler(1d / fileCount);
        }
    }

    /// <summary>
    ///     遍历文件夹中的所有文件。
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        var info = new DirectoryInfo(ShortenPath(directory));
        if (!info.Exists)
            return new List<FileInfo>();
        return info.EnumerateFiles("*", SearchOption.AllDirectories);
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
        try
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (!path.EndsWithF(@"\"))
                path += @"\";
            if (path.EndsWithF(@":\System Volume Information\") || path.EndsWithF(@":\$RECYCLE.BIN\"))
                return false;
            if (!Directory.Exists(path))
                return false;
            var fileName = "CheckPermission" + GetUuid();
            if (File.Exists(path + fileName))
                File.Delete(path + fileName);
            File.Create(path + fileName).Dispose();
            File.Delete(path + fileName);
            return true;
        }
        catch (Exception ex)
        {
            Log(ex, "没有对文件夹 " + path + " 的权限，请尝试以管理员权限运行 PCL");
            return false;
        }
    }

    /// <summary>
    ///     检查是否拥有某一文件夹的 I/O 权限。如果出错，则抛出异常。
    /// </summary>
    public static void CheckPermissionWithException(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException("文件夹名不能为空！");
        if (!path.EndsWithF(@"\"))
            path += @"\";
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("文件夹不存在！");
        if (File.Exists(path + "CheckPermission"))
            File.Delete(path + "CheckPermission");
        File.Create(path + "CheckPermission").Dispose();
        File.Delete(path + "CheckPermission");
    }

    #endregion
}
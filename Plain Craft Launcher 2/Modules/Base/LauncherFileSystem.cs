using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.Utils.Codecs;

namespace PCL;

/// <summary>
/// Owns file read, write, copy, delete, enumerate, extract, and permission/check operations.
/// </summary>
public static class LauncherFileSystem
{
    public static void CopyFile(string fromPath, string toPath)
    {
        try
        {
            fromPath = ResolveLauncherRelativePath(fromPath);
            toPath = ResolveLauncherRelativePath(toPath);
            if ((fromPath ?? "") == (toPath ?? ""))
                return;
            Directory.CreateDirectory(LauncherPaths.GetDirectoryFromPath(toPath));
            File.Copy(fromPath, toPath, true);
        }
        catch (Exception ex)
        {
            throw new Exception("复制文件出错：" + fromPath + " → " + toPath, ex);
        }
    }

    public static byte[] ReadFileBytes(string filePath, Encoding encoding = null)
    {
        try
        {
            filePath = ResolveLauncherRelativePath(filePath);
            if (File.Exists(filePath))
            {
                using var readStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var ms = new MemoryStream();
                readStream.CopyTo(ms);
                return ms.ToArray();
            }

            LauncherLogger.Log("[System] 欲读取的文件不存在，已返回空内容：" + filePath, LauncherLogger.LogLevel.Normal);
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "读取文件出错：" + filePath, LauncherLogger.LogLevel.Normal);
            return Array.Empty<byte>();
        }
    }

    public static string ReadFile(string filePath, Encoding encoding = null)
    {
        var fileBytes = ReadFileBytes(filePath);
        return encoding is null ? DecodeBytes(fileBytes) : encoding.GetString(fileBytes);
    }

    public static string ReadFile(Stream stream, Encoding encoding = null)
    {
        try
        {
            var readContent = new MemoryStream();
            stream.CopyTo(readContent);
            var bytes = readContent.ToArray();
            return (encoding ?? EncodingDetector.DetectEncoding(bytes)).GetString(bytes);
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "读取流出错");
            return "";
        }
    }

    public static bool WriteFile(string filePath, string text, bool append = false, Encoding? encoding = null)
    {
        filePath = ResolveLauncherRelativePath(filePath);
        Directory.CreateDirectory(LauncherPaths.GetDirectoryFromPath(filePath));
        if (append)
        {
            using var writer = new StreamWriter(filePath, true, encoding ?? EncodingDetector.DetectEncoding(ReadFileBytes(filePath)));
            writer.Write(text);
        }
        else
        {
            File.WriteAllBytes(filePath, encoding is null ? new UTF8Encoding(false).GetBytes(text) : encoding.GetBytes(text));
        }

        return true;
    }

    public static bool WriteFile(string filePath, byte[] content, bool append = false)
    {
        filePath = ResolveLauncherRelativePath(filePath);
        Directory.CreateDirectory(LauncherPaths.GetDirectoryFromPath(filePath));
        File.WriteAllBytes(filePath, content);
        return true;
    }

    public static bool WriteFile(string filePath, Stream stream)
    {
        try
        {
            filePath = ResolveLauncherRelativePath(filePath);
            Directory.CreateDirectory(LauncherPaths.GetDirectoryFromPath(filePath));
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            fs.SetLength(0L);
            stream.CopyTo(fs);
            return true;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "保存流出错");
            return false;
        }
    }

    public static string DecodeBytes(byte[] bytes)
    {
        var length = bytes.Length;
        if (length < 3)
            return Encoding.UTF8.GetString(bytes);
        if (bytes[0] >= 0xEF)
        {
            if (bytes[0] == 0xEF && bytes[1] == 0xBB) return Encoding.UTF8.GetString(bytes, 3, length - 3);
            if (bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes, 3, length - 3);
            if (bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 3, length - 3);
            return Encoding.GetEncoding("GB18030").GetString(bytes, 3, length - 3);
        }

        var utf8 = Encoding.UTF8.GetString(bytes);
        var errorChar = Encoding.UTF8.GetString(new[] { (byte)239, (byte)191, (byte)189 }).ToCharArray()[0];
        return utf8.Contains(errorChar) ? Encoding.GetEncoding("GB18030").GetString(bytes) : utf8;
    }

    public class FileChecker
    {
        public long ActualSize = -1;
        public bool CanUseExistsFile = true;
        public string Hash;
        public bool IsJson;
        public long MinSize = -1;

        public FileChecker(long minSize = -1, long actualSize = -1, string hash = null, bool canUseExistsFile = true,
            bool isJson = false)
        {
            ActualSize = actualSize;
            MinSize = minSize;
            Hash = hash;
            CanUseExistsFile = canUseExistsFile;
            IsJson = isJson;
        }

        public string Check(string localPath)
        {
            try
            {
                LauncherLogger.Log($"[Checker] 开始校验文件 {localPath}", LauncherLogger.LogLevel.Developer);
                var info = new FileInfo(localPath);
                if (!info.Exists)
                    return "文件不存在：" + localPath;
                var fileSize = info.Length;
                var errorMessage = new List<string>();
                var allowIgnore = false;
                if (!string.IsNullOrEmpty(Hash))
                {
                    if (Hash.Length < 35)
                    {
                        var computedHash = LauncherHash.GetFileMD5(localPath);
                        if ((Hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add("文件 MD5 应为 " + Hash + "，实际为 " + computedHash);
                    }
                    else if (Hash.Length == 64)
                    {
                        var computedHash = LauncherHash.GetFileSHA256(localPath);
                        if ((Hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add("文件 SHA256 应为 " + Hash + "，实际为 " + computedHash);
                    }
                    else
                    {
                        var computedHash = LauncherHash.GetFileSHA1(localPath);
                        if ((Hash.ToLowerInvariant() ?? "") != (computedHash ?? ""))
                            errorMessage.Add("文件 SHA1 应为 " + Hash + "，实际为 " + computedHash);
                    }

                    allowIgnore = errorMessage.Count == 0;
                }

                if (ActualSize >= 0L && ActualSize != fileSize && !allowIgnore)
                    errorMessage.Add($"文件大小应为 {ActualSize} B，实际为 {fileSize} B" +
                                     (fileSize < 2000L ? "，内容为" + ReadFile(localPath) : ""));

                if (MinSize >= 0L && MinSize > fileSize)
                    errorMessage.Add($"文件大小应大于 {MinSize} B，实际为 {fileSize} B" +
                                     (fileSize < 2000L ? "，内容为：" + ReadFile(localPath) : ""));

                if (IsJson)
                {
                    var content = ReadFile(localPath);
                    if (string.IsNullOrEmpty(content))
                        throw new Exception("读取到的文件为空");
                    try
                    {
                        LauncherSerialization.GetJson(content);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("不是有效的 Json 文件", ex);
                    }
                }

                if (errorMessage.Count != 0)
                {
                    errorMessage.Insert(0, $"实际校验地址：{localPath}");
                    return errorMessage.Join(";");
                }

                return null;
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "检查文件出错");
                return ex.ToString();
            }
        }
    }

    public static void ExtractFile(string compressFilePath, string destDirectory, Encoding encode = null,
        Action<double> progressIncrementHandler = null)
    {
        Directory.CreateDirectory(destDirectory);
        destDirectory = Path.GetFullPath(destDirectory);
        if (!destDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            destDirectory += Path.DirectorySeparatorChar;
        if (compressFilePath.EndsWithF(".gz", true))
        {
            using var compressedFile = new FileStream(compressFilePath, FileMode.Open, FileAccess.Read);
            using var decompressStream = new GZipStream(compressedFile, CompressionMode.Decompress);
            using var extractFileStream = new FileStream(
                Path.Combine(destDirectory, LauncherPaths.GetFileName(compressFilePath).ToLower().Replace(".tar", "").Replace(".gz", "")),
                FileMode.OpenOrCreate, FileAccess.Write);
            decompressStream.CopyTo(extractFileStream);
        }
        else
        {
            using var archive = ZipFile.Open(compressFilePath, ZipArchiveMode.Read, encode ?? Encoding.GetEncoding("GB18030"));
            var totalCount = archive.Entries.Count;
            foreach (var entry in archive.Entries)
            {
                progressIncrementHandler?.Invoke(1d / totalCount);
                var destinationPath = Path.GetFullPath(Path.Combine(destDirectory, entry.FullName));
                if (!destinationPath.StartsWithF(destDirectory))
                    throw new Exception($"解压文件 {entry.FullName} 错误：解压文件路径 {destinationPath} 不在目标目录 {destDirectory} 内");
                if (destinationPath.EndsWithF(@"\") || destinationPath.EndsWithF("/"))
                    continue;
                Directory.CreateDirectory(LauncherPaths.GetDirectoryFromPath(destinationPath));
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }

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
        catch (DirectoryNotFoundException ex)
        {
            LauncherLogger.Log(ex, $"疑似为孤立符号链接，尝试直接删除（{path}）", LauncherLogger.LogLevel.Developer);
            Directory.Delete(path);
            return 0;
        }

        foreach (var filePath in files)
        {
            var retriedFile = false;
            while (true)
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount += 1;
                    break;
                }
                catch (Exception ex)
                {
                    if (!retriedFile)
                    {
                        retriedFile = true;
                        LauncherLogger.Log(ex, $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                        Thread.Sleep(300);
                        continue;
                    }
                    if (ignoreIssue)
                    {
                        LauncherLogger.Log(ex, "删除单个文件可忽略地失败");
                        break;
                    }
                    throw;
                }
            }
        }

        foreach (var childPath in Directory.GetDirectories(path))
            DeleteDirectory(childPath, ignoreIssue);
        var retriedDir = false;
        while (true)
        {
            try
            {
                Directory.Delete(path, true);
                break;
            }
            catch (Exception ex)
            {
                if (!retriedDir && !LauncherDispatcher.RunInUi())
                {
                    retriedDir = true;
                    LauncherLogger.Log(ex, $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                    Thread.Sleep(300);
                    continue;
                }
                if (ignoreIssue)
                {
                    LauncherLogger.Log(ex, "删除单个文件夹可忽略地失败");
                    break;
                }
                throw;
            }
        }

        return deletedCount;
    }

    public static void CopyDirectory(string fromPath, string toPath, Action<double> progressIncrementHandler = null)
    {
        fromPath = fromPath.Replace("/", @"\");
        if (!fromPath.EndsWithF(@"\")) fromPath += @"\";
        toPath = toPath.Replace("/", @"\");
        if (!toPath.EndsWithF(@"\")) toPath += @"\";
        var allFiles = EnumerateFiles(fromPath).ToList();
        var fileCount = allFiles.Count;
        foreach (var file in allFiles)
        {
            CopyFile(file.FullName, file.FullName.Replace(fromPath, toPath));
            progressIncrementHandler?.Invoke(1d / fileCount);
        }
    }

    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        var info = new DirectoryInfo(LauncherPaths.ShortenPath(directory));
        if (!info.Exists)
            return new List<FileInfo>();
        return info.EnumerateFiles("*", SearchOption.AllDirectories);
    }

    public static void MoveDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);
        foreach (var filePath in Directory.GetFiles(sourceDir))
            File.Move(filePath, Path.Combine(targetDir, LauncherPaths.GetFileName(filePath)));
        foreach (var dirPath in Directory.GetDirectories(sourceDir))
            MoveDirectory(dirPath, Path.Combine(targetDir, LauncherPaths.GetFolderName(dirPath)));
    }

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
            var fileName = "CheckPermission" + Guid.NewGuid().ToString("N");
            if (File.Exists(path + fileName))
                File.Delete(path + fileName);
            File.Create(path + fileName).Dispose();
            File.Delete(path + fileName);
            return true;
        }
        catch (Exception ex)
        {
            LauncherLogger.Log(ex, "没有对文件夹 " + path + " 的权限，请尝试以管理员权限运行 PCL");
            return false;
        }
    }

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

    private static string ResolveLauncherRelativePath(string filePath)
    {
        return filePath.Contains(@":\") ? filePath : LauncherPaths.ExecutableDirectory + filePath;
    }
}

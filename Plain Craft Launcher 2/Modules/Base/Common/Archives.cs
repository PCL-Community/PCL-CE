using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace PCL;

public static partial class Common
{
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
        filePath = filePath.Contains(@":\") ? filePath : ExePath + filePath;
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
                        if (!string.IsNullOrEmpty(content)
                            && content.Trim().StartsWith("{"))
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
    public static void ExtractFile(
        string compressFilePath,
        string destDirectory,
        Encoding? encode = null,
        Action<double>? progressIncrementHandler = null)
    {
        Directory.CreateDirectory(destDirectory);
        destDirectory = Path.GetFullPath(destDirectory);
        if (!destDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            destDirectory += Path.DirectorySeparatorChar.ToString();
        if (compressFilePath.EndsWithF(".gz", true))
        {
            using var compressedFile = new FileStream(
                compressFilePath,
                FileMode.Open,
                FileAccess.Read);
            using var decompressStream = new GZipStream(
                compressedFile,
                CompressionMode.Decompress);
            using var extractFileStream = new FileStream(
                Path.Combine(
                    destDirectory,
                    GetFileNameFromPath(compressFilePath)
                        .ToLower()
                        .Replace(".tar", "")
                        .Replace(".gz", "")),
                FileMode.OpenOrCreate,
                FileAccess.Write);
            decompressStream.CopyTo(extractFileStream);
        }
        else
        {
            using var archive = ZipFile.Open(
                compressFilePath,
                ZipArchiveMode.Read,
                encode ?? Encoding.GetEncoding("GB18030"));
            var totalCount = archive.Entries.Count;
            foreach (var entry in archive.Entries)
            {
                progressIncrementHandler?.Invoke(1d / totalCount);
                var destinationPath = Path.GetFullPath(Path.Combine(destDirectory, entry.FullName));

                if (!destinationPath.StartsWithF(destDirectory))
                    throw new Exception(
                        $"解压文件 {entry.FullName} 错误：解压文件路径 {destinationPath} 不在目标目录 {destDirectory} 内");

                if (destinationPath.EndsWithF(@"\")
                    || destinationPath.EndsWithF("/"))
                    continue;

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
        catch (DirectoryNotFoundException ex)
        {
            Log(ex, $"疑似为孤立符号链接，尝试直接删除（{path}）", LogLevel.Developer);
            Directory.Delete(path);
            return 0;
        }

        foreach (var filePath in files)
            for (var attempt = 0; attempt < 2; attempt++)
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                    {
                        Log(ex, $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                        Thread.Sleep(300);
                        continue;
                    }

                    if (!ignoreIssue)
                        throw;

                    Log(ex, "删除单个文件可忽略地失败");
                }

        foreach (var directory in Directory.GetDirectories(path))
            DeleteDirectory(directory, ignoreIssue);

        for (var attempt = 0; attempt < 2; attempt++)
            try
            {
                Directory.Delete(path, true);
                break;
            }
            catch (Exception ex)
            {
                if (attempt == 0
                    && !RunInUi())
                {
                    Log(ex, $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                    Thread.Sleep(300);
                    continue;
                }

                if (!ignoreIssue)
                    throw;

                Log(ex, "删除单个文件夹可忽略地失败");
                break;
            }

        return deletedCount;
    }

    /// <summary>
    ///     复制文件夹，失败会抛出异常。
    /// </summary>
    public static void CopyDirectory(
        string fromPath,
        string toPath,
        Action<double>? progressIncrementHandler = null)
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
            progressIncrementHandler?.Invoke(1d / fileCount);
        }
    }

    /// <summary>
    ///     遍历文件夹中的所有文件。
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string directory)
    {
        var info = new DirectoryInfo(ShortenPath(directory));
        return !info.Exists
            ? new List<FileInfo>()
            : info.EnumerateFiles("*", SearchOption.AllDirectories);
    }

    /// <summary>
    ///     若路径长度大于指定值，则将长路径转换为短路径。
    /// </summary>
    public static string ShortenPath(string longPath, int shortenThreshold = 247)
    {
        if (longPath.Length <= shortenThreshold)
            return longPath;
        var shortPath = new StringBuilder(260);
        _ = GetShortPathName(longPath, shortPath, 260);
        return shortPath.ToString();
    }

    [DllImport("kernel32", EntryPoint = "GetShortPathNameA")]
    private static extern int GetShortPathName(
        string lpszLongPath,
        StringBuilder lpszShortPath,
        int cchBuffer);

    public static void CreateSymbolicLink(string linkPath, string targetPath)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = GameLaunch.ExtractLinkD(),
            Arguments = $"\"{linkPath}\" \"{targetPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        process.Start();
        process.WaitForExit();
    }
}
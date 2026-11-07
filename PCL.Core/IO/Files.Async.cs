using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.Codecs;

namespace PCL.Core.IO;

public static partial class Files
{
    /// <summary>
    ///     从剪切板粘贴文件或文件夹
    /// </summary>
    /// <param name="dest">目标文件夹</param>
    /// <param name="copyFile">是否粘贴文件</param>
    /// <param name="copyDir">是否粘贴文件夹</param>
    /// <returns>总共粘贴的数量</returns>
    public static async Task<int> PasteFromClipboardAsync(
        string dest,
        bool copyFile,
        bool copyDir,
        bool skipExisting = false)
    {
        if (string.IsNullOrEmpty(dest))
            throw new ArgumentException(
                "Destination folder cannot be null or empty.",
                nameof(dest));

        if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);

        var paths = PlatformServices.Clipboard?.GetFileDropList();
        if (paths is null || paths.Length == 0) return 0;

        var count = 0;
        foreach (var path in paths)
            if (File.Exists(path) && copyFile)
            {
                var targetPath = Path.Combine(dest, Path.GetFileName(path));
                if (skipExisting && File.Exists(targetPath)) continue;
                await CopyFileAsync(path, targetPath);
                count++;
            }
            else if (Directory.Exists(path) && copyDir)
            {
                var targetDir = Path.Combine(dest, new DirectoryInfo(path).Name);
                if (skipExisting && Directory.Exists(targetDir)) continue;
                await CopyDirectoryAsync(path, targetDir);
                count++;
            }

        return count;
    }

    /// <summary>
    ///     复制文件，自动创建目标目录并覆盖已有文件。
    /// </summary>
    /// <param name="fromPath">源文件路径（完整或相对）</param>
    /// <param name="toPath">目标文件路径（完整或相对）</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <exception cref="IOException">复制失败时抛出</exception>
    public static async Task CopyFileAsync(
        string fromPath,
        string toPath,
        CancellationToken cancelToken = default)
    {
        try
        {
            var fullFromPath = GetFullPath(fromPath);
            var fullToPath = GetFullPath(toPath);
            if (fullFromPath == fullToPath) return;

            var directoryName = Path.GetDirectoryName(fullToPath);
            if (directoryName is null) throw new InvalidOperationException("无法获取目标目录");

            Directory.CreateDirectory(directoryName);

            // 使用异步流复制
            const int bufferSize = 4096;
            await using var sourceStream = new FileStream(
                fullFromPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destinationStream = new FileStream(
                fullToPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize,
                FileOptions.Asynchronous);
            await sourceStream.CopyToAsync(destinationStream, cancelToken);
        }
        catch (Exception ex)
        {
            throw new IOException($"复制文件出错：{fromPath} -> {toPath}", ex);
        }
    }

    public static async Task CopyDirectoryAsync(
        string sourceDir,
        string destDir,
        CancellationToken cancelToken = default)
    {
        Directory.CreateDirectory(destDir);

        // 获取所有文件和子目录
        var files = Directory.GetFiles(sourceDir);
        var directories = Directory.GetDirectories(sourceDir);

        // 限制并行度，防止资源耗尽
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
            CancellationToken = cancelToken
        };

        // 并行复制文件
        await Parallel.ForEachAsync(
            files,
            parallelOptions,
            async (file, ct) =>
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                await CopyFileAsync(file, destFile, ct);
            });

        // 并行处理子目录
        await Parallel.ForEachAsync(
            directories,
            parallelOptions,
            async (subDir, ct) =>
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                await CopyDirectoryAsync(subDir, destSubDir, ct);
            });
    }

    /// <summary>
    ///     读取文件为字节数组，支持读取被占用的文件。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>文件内容的字节数组，失败时返回空数组</returns>
    public static async Task<byte[]> ReadAllBytesOrEmptyAsync(
        string filePath,
        CancellationToken cancelToken = default)
    {
        try
        {
            var fullPath = GetFullPath(filePath);
            if (File.Exists(fullPath))
                // 使用 ReadAllBytesAsync
                return await File.ReadAllBytesAsync(fullPath, cancelToken);

            throw new FileNotFoundException(fullPath);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"读取文件出错：{filePath}");
            return [];
        }
    }

    /// <summary>
    ///     读取文件为字符串。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="encoding">文件编码，默认为 UTF-8</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>文件内容的字符串，失败时返回空字符串</returns>
    public static async Task<string> ReadAllTextOrEmptyAsync(
        string filePath,
        Encoding? encoding = null,
        CancellationToken cancelToken = default)
    {
        try
        {
            var fullPath = GetFullPath(filePath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException(fullPath);
            if (encoding is null) return await File.ReadAllTextAsync(fullPath, cancelToken);
            return await File.ReadAllTextAsync(fullPath, encoding, cancelToken);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"读取文件出错：{filePath}");
            return "";
        }
    }

    /// <summary>
    ///     从流中读取所有文本。
    /// </summary>
    /// <param name="stream">要读取的流</param>
    /// <param name="encoding">文件编码（可选，若为 null 则动态检测）</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>流内容的字符串，失败时返回空字符串</returns>
    public static async Task<string> ReadAllTextOrEmptyAsync(
        Stream stream,
        Encoding? encoding = null,
        CancellationToken cancelToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(stream);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancelToken);
            // 使用 MemoryStream 的内部 buffer 避免再分配一次完整的 byte 数组以节省内存
            // 注：内部 buffer 长度可能大于实际数据长度
            var buffer = memoryStream.GetBuffer();
            var len = (int)memoryStream.Length;
            return (encoding ?? EncodingDetector.DetectEncoding(buffer)).GetString(buffer, 0, len);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"读取流出错: {stream}");
            return string.Empty;
        }
    }

    /// <summary>
    ///     异步读取文件到流。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>包含文件内容的 MemoryStream，失败时返回空的 MemoryStream</returns>
    public static async Task<MemoryStream> ReadFileToStreamOrEmptyAsync(
        string filePath,
        CancellationToken cancelToken = default)
    {
        try
        {
            var fullPath = GetFullPath(filePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException(fullPath);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                true);
            var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, cancelToken);
            memoryStream.Position = 0; // 重置流位置以便后续读取
            return memoryStream;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"读取文件到流出错：{filePath}");
            return new MemoryStream(); // 返回空的 MemoryStream
        }
    }

    /// <summary>
    ///     写入字符串到文件，支持追加或覆盖，自动创建目录。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="text">要写入的文本</param>
    /// <param name="append">追加到文件（true）或覆盖（false）</param>
    /// <param name="encoding">文件编码（可选），默认智能检测</param>
    /// <param name="cancelToken">取消令牌</param>
    public static async Task WriteFileAsync(
        string filePath,
        string text,
        bool append = false,
        Encoding? encoding = null,
        CancellationToken cancelToken = default)
    {
        var fullPath = GetFullPath(filePath);
        var directoryName = Path.GetDirectoryName(fullPath);
        if (directoryName is null) throw new InvalidOperationException("无法获取目标目录");

        Directory.CreateDirectory(directoryName);

        if (append)
        {
            // 编码检测使用 stream 而不是完整读取内容以避免多余的内存占用
            await using (var fileStream = new FileStream(
                             fullPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.ReadWrite))
            {
                encoding ??= EncodingDetector.DetectEncoding(fileStream);
            }

            // 注：从此处开始，编码检测使用的 stream 已经销毁
            await File.AppendAllTextAsync(fullPath, text, encoding, cancelToken);
        }
        else
        {
            encoding ??= new UTF8Encoding(false); // 无 BOM 的 UTF-8
            await File.WriteAllTextAsync(fullPath, text, encoding, cancelToken);
        }
    }

    /// <summary>
    ///     写入字节数组到文件，自动创建目录。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="content">要写入的字节数组</param>
    /// <param name="append">追加到文件（true）或覆盖（false），默认为 false</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>一个 Task，表示异步写入操作</returns>
    public static async Task WriteFileAsync(
        string filePath,
        byte[] content,
        bool append = false,
        CancellationToken cancelToken = default)
    {
        var fullPath = GetFullPath(filePath);
        var directoryName = Path.GetDirectoryName(fullPath);
        if (directoryName is null) throw new InvalidOperationException("无法获取目标目录");

        Directory.CreateDirectory(directoryName);

        var fileMode = append ? FileMode.Append : FileMode.Create;
        await using var fileStream = new FileStream(
            fullPath,
            fileMode,
            FileAccess.Write,
            FileShare.Read);
        await fileStream.WriteAsync(content.AsMemory(), cancelToken);
    }

    /// <summary>
    ///     将流写入文件，自动创建目录。
    /// </summary>
    /// <param name="filePath">文件路径（完整或相对）</param>
    /// <param name="stream">要写入的流</param>
    /// <param name="cancelToken">取消令牌</param>
    /// <returns>写入是否成功</returns>
    public static async Task<bool> WriteFileAsync(
        string filePath,
        Stream? stream,
        CancellationToken cancelToken = default)
    {
        if (stream is null) return false;
        try
        {
            var fullPath = GetFullPath(filePath);
            var directoryName = Path.GetDirectoryName(fullPath);
            if (directoryName is null) throw new InvalidOperationException("无法获取目标目录");

            Directory.CreateDirectory(directoryName);

            await using var fileStream = new FileStream(
                fullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            fileStream.SetLength(0);
            await stream.CopyToAsync(fileStream, cancelToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "保存流出错");
            return false;
        }
    }
}
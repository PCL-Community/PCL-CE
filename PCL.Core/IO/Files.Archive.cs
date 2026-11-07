using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.Exts;

namespace PCL.Core.IO;

public static partial class Files
{
    public static async Task<bool> ExportAsZipArchiveAsync(
        IEnumerable<string> sourceFiles,
        string dialogTitle,
        string defaultFileName,
        string fileFilter,
        string defaultDirectory,
        string tempDirPrefix,
        CancellationToken cancellationToken = default)
    {
        var tempDirName = $"{tempDirPrefix}.tmp";
        var selectedPath = SystemDialogs.SelectSaveFile(
            dialogTitle,
            defaultFileName,
            fileFilter,
            defaultDirectory);

        if (string.IsNullOrEmpty(selectedPath)) return false;

        try
        {
            Directory.CreateDirectory(tempDirName);

            if (File.Exists(selectedPath))
            {
                File.Delete(selectedPath);
                LogWrapper.Info("Files", $"删除已有文件：{selectedPath}");
            }

            await using var fileStream = new FileStream(
                selectedPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                true);
            await using var zipStream = new ZipOutputStream(fileStream);
            foreach (var item in sourceFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var itemFileName = Path.GetFileName(item);
                var tempPath = Path.Combine(tempDirName, itemFileName);

                await CopyFileAsync(item, tempPath, cancellationToken);
                await using (var sourceStream = new FileStream(
                                 tempPath,
                                 FileMode.Open,
                                 FileAccess.Read,
                                 FileShare.Read,
                                 4096,
                                 true))
                {
                    var entry = new ZipEntry(itemFileName);
                    await zipStream.PutNextEntryAsync(entry, cancellationToken);
                    await sourceStream.CopyToAsync(zipStream, cancellationToken);
                }

                File.Delete(tempPath);
            }

            await zipStream.FinishAsync(cancellationToken);
            LogWrapper.Info("Files", $"导出 Zip 成功：{selectedPath}");

            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Log", "导出 Zip 失败");
            return false;
        }
        finally
        {
            if (Directory.Exists(tempDirName))
            {
                Directory.Delete(tempDirName, true);
                LogWrapper.Debug("Log", $"清理临时文件夹：{tempDirName}");
            }
        }
    }

    /// <summary>
    ///     尝试根据文件后缀名判断文件种类并解压，支持 zip、gz、tar、tar.gz 和 bzip2。
    ///     会尝试将 jar 文件以 zip 方式解压。不会清空目标目录，但会创建不存在的目录。
    /// </summary>
    /// <param name="compressFilePath">压缩文件路径</param>
    /// <param name="destDirectory">目标解压目录</param>
    /// <param name="progressIncrementHandler">进度更新回调，接收 0.0 到 1.0 的进度值</param>
    /// <param name="cancellationToken">取消操作的令牌</param>
    /// <returns>异步任务。</returns>
    /// <exception cref="ArgumentNullException">
    ///     当 <paramref name="compressFilePath" /> 或
    ///     <paramref name="destDirectory" /> 为 null 或空时抛出
    /// </exception>
    /// <exception cref="NotSupportedException">当文件格式不受支持时抛出</exception>
    public static async Task ExtractFileAsync(
        string? compressFilePath,
        string? destDirectory,
        Action<double>? progressIncrementHandler = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(compressFilePath))
        {
            LogWrapper.Error(new ArgumentNullException(nameof(compressFilePath)), "压缩文件路径为空");
            return;
        }

        if (string.IsNullOrEmpty(destDirectory))
        {
            LogWrapper.Error(new ArgumentNullException(nameof(destDirectory)), "目标目录路径为空");
            return;
        }

        try
        {
            Directory.CreateDirectory(destDirectory); // 创建目标目录（同步操作，因为通常很快且无异步版本）

            if (compressFilePath.EndsWithF(".gz") || compressFilePath.EndsWithF(".tgz"))
                await _ExtractGZipAsync(
                        compressFilePath,
                        destDirectory,
                        progressIncrementHandler,
                        cancellationToken)
                    .ConfigureAwait(false);
            else if (compressFilePath.EndsWithF(".bz2"))
                await _ExtractBZip2Async(
                        compressFilePath,
                        destDirectory,
                        progressIncrementHandler,
                        cancellationToken)
                    .ConfigureAwait(false);
            else if (compressFilePath.EndsWithF(".tar"))
                await _ExtractTarAsync(
                        compressFilePath,
                        destDirectory,
                        progressIncrementHandler,
                        cancellationToken)
                    .ConfigureAwait(false);
            else if (compressFilePath.EndsWithF(".zip") || compressFilePath.EndsWithF(".jar"))
                await _ExtractZipAsync(
                        compressFilePath,
                        destDirectory,
                        progressIncrementHandler,
                        cancellationToken)
                    .ConfigureAwait(false);
            else
                throw new NotSupportedException("不支持的压缩文件格式");
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"解压文件 {compressFilePath} 失败");
            throw;
        }
    }

    /// <summary>
    ///     异步解压 GZip 文件（包括 .gz、.tgz 和 .tar.gz）。
    /// </summary>
    private static async Task _ExtractGZipAsync(
        string compressFilePath,
        string destDirectory,
        Action<double>? progressIncrementHandler,
        CancellationToken cancellationToken)
    {
        var outputFileName = Path.GetFileName(compressFilePath).ToLower();
        var isTarGZip = outputFileName.EndsWithF(".tar.gz") || outputFileName.EndsWithF(".tgz");

        if (isTarGZip)
            outputFileName = outputFileName
                .Replace(".tar.gz", "")
                .Replace(".tgz", "");
        else if (outputFileName.EndsWithF(".gz"))
            outputFileName = outputFileName.Replace(".gz", "");

        var outputPath = _GetSafePath(destDirectory, outputFileName);

        await using FileStream compressedFile = new(
            compressFilePath,
            FileMode.Open,
            FileAccess.Read);
        await using GZipInputStream gzipStream = new(compressedFile);

        if (isTarGZip)
        {
            // 处理 .tgz / .tar.gz 文件
            await using TarInputStream tarStream = new(gzipStream, Encoding.UTF8);

            await _ExtractTarStreamAsync(
                    tarStream,
                    destDirectory,
                    progressIncrementHandler,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            // 处理普通 .gz 文件
            await using FileStream outputStream = new(
                outputPath,
                FileMode.Create,
                FileAccess.Write);

            await gzipStream
                .CopyToAsync(outputStream, cancellationToken)
                .ConfigureAwait(false);

            progressIncrementHandler?.Invoke(1.0);
        }
    }

    /// <summary>
    ///     异步解压 BZip2 文件。
    /// </summary>
    private static async Task _ExtractBZip2Async(
        string compressFilePath,
        string destDirectory,
        Action<double>? progressIncrementHandler,
        CancellationToken cancellationToken)
    {
        var outputFileName = Path
            .GetFileName(compressFilePath)
            .ToLower()
            .Replace(".bz2", "");

        var outputPath = _GetSafePath(destDirectory, outputFileName);

        await using FileStream compressedFile = new(
            compressFilePath,
            FileMode.Open,
            FileAccess.Read);
        await using BZip2InputStream bzip2Stream = new(compressedFile);
        await using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.Write);

        await bzip2Stream
            .CopyToAsync(outputStream, cancellationToken)
            .ConfigureAwait(false);

        progressIncrementHandler?.Invoke(1.0);
    }

    /// <summary>
    ///     异步解压 Tar 文件。
    /// </summary>
    private static async Task _ExtractTarAsync(
        string compressFilePath,
        string destDirectory,
        Action<double>? progressIncrementHandler,
        CancellationToken cancellationToken)
    {
        await using FileStream compressedFile = new(
            compressFilePath,
            FileMode.Open,
            FileAccess.Read);
        await using TarInputStream tarStream = new(compressedFile, Encoding.UTF8);

        await _ExtractTarStreamAsync(
                tarStream,
                destDirectory,
                progressIncrementHandler,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     异步解压 Tar 流中的内容到指定目录。
    /// </summary>
    /// <param name="tarStream">要解压的 Tar 输入流。</param>
    /// <param name="destDirectory">目标解压目录。</param>
    /// <param name="progressIncrementHandler">进度更新回调，接收 0.0 到 1.0 的进度值（基于条目计数）。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <exception cref="OperationCanceledException">如果操作被取消。</exception>
    /// <exception cref="InvalidOperationException">如果路径不合法或解压失败。</exception>
    /// <exception cref="IOException">如果发生 IO 相关错误。</exception>
    private static async Task _ExtractTarStreamAsync(
        TarInputStream tarStream,
        string destDirectory,
        Action<double>? progressIncrementHandler,
        CancellationToken cancellationToken)
    {
        var entries = new List<TarEntry>();
        long totalBytes = 0;

        while (await _GetNextEntryAsync(tarStream, cancellationToken).ConfigureAwait(false) is
               { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();

            entries.Add(entry);
            totalBytes += entry.Size;
        }

        long processedBytes = 0;

        tarStream.Reset(); // 重置流以重新读取条目

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var destinationPath = _GetSafePath(destDirectory, entry.Name);

                if (entry.IsDirectory)
                {
                    await _CreateDirectoryAsync(destinationPath, cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                await _CreateDirectoryAsync(
                        Path.GetDirectoryName(destinationPath)!,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (entry.Size == 0)
                {
                    await File.Create(destinationPath).DisposeAsync();
                    continue;
                }

                await using FileStream outputStream = new(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write);

                await tarStream
                    .CopyEntryContentsAsync(outputStream, cancellationToken)
                    .ConfigureAwait(false);

                processedBytes += entry.Size;
                progressIncrementHandler?.Invoke((double)processedBytes / totalBytes);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to extract entry {entry.Name}: {ex.Message}",
                    ex);
            }
        }
    }

    private static async Task<TarEntry?> _GetNextEntryAsync(
        TarInputStream tarStream,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var task = Task.Run(tarStream.GetNextEntry, cts.Token);

        if (await Task
                .WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), cts.Token))
                .ConfigureAwait(false)
            == task)
            return await task.ConfigureAwait(false);

        throw new TimeoutException("Operation timed out while reading next Tar entry.");
    }

    private static string _GetSafePath(
        string destDirectory,
        string entryName)
    {
        var destinationRoot =
            Path
                .GetFullPath(destDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        var fullPath = Path.GetFullPath(Path.Combine(destinationRoot, entryName));

        return fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : throw new InvalidOperationException($"Invalid path detected: {entryName}");
    }

    private static async Task _CreateDirectoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await Task
            .Run(() => Directory.CreateDirectory(path), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     异步解压 Zip 文件（包括 .zip 和 .jar）。
    /// </summary>
    private static async Task _ExtractZipAsync(
        string compressFilePath,
        string destDirectory,
        Action<double>? progressIncrementHandler,
        CancellationToken cancellationToken)
    {
        using ZipFile zipFile = new(compressFilePath);

        var totalEntries = zipFile.Count;
        long currentEntry = 0;

        foreach (ZipEntry entry in zipFile)
        {
            var destinationPath = _GetSafePath(destDirectory, entry.Name);

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            await using var zipStream = zipFile.GetInputStream(entry);
            await using FileStream outputStream = new(
                destinationPath,
                FileMode.Create,
                FileAccess.Write);

            await zipStream
                .CopyToAsync(outputStream, cancellationToken)
                .ConfigureAwait(false);

            currentEntry++;
            progressIncrementHandler?.Invoke((double)currentEntry / totalEntries);
        }
    }
}
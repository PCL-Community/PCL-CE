using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.IO;

public static class Directories
{
    /// <summary>
    ///     异步检查是否拥有对指定文件夹的实际写入权限。
    ///     通过创建并删除临时探测文件确认真实 I/O 能力，避免仅靠 ACL 推断造成误判。
    /// </summary>
    /// <param name="path">要检查的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>如果文件夹存在且可实际写入，返回 true；否则返回 false。</returns>
    public static async Task<bool> CheckPermissionAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await CheckPermissionWithExceptionAsync(path, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Warn("权限检查被取消");
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"没有对文件夹 {path} 的权限，请尝试以管理员权限运行。");
            return false;
        }
    }

    /// <summary>
    ///     异步检查文件夹权限，若无权限或文件夹不存在则抛出异常。
    ///     通过创建并删除临时探测文件确认真实 I/O 能力。
    /// </summary>
    /// <param name="path">要检查的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <exception cref="ArgumentNullException">路径为空或只包含空白字符。</exception>
    /// <exception cref="DirectoryNotFoundException">文件夹不存在。</exception>
    /// <exception cref="UnauthorizedAccessException">无访问权限。</exception>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task CheckPermissionWithExceptionAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path), "文件夹路径不能为空！");

        if (_IsSystemProtectedFolder(path))
            throw new UnauthorizedAccessException($"无法访问受保护的系统文件夹：{path}");

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"文件夹不存在：{path}");

        cancellationToken.ThrowIfCancellationRequested();

        // 验证读取 / 枚举权限
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"无法读取文件夹 {path}：{ex.Message}", ex);
        }

        var probePath = Path.Combine(path, $".pcl-permission-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(probePath, [], cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _TryDeleteProbeFileBestEffort(probePath);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            _TryDeleteProbeFileBestEffort(probePath);
            throw;
        }
        catch (Exception ex)
        {
            _TryDeleteProbeFileBestEffort(probePath);
            throw new UnauthorizedAccessException($"无法写入文件夹 {path}：{ex.Message}", ex);
        }

        try
        {
            if (File.Exists(probePath)) File.Delete(probePath);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(
                $"无法删除文件夹 {path} 中的权限检查临时文件：{ex.Message}",
                ex);
        }
    }

    private static void _TryDeleteProbeFileBestEffort(string probePath)
    {
        try
        {
            if (File.Exists(probePath)) File.Delete(probePath);
        }
        catch
        {
            // 权限检查失败后的兜底清理，不覆盖原始异常。
        }
    }

    /// <summary>
    ///     检查是否为受保护的系统文件夹。
    /// </summary>
    private static bool _IsSystemProtectedFolder(string path)
    {
        return path.EndsWith(":\\System Volume Information", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(":\\$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     异步删除文件夹及其内容，返回删除的文件数。支持忽略错误。
    /// </summary>
    /// <param name="path">要删除的文件夹路径。</param>
    /// <param name="ignoreIssue">是否忽略删除过程中的错误。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>成功删除的文件数。</returns>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task<int> DeleteDirectoryAsync(
        string? path,
        bool ignoreIssue = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;

        var deletedCount = 0;

        try
        {
            // 枚举文件，延迟加载以提高性能
            foreach (var filePath in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var attempt = 0; attempt < 2; attempt++)
                    try
                    {
                        await _FileDeleteAsync(filePath, cancellationToken).ConfigureAwait(false);
                        deletedCount++;
                        break;
                    }
                    catch (Exception ex) when (attempt == 0)
                    {
                        LogWrapper.Error(ex, $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (ignoreIssue)
                            LogWrapper.Error(ex, "删除单个文件可忽略地失败");
                        else
                            throw;
                    }
            }

            // 递归删除子目录
            foreach (var subDir in Directory.EnumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                deletedCount +=
                    await DeleteDirectoryAsync(subDir, ignoreIssue, cancellationToken)
                        .ConfigureAwait(false);
            }

            // 删除空目录
            for (var attempt = 0; attempt < 2; attempt++)
                try
                {
                    Directory.Delete(path, true);
                    break;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    LogWrapper.Error(ex, $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (ignoreIssue)
                        LogWrapper.Error(ex, "删除单个文件夹可忽略地失败");
                    else
                        throw;
                }
        }
        catch (DirectoryNotFoundException ex)
        {
            // 处理疑似符号链接的情况
            LogWrapper.Error(ex, $"疑似为孤立符号链接，尝试直接删除（{path}）", "Developer");
            try
            {
                Directory.Delete(path);
            }
            catch (Exception deleteEx)
            {
                if (!ignoreIssue) throw;

                LogWrapper.Error(deleteEx, $"删除符号链接文件夹失败（{path}）");
            }
        }

        return deletedCount;
    }

    /// <summary>
    ///     异步复制文件夹及其内容，失败时抛出异常。
    /// </summary>
    /// <param name="fromPath">源文件夹路径。</param>
    /// <param name="toPath">目标文件夹路径。</param>
    /// <param name="progressIncrementHandler">进度增量回调，每复制一个文件传入本次增加的进度值。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <exception cref="ArgumentNullException">源或目标文件夹路径为空。</exception>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task CopyDirectoryAsync(
        string? fromPath,
        string? toPath,
        Action<double>? progressIncrementHandler = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fromPath))
            throw new ArgumentNullException(nameof(fromPath), "源文件夹路径为空");

        if (string.IsNullOrEmpty(toPath))
            throw new ArgumentNullException(nameof(toPath), "目标文件夹路径为空");

        // 规范化路径
        fromPath = Path
                       .GetFullPath(fromPath)
                       .TrimEnd(
                           Path.DirectorySeparatorChar,
                           Path.AltDirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        toPath = Path
                     .GetFullPath(toPath)
                     .TrimEnd(
                         Path.DirectorySeparatorChar,
                         Path.AltDirectorySeparatorChar)
                 + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(toPath);

        var allFiles =
            (await EnumerateFilesAsync(fromPath, cancellationToken).ConfigureAwait(false)).ToList();
        var totalFiles = allFiles.Count;
        if (totalFiles == 0)
        {
            progressIncrementHandler?.Invoke(1d);
            return;
        }

        var progressStep = 1d / totalFiles;

        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = file.FullName[fromPath.Length..];
            var destFilePath = Path.Combine(toPath, relativePath);

            // 确保目标目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);

            for (var attempt = 0; attempt < 2; attempt++)
                try
                {
                    await _FileCopyAsync(
                        file.FullName,
                        destFilePath,
                        true,
                        cancellationToken).ConfigureAwait(false);
                    progressIncrementHandler?.Invoke(progressStep);
                    break;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    LogWrapper.Error(
                        ex,
                        $"复制文件失败，将在 0.3s 后重试（{file.FullName} 到 {destFilePath}）");
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(
                        ex,
                        $"复制文件失败（{file.FullName} 到 {destFilePath}）");
                    throw;
                }
        }
    }

    /// <summary>
    ///     异步遍历文件夹中的所有文件。
    /// </summary>
    /// <param name="directory">要遍历的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>文件信息的枚举器。</returns>
    public static async Task<IEnumerable<FileInfo>> EnumerateFilesAsync(
        string? directory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"目录不存在：{directory}");

        try
        {
            // DirectoryInfo.EnumerateFiles 是同步的，使用 Task.Run 包装
            return await Task
                .Run(
                    () => new DirectoryInfo(directory)
                        .EnumerateFiles("*", SearchOption.AllDirectories)
                        .ToList(),
                    cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Warn("文件夹遍历被取消");
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, $"遍历文件夹失败（{directory}）");
            return [];
        }
    }

    /// <summary>
    ///     异步遍历文件夹中的所有文件。目录不存在时返回空集合。
    /// </summary>
    public static async Task<IEnumerable<FileInfo>> EnumerateFilesOrEmptyAsync(
        string? directory,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(directory) || !Directory.Exists(directory)
            ? []
            : await EnumerateFilesAsync(directory, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     异步移动目录中的全部文件和子目录到目标目录。目标目录不存在时会自动创建。
    /// </summary>
    public static async Task MoveDirectoryAsync(
        string sourceDir,
        string targetDir,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceDir))
            throw new ArgumentNullException(nameof(sourceDir));
        if (string.IsNullOrWhiteSpace(targetDir))
            throw new ArgumentNullException(nameof(targetDir));
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"源目录不存在：{sourceDir}");

        Directory.CreateDirectory(targetDir);

        foreach (var filePath in Directory.EnumerateFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = Path.Combine(targetDir, Path.GetFileName(filePath));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await Task
                .Run(
                    () => File.Move(filePath, targetPath, true),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var childDir in Directory.EnumerateDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childTarget = Path.Combine(targetDir, Path.GetFileName(childDir));
            await MoveDirectoryAsync(childDir, childTarget, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    // 辅助方法：异步打开 FileStream
    private static async Task<FileStream> _FileStreamOpenAsync(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        CancellationToken cancellationToken)
    {
        var fs = new FileStream(
            path,
            mode,
            access,
            share,
            4096,
            true);
        await Task.Yield(); // 确保异步上下文
        cancellationToken.ThrowIfCancellationRequested();
        return fs;
    }

    // 辅助方法：异步删除文件
    private static async Task _FileDeleteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await Task
            .Run(
                () => File.Delete(path),
                cancellationToken)
            .ConfigureAwait(false);
    }

    // 辅助方法：异步复制文件
    private static async Task _FileCopyAsync(
        string sourceFileName,
        string destFileName,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        await using FileStream sourceStream = new(
            sourceFileName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            true);
        await using FileStream destStream = new(
            destFileName,
            overwrite
                ? FileMode.Create
                : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            true);
        await sourceStream
            .CopyToAsync(destStream, cancellationToken)
            .ConfigureAwait(false);
    }
}
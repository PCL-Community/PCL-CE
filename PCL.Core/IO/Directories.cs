using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO;

/// <summary>目录级文件系统操作，权限检查与删除均保持跨平台语义。</summary>
public static class Directories
{
    /// <summary>同步删除目录及其内容，目录不存在时不做任何操作。</summary>
    public static void DeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        Directory.Delete(path, true);
    }

    /// <summary>判断目录是否存在且当前进程可以写入。</summary>
    public static bool CanWrite(string path)
    {
        try
        {
            EnsureWritable(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>判断目录是否存在且当前进程可以异步写入。</summary>
    public static async Task<bool> CanWriteAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureWritableAsync(path, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>确保目录存在且当前进程可以写入，失败时抛出明确异常。</summary>
    /// <exception cref="ArgumentNullException">路径为空。</exception>
    /// <exception cref="DirectoryNotFoundException">目录不存在。</exception>
    /// <exception cref="UnauthorizedAccessException">目录不可写。</exception>
    public static void EnsureWritable(string path)
    {
        var directory = _NormalizeDirectory(path);
        var probePath = _CreateProbePath(directory);
        try
        {
            using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
            stream.WriteByte(0);
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (DirectoryNotFoundException)
        {
            throw;
        }
        catch (PathTooLongException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"无法写入文件夹 {directory}：{ex.Message}", ex);
        }
        finally
        {
            _TryDeleteProbe(probePath);
        }
    }

    /// <summary>异步确保目录存在且当前进程可以写入，失败时抛出明确异常。</summary>
    public static async Task EnsureWritableAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var directory = _NormalizeDirectory(path);
        var probePath = _CreateProbePath(directory);
        try
        {
            await using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await stream
                .WriteAsync(
                    new byte[]
                    {
                        0
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (DirectoryNotFoundException)
        {
            throw;
        }
        catch (PathTooLongException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException($"无法写入文件夹 {directory}：{ex.Message}", ex);
        }
        finally
        {
            _TryDeleteProbe(probePath);
        }
    }

    private static string _NormalizeDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path), "文件夹路径不能为空！");

        var directory = Path.GetFullPath(path);
        return !Directory.Exists(directory)
            ? throw new DirectoryNotFoundException($"文件夹不存在：{directory}")
            : directory;
    }

    private static string _CreateProbePath(string directory)
    {
        return Path.Combine(directory, $".pcl-write-{Guid.NewGuid():N}");
    }

    private static void _TryDeleteProbe(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // 清理探针失败不应覆盖真正的权限异常。
        }
    }
}
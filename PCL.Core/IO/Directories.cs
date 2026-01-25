using System.Security.AccessControl;

namespace PCL.Core.IO;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logging;

public static class Directories {
    /// <summary>
    /// 异步检查是否拥有对指定文件夹的读写权限。
    /// 如果文件夹不存在或没有权限，返回 false，不修改文件系统。
    /// </summary>
    /// <param name="path">要检查的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>如果拥有读写权限且文件夹存在，返回 true；否则返回 false。</returns>
    public static async Task<bool> CheckPermissionAsync(string? path, CancellationToken cancellationToken = default) {
        try {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            // 排除特殊系统文件夹
            if (IsSystemProtectedFolder(path)) {
                return false;
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(path)) {
                return false;
            }

            // 检查目录访问权限
            var directoryInfo = new DirectoryInfo(path);
            var security = await Task.Run(() => directoryInfo.GetAccessControl(), cancellationToken);
            var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));

            // 检查当前用户是否有读写权限，优先考虑拒绝规则
            var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(currentUser);

            var isDenied = false;
            var isAllowed = false;

            foreach (FileSystemAccessRule rule in rules) {
                if (!rule.FileSystemRights.HasFlag(FileSystemRights.Write))
                    continue;

                // 检查规则是否适用于当前用户或其组
                if (principal.IsInRole(rule.IdentityReference.Value)) {
                    if (rule.AccessControlType == AccessControlType.Deny) {
                        isDenied = true;
                        break; // 拒绝优先，直接返回
                    }
                    if (rule.AccessControlType == AccessControlType.Allow) {
                        isAllowed = true;
                    }
                }
            }

            if (isDenied || !isAllowed) {
                return false;
            }

            // 尝试枚举目录内容以确认实际访问能力
            await Task.Run(() => Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Any(), cancellationToken);
            return true;
        } catch (OperationCanceledException) {
            LogWrapper.Warn("权限检查被取消");
            return false;
        } catch (Exception ex) {
            LogWrapper.Warn(ex, $"没有对文件夹 {path} 的权限，请尝试以管理员权限运行。");
            return false;
        }
    }

    /// <summary>
    /// 异步检查文件夹权限，若无权限或文件夹不存在则抛出异常。
    /// 不修改文件系统。
    /// </summary>
    /// <param name="path">要检查的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <exception cref="ArgumentNullException">路径为空或只包含空白字符。</exception>
    /// <exception cref="DirectoryNotFoundException">文件夹不存在。</exception>
    /// <exception cref="UnauthorizedAccessException">无访问权限。</exception>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task CheckPermissionWithExceptionAsync(string? path, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new ArgumentNullException(nameof(path), "文件夹路径不能为空！");
        }

        if (IsSystemProtectedFolder(path)) {
            throw new UnauthorizedAccessException($"无法访问受保护的系统文件夹：{path}");
        }

        if (!Directory.Exists(path)) {
            throw new DirectoryNotFoundException($"文件夹不存在：{path}");
        }

        try {
            var directoryInfo = new DirectoryInfo(path);
            var security = await Task.Run(() => directoryInfo.GetAccessControl(), cancellationToken);
            var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount));

            var hasAccess = rules.Cast<FileSystemAccessRule>()
                .Any(rule => rule.FileSystemRights.HasFlag(FileSystemRights.Write) &&
                             rule.AccessControlType == AccessControlType.Allow);

            if (!hasAccess) {
                throw new UnauthorizedAccessException($"没有对文件夹 {path} 的写权限");
            }

            // 确认实际访问能力
            await Task.Run(() => Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Any(), cancellationToken);
        } catch (UnauthorizedAccessException) {
            throw;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            throw new UnauthorizedAccessException($"无法访问文件夹 {path}：{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 检查是否为受保护的系统文件夹。
    /// </summary>
    private static bool IsSystemProtectedFolder(string path) {
        return path.EndsWith(":\\System Volume Information", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(":\\$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 异步删除文件夹及其内容，返回删除的文件数。支持忽略错误。
    /// </summary>
    /// <param name="path">要删除的文件夹路径。</param>
    /// <param name="ignoreIssue">是否忽略删除过程中的错误。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>成功删除的文件数。</returns>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task<int> DeleteDirectoryAsync(string? path, bool ignoreIssue = false, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
            return 0;
        }

        var deletedCount = 0;

        try {
            // 枚举文件，延迟加载以提高性能
            foreach (var filePath in Directory.EnumerateFiles(path)) {
                cancellationToken.ThrowIfCancellationRequested();

                for (var attempt = 0; attempt < 2; attempt++) {
                    try {
                        await FileDeleteAsync(filePath, cancellationToken).ConfigureAwait(false);
                        deletedCount++;
                        break;
                    } catch (Exception ex) when (attempt == 0) {
                        LogWrapper.Error(ex, $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    } catch (Exception ex) {
                        if (ignoreIssue) {
                            LogWrapper.Error(ex, "删除单个文件可忽略地失败");
                        } else {
                            throw;
                        }
                    }
                }
            }

            // 递归删除子目录
            foreach (var subDir in Directory.EnumerateDirectories(path)) {
                cancellationToken.ThrowIfCancellationRequested();
                deletedCount += await DeleteDirectoryAsync(subDir, ignoreIssue, cancellationToken).ConfigureAwait(false);
            }

            // 删除空目录
            for (var attempt = 0; attempt < 2; attempt++) {
                try {
                    Directory.Delete(path, true);
                    break;
                } catch (Exception ex) when (attempt == 0) {
                    LogWrapper.Error(ex, $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                } catch (Exception ex) {
                    if (ignoreIssue) {
                        LogWrapper.Error(ex, "删除单个文件夹可忽略地失败");
                    } else {
                        throw;
                    }
                }
            }
        } catch (DirectoryNotFoundException ex) {
            // 处理疑似符号链接的情况
            LogWrapper.Error(ex, $"疑似为孤立符号链接，尝试直接删除（{path}）", "Developer");
            try {
                Directory.Delete(path);
            } catch (Exception deleteEx) {
                if (!ignoreIssue) {
                    throw;
                }
                LogWrapper.Error(deleteEx, $"删除符号链接文件夹失败（{path}）");
            }
        }

        return deletedCount;
    }

    /// <summary>
    /// 异步复制文件夹及其内容，失败时抛出异常。
    /// </summary>
    /// <param name="fromPath">源文件夹路径。</param>
    /// <param name="toPath">目标文件夹路径。</param>
    /// <param name="progressIncrementHandler">进度更新回调，接收 0 到 1 的进度值。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <exception cref="ArgumentNullException">源或目标文件夹路径为空。</exception>
    /// <exception cref="OperationCanceledException">操作被取消。</exception>
    public static async Task CopyDirectoryAsync(string? fromPath, string? toPath, Action<double>? progressIncrementHandler = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(fromPath)) {
            throw new ArgumentNullException(nameof(fromPath), "源文件夹路径为空");
        }

        if (string.IsNullOrEmpty(toPath)) {
            throw new ArgumentNullException(nameof(toPath), "目标文件夹路径为空");
        }

        // 规范化路径
        fromPath = Path.GetFullPath(fromPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        toPath = Path.GetFullPath(toPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var allFiles = (await EnumerateFilesAsync(fromPath, cancellationToken).ConfigureAwait(false)).ToList();
        var totalFiles = allFiles.Count;
        long copiedFiles = 0;

        foreach (var file in allFiles) {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = file.FullName[fromPath.Length..];
            var destFilePath = Path.Combine(toPath, relativePath);

            // 确保目标目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);

            for (var attempt = 0; attempt < 2; attempt++) {
                try {
                    await FileCopyAsync(file.FullName, destFilePath, overwrite: true, cancellationToken).ConfigureAwait(false);
                    copiedFiles++;
                    progressIncrementHandler?.Invoke((double)copiedFiles / totalFiles);
                    break;
                } catch (Exception ex) when (attempt == 0) {
                    LogWrapper.Error(ex, $"复制文件失败，将在 0.3s 后重试（{file.FullName} 到 {destFilePath}）");
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                } catch (Exception ex) {
                    LogWrapper.Error(ex, $"复制文件失败（{file.FullName} 到 {destFilePath}）");
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// 异步遍历文件夹中的所有文件。
    /// </summary>
    /// <param name="directory">要遍历的文件夹路径。</param>
    /// <param name="cancellationToken">取消操作的令牌。</param>
    /// <returns>文件信息的枚举器。</returns>
    public static async Task<IEnumerable<FileInfo>> EnumerateFilesAsync(string? directory, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) {
            throw new DirectoryNotFoundException($"目录不存在：{directory}");
        }

        try {
            // DirectoryInfo.EnumerateFiles 是同步的，使用 Task.Run 包装
            return await Task.Run(() => new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories).ToList(), cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) {
            LogWrapper.Warn("文件夹遍历被取消");
            return [];
        } catch (Exception ex) {
            LogWrapper.Error(ex, $"遍历文件夹失败（{directory}）");
            return [];
        }
    }

    // 辅助方法：异步打开 FileStream
    private static async Task<FileStream> FileStreamOpenAsync(string path, FileMode mode, FileAccess access, FileShare share, CancellationToken cancellationToken) {
        var fs = new FileStream(path, mode, access, share, bufferSize: 4096, useAsync: true);
        await Task.Yield(); // 确保异步上下文
        cancellationToken.ThrowIfCancellationRequested();
        return fs;
    }

    // 辅助方法：异步删除文件
    private static async Task FileDeleteAsync(string path, CancellationToken cancellationToken) {
        await Task.Run(() => File.Delete(path), cancellationToken).ConfigureAwait(false);
    }

    // 辅助方法：异步复制文件
    private static async Task FileCopyAsync(string sourceFileName, string destFileName, bool overwrite, CancellationToken cancellationToken) {
        await using FileStream sourceStream = new(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        await using FileStream destStream = new(destFileName, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await sourceStream.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);
    }
}

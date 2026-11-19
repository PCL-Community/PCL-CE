using System;
using System.IO;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;

namespace PCL.Core.IO;

public static partial class Files
{
    /// <summary>
    ///     异步计算文件的指定哈希值。
    /// </summary>
    /// <param name="filePath">要计算哈希的文件路径</param>
    /// <param name="hashProvider">哈希算法提供者（如 MD5Provider、SHA1Provider 等）</param>
    /// <param name="ignoreIfBusy">是否忽略被占用的文件</param>
    /// <returns>哈希值（十六进制字符串），失败时返回空字符串</returns>
    public static async Task<string> ComputeFileHashAsync(
        string? filePath,
        IHashProvider hashProvider,
        bool ignoreIfBusy = false)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            LogWrapper.Warn(new ArgumentNullException(nameof(filePath)), "文件路径为空");
            return string.Empty;
        }

        if (ignoreIfBusy && await CheckFileBusyAsync(filePath).ConfigureAwait(false))
            return string.Empty;

        for (var attempt = 0; attempt < 2; attempt++)
            try
            {
                await using FileStream fs = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                return (await hashProvider.ComputeHashAsync(fs).ConfigureAwait(false))
                    .ToHexString();
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                LogWrapper.Warn(ex, $"计算文件哈希失败：{filePath}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                if (attempt == 0)
                {
                    LogWrapper.Warn(ex, $"计算文件哈希可重试失败：{filePath}");
                    await Task.Delay(Random.Shared.Next(200, 500)).ConfigureAwait(false);
                    continue;
                }

                LogWrapper.Warn(ex, $"计算文件哈希失败：{filePath}");
                return string.Empty;
            }

        return string.Empty;
    }


    /// <summary>
    ///     异步检查文件是否被占用。
    /// </summary>
    /// <returns>若被占用则为 true，否则为 false</returns>
    public static async Task<bool> CheckFileBusyAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;
            await using FileStream fs = new(
                filePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ReSharper disable InconsistentNaming

    /// <summary>
    ///     异步获取文件的 MD5 哈希值。
    /// </summary>
    public static Task<string> GetFileMD5Async(string? filePath)
    {
        return ComputeFileHashAsync(filePath, MD5Provider.Instance);
    }

    /// <summary>
    ///     异步获取文件的 SHA1 哈希值。
    /// </summary>
    public static Task<string> GetFileSHA1Async(string? filePath)
    {
        return ComputeFileHashAsync(filePath, SHA1Provider.Instance);
    }

    /// <summary>
    ///     异步获取文件的 SHA256 哈希值。
    /// </summary>
    public static Task<string> GetFileSHA256Async(string? filePath, bool ignoreIfBusy = false)
    {
        return ComputeFileHashAsync(filePath, SHA256Provider.Instance, ignoreIfBusy);
    }

    /// <summary>
    ///     异步获取文件的 SHA512 哈希值。
    /// </summary>
    public static Task<string> GetFileSHA512Async(string? filePath, bool ignoreIfBusy = false)
    {
        return ComputeFileHashAsync(filePath, SHA512Provider.Instance, ignoreIfBusy);
    }

    // ReSharper restore InconsistentNaming
}
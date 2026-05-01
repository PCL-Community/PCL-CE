using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PCL.Core.IO;


/// <summary>
/// 文件的校验规则。
/// </summary>
public sealed class FileChecker
{
    /// <summary>
    /// 文件的准确大小。
    /// 不检查则为 -1。
    /// </summary>
    public readonly long ActualSize = -1;

    /// <summary>
    /// 是否可以使用已经存在的文件。
    /// </summary>
    public bool CanUseExistsFile;

    /// <summary>
    /// 文件的 MD5、SHA1 或 SHA256。会根据输入字符串的长度自动判断种类。
    /// 不检查则为 Nothing。
    /// </summary>
    public readonly string Hash;

    /// <summary>
    /// 是否要求为 JSON 文件。
    /// 即，开头结尾必须为 {} 或 []。
    /// </summary>
    public readonly bool IsJson;

    /// <summary>
    /// 文件的最小大小。
    /// 不检查则为 -1。
    /// </summary>
    public readonly long MinSize = -1;

    public FileChecker(
        long minSize = -1,
        long actualSize = -1,
        string? hash = null,
        bool canUseExistsFile = true,
        bool isJson = false)
    {
        ActualSize = actualSize;
        MinSize = minSize;
        Hash = hash ?? string.Empty;
        CanUseExistsFile = canUseExistsFile;
        IsJson = isJson;
    }

    /// <summary>
    /// 检查文件。若成功则返回 Nothing，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
    /// </summary>
    /// <exception cref="JsonException">文件内容不是JSON类型</exception>
    public async Task<string?> CheckAsync(string localPath)
    {
        try
        {
            LogWrapper.Debug($"[Checker] 开始校验文件 {localPath}");
            var info = new FileInfo(localPath);
            if (!info.Exists)
            {
                return "文件不存在：" + localPath;
            }

            var fileSize = info.Length;
            var errorMessage = new List<string>();
            var allowWrongSize = false; // 允许相信哈希正确但是大小不正确
            if (!string.IsNullOrEmpty(Hash))
            {
                if (Hash.Length < 35) // MD5
                {
                    var computedHash = await Files.GetFileMD5Async(localPath).ConfigureAwait(false);
                    if ((Hash.ToLowerInvariant() ?? string.Empty) != (computedHash))
                    {
                        errorMessage.Add("文件 MD5 应为 " + Hash + "，实际为 " + computedHash);
                    }
                }
                else if (Hash.Length == 64) // SHA256
                {
                    var computedHash = await Files.GetFileSHA256Async(localPath).ConfigureAwait(false);
                    if ((Hash.ToLowerInvariant() ?? string.Empty) != (computedHash ?? string.Empty))
                    {
                        errorMessage.Add("文件 SHA256 应为 " + Hash + "，实际为 " + computedHash);
                    }
                }
                else // SHA1 (40)
                {
                    var computedHash = await Files.GetFileSHA1Async(localPath).ConfigureAwait(false);
                    if ((Hash.ToLowerInvariant() ?? string.Empty) != (computedHash ?? string.Empty))
                    {
                        errorMessage.Add("文件 SHA1 应为 " + Hash + "，实际为 " + computedHash);
                    }
                }

                allowWrongSize = errorMessage.Count == 0;
            }

            if (ActualSize >= 0L && ActualSize != fileSize && !allowWrongSize) // 不允许忽略大小不正确的情况
            {
                errorMessage.Add($"文件大小应为 {ActualSize} B，实际为 {fileSize} B" +
                                 (fileSize < 2000L ? "，内容为" + await File.ReadAllTextAsync(localPath).ConfigureAwait(false) : string.Empty));
            }

            if (MinSize >= 0L && MinSize > fileSize)
            {
                errorMessage.Add($"文件大小应大于 {MinSize} B，实际为 {fileSize} B" +
                                 (fileSize < 2000L ? "，内容为：" + await File.ReadAllTextAsync(localPath).ConfigureAwait(false) : string.Empty));
            }

            if (IsJson)
            {
                var content = await File.ReadAllTextAsync(localPath).ConfigureAwait(false);
                if (string.IsNullOrEmpty(content))
                {
                    throw new InvalidDataException("读取到的文件为空");
                }

                _ = JsonDocument.Parse(content);
            }

            if (errorMessage.Count != 0)
            {
                errorMessage.Insert(0, $"实际校验地址：{localPath}");
                return string.Join(';', errorMessage);
            }

            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "检查文件时出错");
            throw;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.IO;

public static partial class Files
{
    public static readonly JsonSerializerOptions PrettierJsonOptions =
        new(JsonCompat.SerializerOptions)
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    /// <summary>
    ///     合并两个 JSON 对象，源 JSON 覆盖目标 JSON 的同名键，数组去重合并。
    /// </summary>
    /// <param name="target">目标 JSON 对象。</param>
    /// <param name="source">源 JSON 对象，优先级高于目标对象。</param>
    /// <returns>合并后的 JSON 对象。如果输入无效，返回源或目标的深拷贝。</returns>
    /// <exception cref="ArgumentNullException">如果 target 和 source 均为 null，则抛出异常。</exception>
    public static JsonNode MergeJson(JsonNode target, JsonNode source)
    {
        if (target is null && source is null)
            throw new ArgumentNullException(nameof(target), "目标和源 JSON 不能同时为 null。");

        if (target is not JsonObject targetObj || source is not JsonObject sourceObj)
            return source.DeepClone();

        var result = (JsonObject)targetObj.DeepClone(); // 克隆以避免修改原始对象

        foreach (var (key, sourceValue) in sourceObj)
        {
            var targetValue = result[key];

            switch (sourceValue)
            {
                case null:
                    continue;

                case JsonObject when targetValue is JsonObject:
                    result[key] = MergeJson(targetValue, sourceValue);
                    break;

                case JsonArray sourceArray when targetValue is JsonArray targetArray:
                {
                    var uniqueValues = new HashSet<string>(StringComparer.Ordinal);
                    JsonArray mergedArray = [];

                    foreach (var item in targetArray)
                    {
                        if (item is null) continue;

                        var itemStr = item.ToJsonString();
                        if (uniqueValues.Add(itemStr)) mergedArray.Add(item.DeepClone());
                    }

                    foreach (var item in sourceArray)
                    {
                        if (item is null) continue;

                        var itemStr = item.ToJsonString();
                        if (uniqueValues.Add(itemStr)) mergedArray.Add(item.DeepClone());
                    }

                    result[key] = mergedArray;
                    break;
                }

                default:
                    result[key] = sourceValue.DeepClone();
                    break;
            }
        }

        return result;
    }

    /// <summary>
    ///     检查文件。若成功则返回 null，失败则返回错误的描述文本，描述文本不以句号结尾。不会抛出错误。
    /// </summary>
    public static async Task<string?> CheckAsync(
        string localPath,
        long minSize = -1,
        long actualSize = -1,
        string? hash = null,
        bool isJson = false)
    {
        try
        {
            LogWrapper.Debug("Checker", $"开始校验文件 {localPath}");
            var info = new FileInfo(localPath);
            if (!info.Exists) return $"文件不存在：{localPath}";

            var fileSize = info.Length;
            var errors = new StringBuilder();
            var allowSizeMismatch = false; // 允许相信哈希正确但是大小不正确

            if (!string.IsNullOrEmpty(hash))
            {
                var computedHash = hash.Length switch
                {
                    < 35 => await GetFileMD5Async(localPath), // MD5
                    64 => await GetFileSHA256Async(localPath), // SHA256
                    _ => await GetFileSHA1Async(localPath) // SHA1 (40)
                };

                if (!string.Equals(hash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    var hashType = hash.Length switch
                    {
                        < 35 => "MD5",
                        64 => "SHA256",
                        _ => "SHA1"
                    };
                    errors.AppendLine($"文件 {hashType} 应为 {hash}，实际为 {computedHash}");
                }
                else
                {
                    allowSizeMismatch = true;
                }
            }

            // 检查实际大小
            if (actualSize >= 0 && actualSize != fileSize && !allowSizeMismatch)
            {
                var contentPreview =
                    fileSize < 2000 ? await ReadAllTextOrEmptyAsync(localPath) : "";
                errors.AppendLine(
                    $"文件大小应为 {actualSize} B，实际为 {fileSize} B"
                    + (string.IsNullOrEmpty(contentPreview) ? "" : $"，内容为 {contentPreview}"));
            }

            // 检查最小大小
            if (minSize >= 0 && minSize > fileSize)
            {
                var contentPreview =
                    fileSize < 2000 ? await ReadAllTextOrEmptyAsync(localPath) : "";
                errors.AppendLine(
                    $"文件大小应大于 {minSize} B，实际为 {fileSize} B"
                    + (string.IsNullOrEmpty(contentPreview) ? "" : $"，内容为 {contentPreview}"));
            }

            // JSON 检查
            if (isJson)
            {
                var content = await ReadAllTextOrEmptyAsync(localPath);
                if (string.IsNullOrEmpty(content)) throw new Exception("读取到的文件为空");
                try
                {
                    using var document = JsonDocument.Parse(content, JsonCompat.DocumentOptions);
                    // 简单验证 JSON 有效性
                }
                catch (JsonException ex)
                {
                    throw new InvalidJsonException(ex);
                }
            }

            if (errors.Length <= 0) return null;

            errors.Insert(0, $"实际校验地址：{localPath}\n");
            return errors.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Checker", $"检查文件出错: {ex}");
            return ex.ToString();
        }
    }
}
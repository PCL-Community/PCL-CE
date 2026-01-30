using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using PCL.Core.IO;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Instance.Resources;

// 表示Minecraft版本JSON中的assetIndex字段
public class AssetIndex {
    [JsonPropertyName("id")]
    public string? Id { get; set; } // 资源索引的标识符

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; } // 资源索引的SHA1校验码

    [JsonPropertyName("size")]
    public int? Size { get; set; } // 资源索引文件的大小

    [JsonPropertyName("totalSize")]
    public int? TotalSize { get; set; } // 所有资源文件的总大小

    [JsonPropertyName("url")]
    public string? Url { get; set; } // 下载资源索引文件的URL
}

// 资源索引反序列化工具类
public static class AssetIndexDeserializer {
    /// <summary>
    /// 从JSON字符串反序列化AssetIndex对象
    /// </summary>
    /// <param name="json">包含assetIndex字段的JSON字符串</param>
    /// <returns>反序列化后的AssetIndex对象，如果失败则返回null</returns>
    public static AssetIndex? DeserializeAssetIndex(JsonNode? json) {
        try {
            return json.Deserialize<AssetIndex>(Files.PrettierJsonOptions);
        } catch (JsonException ex) {
            LogWrapper.Warn($"资源索引反序列化错误: {ex.Message}");
            return null;
        }
    }
}

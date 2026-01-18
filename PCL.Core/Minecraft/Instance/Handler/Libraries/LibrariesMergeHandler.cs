using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.IO;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Instance.Resources;

namespace PCL.Core.Minecraft.Instance.Handler.Libraries;

public static class LibrariesMergeHandler {
    /// <summary>
    /// 从 Merge 类型 JSON 中提取并反序列化libraries字段
    /// </summary>
    public static List<Library>? ParseLibrariesFromJson(IMcInstance instance, JsonObject versionJson) {
        try {
            // 获取libraries字段
            var librariesNode = versionJson["libraries"];

            // 反序列化libraries字段
            return librariesNode.Deserialize<List<Library>>(Files.PrettierJsonOptions);
        } catch (JsonException) {
            throw new JsonException("JSON 解析或反序列化错误");
        }
    }
}

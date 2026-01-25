using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Handler;

public static class InstanceJsonHandler {
    public static async Task<JsonObject?> RefreshVersionJsonAsync(IMcInstance instance) {
        // 优先尝试读取同名 JSON 文件
        var jsonPath = Path.Combine(instance.Path, $"{instance.Name}.json");
        
        // 如果同名 JSON 文件不存在，则尝试读取唯一的 JSON 文件
        if (!File.Exists(jsonPath)) {
            var jsonFiles = Directory.GetFiles(instance.Path, "*.json");
            if (jsonFiles.Length == 1) {
                jsonPath = jsonFiles[0];
            } else {
                return null;
            }
        }

        try {
            // 异步读取文件内容
            await using var fileStream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var jsonNode = await JsonNode.ParseAsync(fileStream);
            var jsonObject = jsonNode!.AsObject();

            return jsonObject;
        } catch (Exception ex) {
            LogWrapper.Warn(ex, $"初始化实例 JSON 失败（{instance.Name}）");
        }

        return null;
    }
    
    /// <summary>
    /// 实例 JAR 中的 version.json 文件对象
    /// </summary>
    public static async Task<JsonObject?> RefreshVersionJsonInJarAsync(IMcInstance instance) {
        // JAR 存在性检查
        var jarPath = Path.Combine(instance.Path, $"{instance.Name}.jar");
        if (!File.Exists(jarPath)) {
            return null;
        }

        try {
            // 异步读取 JAR 文件内容
            await using var fileStream = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var zipFile = new ZipFile(fileStream);

            // 查找 version.json 条目
            var versionJsonEntry = zipFile.GetEntry("version.json");
            if (versionJsonEntry != null) {
                await using var entryStream = zipFile.GetInputStream(versionJsonEntry);
                
                // 异步解析 JSON 内容
                var jsonNode = await JsonNode.ParseAsync(entryStream);
                if (jsonNode is JsonObject jsonObj) {
                    return jsonObj;
                }
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "从实例 JAR 中读取 version.json 失败");
        }
        return null;
    }
}

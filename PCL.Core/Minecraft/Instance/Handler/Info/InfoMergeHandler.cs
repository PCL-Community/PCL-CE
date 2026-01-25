using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.Instance.Handler.Info;

public static class InfoMergeHandler {
    /// <summary>
    /// 依赖库哈希缓存，便于在 Merge 类型 JSON 中识别补丁类型和版本
    /// </summary>
    private static HashSet<string>? _libraryNameHashCache;

    private static readonly FrozenDictionary<string, string> PatcherIdNameMapping = new Dictionary<string, string> {
            { "org.quiltmc:quilt-loader", "quilt" },
            { "com.cleanroommc:cleanroom", "cleanroom" },
            { "com.mumfrey:liteloader", "liteloader" },
        }
        .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 将 Merge 类型 JSON 转化为对应的 InstanceInfo
    /// </summary>
    public static IMcInstance RefreshMergeInstanceInfo(IMcInstance instance, in JsonObject versionJson) {
        var clonedInstance = InstanceFactory.CloneInstance(instance);

        var instanceInfo = new PatchInstanceInfo();

        // 获取 MC 版本
        var version = RecognizeMcVersion(versionJson);

        // 获取发布时间
        var releaseTime = RecognizeReleaseTime(versionJson);
        
        // 添加 MC 本体补丁信息
        instanceInfo.Patches.Add(new PatchInfo {
            Id = "game",
            Version = version,
            ReleaseTime = releaseTime
        });
        
        // 添加其它补丁信息
        instanceInfo.Patches.AddRange(GetPatchInfos(instanceInfo, versionJson));

        clonedInstance.InstanceInfo = instanceInfo;

        return clonedInstance;
    }

    private static List<PatchInfo> GetPatchInfos(PatchInstanceInfo patchInstanceInfo, in JsonObject versionJson) {
        var patchInfos = new List<PatchInfo>();
        
        // 从 JSON 提取 libraries 的 name 属性为 HashSet
        _libraryNameHashCache = versionJson["libraries"]!.AsArray().Where(lib => lib!["name"] != null)
            .Select(lib => lib!["name"]!.GetValue<string>())
            .ToHashSet();

        // Quilt & Cleanroom & LiteLoader
        foreach (var pair in PatcherIdNameMapping) {
            var version = FindPatcherVersionsInHashSet(pair.Key);
            if (version != null) {
                patchInfos.Add(new PatchInfo {
                    Id = pair.Value,
                    Version = version
                });
            }
        }

        // NeoForge
        var hasNeoForge = true;
        if (FindPatcherVersionsInHashSet("net.neoforged.fancymodloader") != null) {
            PatchInfo? neoForgeInfo;
            if (patchInstanceInfo.McVersionStr.IsNullOrEmpty()) {
                neoForgeInfo = FindArgumentData(versionJson, "--fml.neoForgeVersion", "neoforge");
                if (neoForgeInfo == null) {
                    FindArgumentData(versionJson, "--fml.forgeVersion", "neoforge");
                }
            }
            neoForgeInfo = FindArgumentData(versionJson, patchInstanceInfo.McVersionStr == "1.20.1" ? "--fml.forgeVersion" : "--fml.neoForgeVersion", "neoforge");
            if (neoForgeInfo == null) {
                hasNeoForge = false;
                LogWrapper.Debug("未识别到 NeoForge");
            } else {
                patchInfos.Add(neoForgeInfo);
            }
        } else {
            hasNeoForge = false;
        }

        // Fabric & LegacyFabric
        var hasFabric = false;
        if (!hasNeoForge) {
            var fabricVersion = FindPatcherVersionsInHashSet("net.fabricmc:fabric-loader");
            if (fabricVersion != null) {
                if (FindPatcherVersionsInHashSet("net.legacyfabric") != null) {
                    patchInfos.Add(new PatchInfo {
                        Id = "legacyfabric",
                        Version = fabricVersion
                    });
                } else {
                    patchInfos.Add(new PatchInfo {
                        Id = "fabric",
                        Version = fabricVersion
                    });
                }
                hasFabric = true;
            }
        }

        // Forge
        if (!hasNeoForge && !hasFabric) {
            try {
                FindArgumentData(versionJson, "--fml.forgeVersion", "forge");
            } catch (Exception ex) {
                LogWrapper.Warn(ex, "识别 Forge 时出错");
            }
        }

        // OptiFine
        var optiFineVersion = FindPatcherVersionsInHashSet("optifine:OptiFine");
        if (optiFineVersion != null) {
            var parts = optiFineVersion.Split('_', 2);
            if (parts.Length > 1) {
                if (Version.TryParse(parts[0], out _)) {
                    patchInfos.Add(new PatchInfo {
                        Id = "optifine",
                        Version = parts[1]
                    });
                }
            }
        }

        // LabyMod
        try {
            // 使用 FirstOrDefault() 查找符合条件的节点
            var labyModNode = versionJson["arguments"]!["game"]!.AsArray()
                .FirstOrDefault(node =>
                    node!.GetValueKind() == JsonValueKind.String &&
                    node.ToString().Contains("labymod", StringComparison.OrdinalIgnoreCase));
            if (labyModNode != null) {
                patchInfos.Add(new PatchInfo {
                    Id = "labymod"
                });
            }
        } catch {
            LogWrapper.Info("未识别到 LabyMod");
        }
        
        // 原版
        var mcVersion = RecognizeMcVersion(versionJson);
        var releaseTime = RecognizeReleaseTime(versionJson);
        
        var patchInfo = new PatchInfo { Id = "game" };
        if (mcVersion != null) patchInfo.Version = mcVersion;
        if (releaseTime != null) patchInfo.ReleaseTime = releaseTime;

        patchInfos.Add(patchInfo);

        return patchInfos;
    }

    /// <summary>
    /// 通过查找指定的参数并获取其关联的版本，在 JSON 对象中查找补丁信息。
    /// </summary>
    /// <param name="versionJson">包含游戏参数的 JSON 对象。</param>
    /// <param name="argument">要在游戏参数数组中搜索的参数。</param>
    /// <param name="id">与补丁信息关联的标识符。</param>
    /// <returns>包含补丁 ID 和版本的 <see cref="PatchInfo"/> 对象，如果未找到参数或 JSON 结构无效，则返回 null。</returns>
    private static PatchInfo? FindArgumentData(in JsonObject versionJson, string argument, string id) {
        if (versionJson.TryGetPropertyValue("arguments", out var argumentsNode) &&
            argumentsNode is JsonObject arguments &&
            arguments.TryGetPropertyValue("game", out var gameNode) &&
            gameNode is JsonArray gameArguments) {
            // 在 gameArguments 数组中搜索指定的参数
            for (var i = 0; i < gameArguments.Count - 1; i++) {
                var current = gameArguments[i];
                if (current != null && current.ToString() == argument) {
                    var versionNode = gameArguments[i + 1];
                    if (versionNode != null) {
                        return new PatchInfo {
                            Id = id,
                            Version = versionNode.ToString()
                        };
                    }
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// 从 HashSet 中查找以指定前缀开头的 name 并提取版本号，没有找到则返回 null
    /// </summary>
    private static string? FindPatcherVersionsInHashSet(string prefix) {
        return _libraryNameHashCache!.Where(name => name.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase))
            .Select(name => name[(prefix.Length + 1)..])
            .FirstOrDefault();
    }

    private static string? RecognizeMcVersion(JsonObject versionJson) {
        if (versionJson.TryGetPropertyValue("patches", out var patchesElement) &&
            patchesElement?.GetValueKind() == JsonValueKind.Array) {
            var patchesArray = (JsonArray)patchesElement;
            var gamePatch = patchesArray
                .OfType<JsonObject>()
                .FirstOrDefault(patch =>
                    patch.TryGetPropertyValue("id", out var idElement) &&
                    idElement?.ToString() == "game" &&
                    patch.TryGetPropertyValue("version", out _));

            if (gamePatch?.TryGetPropertyValue("version", out var versionElement) == true) {
                var version = versionElement?.ToString();
                if (!string.IsNullOrEmpty(version)) {
                    return version;
                }
            }
        }

        return versionJson.TryGetPropertyValue("clientVersion", out var clientVersionElement) ? clientVersionElement!.ToString() : null;
    }

    /// <summary>
    /// 异步获取版本的发布日期时间，如果无法获取或解析失败，则返回默认时间（1970-01-01 15:00:00）。
    /// </summary>
    /// <returns>版本的发布日期时间，或默认时间。</returns>
    private static DateTime? RecognizeReleaseTime(JsonObject jsonObject) {
        if (!jsonObject.TryGetPropertyValue("releaseTime", out var releaseTimeNode) ||
            releaseTimeNode == null ||
            !DateTime.TryParse(releaseTimeNode.GetValue<string>(), out var releaseTime)) {
            return null;
        }

        return releaseTime;
    }

    private static McVersionType RecognizeVersionType(JsonObject versionJson, DateTime releaseTime) {
        if (releaseTime is { Month: 4, Day: 1 }) {
            return McVersionType.Fool;
        }

        if (releaseTime.Year > 2000 && releaseTime <= new DateTime(2011, 11, 16)) {
            return McVersionType.Old;
        }

        if (versionJson.TryGetPropertyValue("type", out var typeElement)) {
            var typeString = typeElement!.GetValue<string>();
            if (typeString == "release") {
                return McVersionType.Release;
            }
        }
        return McVersionType.Snapshot;
    }
}

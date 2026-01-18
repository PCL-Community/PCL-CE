using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Impl;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Instance.Service;

public static class InstanceJavaService {
    public static (Version MinVer, Version MaxVer) GetCompatibleJavaVersionRange(IMcInstance instance, in JsonObject versionJson, in JsonObject? versionJsonInJar) {
        var minVer = new Version(0, 0, 0, 0);
        var maxVer = new Version(999, 999, 999, 999);

        CheckJavaVersion(instance, out var minVerVanilla, out var maxVerVanilla);
        minVer = minVerVanilla ?? minVer;
        maxVer = maxVerVanilla ?? maxVer;

        if (!instance.InstanceInfo.IsNormalVersion) {
            return (minVer, maxVer);
        }

        // Minecraft jar recommendations
        if (versionJsonInJar != null) {
            if (versionJsonInJar.TryGetPropertyValue("java_version", out var javaVersionNodeInJar) &&
                javaVersionNodeInJar?.GetValueKind() == JsonValueKind.Number) {
                var recommendedJava = javaVersionNodeInJar.GetValue<int>();
                McLaunchUtils.Log($"Mojang (in JAR) recommends Java {recommendedJava}");
                if (recommendedJava >= 22) {
                    minVer = UpdateMin(minVer, new Version(recommendedJava, 0, 0, 0));
                }
            }
        }

        // OptiFine adjustments
        if (instance.InstanceInfo.HasPatch("optifine")) {
            if (instance.InstanceInfo.McVersion < new Version(1, 7) || instance.InstanceInfo.McVersionMinor == 12) {
                maxVer = UpdateMaxAndLog(maxVer, new Version(8, 999, 999, 999),
                    "OptiFine <1.7 / 1.12 requires max Java 8");
            } else if (instance.InstanceInfo.McVersion >= new Version(1, 8) && instance.InstanceInfo.McVersion < new Version(1, 12)) {
                LogWrapper.Debug("Launch", "OptiFine 1.8 - 1.11 requires exactly Java 8");
                minVer = UpdateMin(minVer, new Version(1, 8, 0, 0));
                maxVer = UpdateMax(maxVer, new Version(8, 999, 999, 999));
            }
        }

        // LiteLoader adjustments
        if (instance.InstanceInfo.HasPatch("liteloader")) {
            maxVer = UpdateMaxAndLog(maxVer, new Version(8, 999, 999, 999),
                "LiteLoader requires max Java 8");
        }

        // Forge adjustments
        if (instance.InstanceInfo.HasPatch("forge")) {
            var mcMinor = instance.InstanceInfo.McVersionMinor;
            var mcVersion = instance.InstanceInfo.McVersion;

            if (mcVersion >= new Version(1, 6, 1) && mcVersion <= new Version(1, 7, 2)) {
                LogWrapper.Debug("Launch", "1.6.1 - 1.7.2 Forge requires exactly Java 7");
                minVer = UpdateMin(minVer, new Version(1, 7, 0, 0));
                maxVer = UpdateMax(maxVer, new Version(1, 7, 999, 999));
            } else {
                var (logMessage, newMin, newMax) = mcMinor switch {
                    <= 12 => ("<=1.12 Forge requires Java 8", null, new Version(8, 999, 999, 999)),
                    <= 14 => ("1.13 - 1.14 Forge requires Java 8 - 10", new Version(1, 8, 0, 0), new Version(10, 999, 999, 999)),
                    15 => ("1.15 Forge requires Java 8 - 15", new Version(1, 8, 0, 0), new Version(15, 999, 999, 999)),
                    16 when Version.TryParse(instance.InstanceInfo.GetPatch("forge")?.Version, out var forgeVersion)
                            && forgeVersion > new Version(34, 0, 0)
                            && forgeVersion < new Version(36, 2, 25) =>
                        ("1.16 Forge 34.X - 36.2.25 requires max Java 8u321", null, new Version(1, 8, 0, 321)),
                    18 when instance.InstanceInfo.HasPatch("optifine") =>
                        ("1.18 Forge + OptiFine requires max Java 18", null, new Version(18, 999, 999, 999)),
                    _ => (null, null, null) // 默认情况，不匹配任何规则
                };

                if (logMessage != null) {
                    LogWrapper.Debug("Launch", logMessage);
                    if (newMin != null) {
                        minVer = UpdateMin(minVer, newMin);
                    }
                    if (newMax != null) {
                        maxVer = UpdateMax(maxVer, newMax);
                    }
                }
            }
        }

        // Cleanroom adjustments
        if (instance.InstanceInfo.HasPatch("cleanroom")) {
            minVer = UpdateMinAndLog(minVer, new Version(21, 0, 0, 0),
                "Cleanroom requires min Java 21");
        }

        // Fabric adjustments
        if (instance.InstanceInfo.HasPatch("fabric")) {
            var mcMinor = instance.InstanceInfo.McVersionMinor;
            // 根据 mcMinor 版本号，使用 switch 表达式确定最低 Java 版本
            minVer = mcMinor switch {
                >= 15 and <= 16 => UpdateMinAndLog(minVer, new Version(1, 8, 0, 0),
                    "1.15 - 1.16 Fabric requires min Java 8"),
                >= 18 => UpdateMinAndLog(minVer, new Version(17, 0, 0, 0),
                    "1.18+ Fabric requires min Java 17"),
                _ => minVer // 默认情况，不更新
            };
        }

        // LabyMod adjustments
        if (instance.InstanceInfo.HasPatch("labymod")) {
            minVer = UpdateMinAndLog(minVer, new Version(21, 0, 0, 0),
                "LabyMod requires min Java 21");
            maxVer = new Version(999, 999, 999, 999); // Reset max if needed, but already high
        }

        // JSON recommended version
        if (!versionJson.TryGetPropertyValue("javaVersion", out var javaVersionNode) ||
            javaVersionNode?.GetValueKind() != JsonValueKind.Object ||
            !javaVersionNode.AsObject().TryGetPropertyValue("majorVersion", out var majorVersionElement) ||
            majorVersionElement?.GetValueKind() != JsonValueKind.Number) {

            return (minVer, maxVer);
        }

        // All checks passed, proceed with the main logic
        var jsonRecommendedJava = majorVersionElement.GetValue<int>();
        McLaunchUtils.Log($"Mojang recommends Java {jsonRecommendedJava}");
        if (jsonRecommendedJava >= 22) {
            minVer = UpdateMin(minVer, new Version(jsonRecommendedJava, 0, 0, 0));
        }

        return (minVer, maxVer);
    }

    // Helper to update minVer to the higher value
    private static Version UpdateMin(Version current, Version candidate) => candidate > current ? candidate : current;

    // Helper to update maxVer to the lower value
    private static Version UpdateMax(Version current, Version candidate) => candidate < current ? candidate : current;

    // 辅助方法：负责更新版本并打印日志
    private static Version UpdateMinAndLog(Version currentMin, Version newMin, string logMessage) {
        LogWrapper.Debug("Launch", logMessage);
        return UpdateMin(currentMin, newMin);
    }

    private static Version UpdateMaxAndLog(Version currentMax, Version newMax, string logMessage) {
        LogWrapper.Debug("Launch", logMessage);
        return UpdateMax(currentMax, newMax);
    }

    // 定义 Java 版本要求规则
    private static readonly List<(Func<PatchInstanceInfo, bool> Condition, Version MinVer, Version? MaxVer, string LogMessage)> VanillaJavaVersionRules = [
        // 1.20.5+ (24w14a+)：至少 Java 21
        (
            info => !info.IsNormalVersion && info.ReleaseTime >= new DateTime(2024, 4, 2) ||
                    info.IsNormalVersion && info.McVersion >= new Version(1, 20, 5),
            new Version(21, 0, 0, 0),
            null,
            "MC 1.20.5+ (24w14a+) 要求至少 Java 21"
        ),
        // 1.18 pre2+：至少 Java 17
        (
            info => !info.IsNormalVersion && info.ReleaseTime >= new DateTime(2021, 11, 16) ||
                    info.IsNormalVersion && info.McVersion >= new Version(1, 18),
            new Version(17, 0, 0, 0),
            null,
            "MC 1.18 pre2+ 要求至少 Java 17"
        ),
        // 1.17+ (21w19a+)：至少 Java 16
        (
            info => !info.IsNormalVersion && info.ReleaseTime >= new DateTime(2021, 5, 11) ||
                    info.IsNormalVersion && info.McVersion >= new Version(1, 17),
            new Version(16, 0, 0, 0),
            null,
            "MC 1.17+ (21w19a+) 要求至少 Java 16"
        ),
        // 1.12+：至少 Java 8
        (
            info => info.ReleaseTime.Year >= 2017,
            new Version(1, 8, 0, 0),
            null,
            "MC 1.12+ 要求至少 Java 8"
        ),
        // 1.5.2-：最高 Java 12
        (
            info => info.ReleaseTime <= new DateTime(2013, 5, 1) && info.ReleaseTime.Year >= 2001,
            new Version(1, 8, 0, 0), // 假设最低 Java 8（可调整）
            new Version(12, 999, 999, 999),
            "MC 1.5.2- 要求最高 Java 12"
        )
    ];

    /// <summary>
    /// 检查 Minecraft 版本所需的 Java 版本
    /// </summary>
    /// <param name="instance">MC 实例</param>
    /// <param name="minVer">输出：所需最低 Java 版本</param>
    /// <param name="maxVer">输出：所需最高 Java 版本（可能为 null）</param>
    /// <returns>返回 true 表示找到匹配规则，false 表示未匹配</returns>
    private static void CheckJavaVersion(IMcInstance instance, out Version? minVer, out Version? maxVer) {
        // 使用 FirstOrDefault 查找第一个匹配的规则
        var matchedRule = VanillaJavaVersionRules.FirstOrDefault(rule => rule.Condition(instance.InstanceInfo));

        // 检查元组中的 Condition 委托是否为 null，来判断是否找到了匹配项
        if (matchedRule.Condition != null) {
            LogWrapper.Debug("Launch", matchedRule.LogMessage);

            minVer = matchedRule.MinVer;
            maxVer = matchedRule.MaxVer;
        }

        // 默认值：未匹配任何规则
        LogWrapper.Debug("Launch", "未匹配任何 Java 版本规则，使用默认值");
        minVer = new Version(1, 8, 0, 0); // 默认最低 Java 8
        maxVer = null;
    }
}

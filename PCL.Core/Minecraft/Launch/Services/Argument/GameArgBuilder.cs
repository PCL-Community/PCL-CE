using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Minecraft.Launch.Utils;

namespace PCL.Core.Minecraft.Launch.Services.Argument;

/// <summary>
/// 构建 Minecraft 游戏启动参数
/// </summary>
public class GameArgBuilder(IMcInstance instance) {
    private readonly IJsonBasedInstance _jsonBasedInstance = (IJsonBasedInstance)instance;

    // ReSharper disable InconsistentNaming
    private const string OPTIFINE_TWEAKER = "optifine.OptiFineTweaker";
    private const string OPTIFINE_FORGE_TWEAKER = "optifine.OptiFineForgeTweaker";
    private const string RETRO_TWEAKER = "--tweakClass com.zero.retrowrapper.RetroTweaker";
    private const string DEFAULT_RESOLUTION_ARGS = " --height ${resolution_height} --width ${resolution_width}";
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// 构建旧版本格式的游戏参数
    /// </summary>
    public List<string> BuildLegacyGameArguments() {
        var arguments = new List<string>();

        AddRetroWrapperArgs(arguments);
        AddLegacyMinecraftArgs(arguments);
        
        return ProcessOptiFineTweaker(arguments);
    }

    /// <summary>
    /// 构建新版本格式的游戏参数
    /// </summary>
    public List<string> BuildModernGameArguments() {
        var arguments = ExtractJsonGameArguments();
        var processedArguments = MergeConsecutiveArguments(arguments);
        var deduplicatedArguments = processedArguments.Distinct().ToList();
        
        return ProcessOptiFineTweaker(deduplicatedArguments);
    }

    #region 私有方法 - 参数构建

    /// <summary>
    /// 添加 RetroWrapper 相关参数
    /// </summary>
    private void AddRetroWrapperArgs(List<string> arguments) {
        if (LaunchEnvUtils.NeedRetroWrapper(instance)) {
            arguments.Add(RETRO_TWEAKER);
        }
    }

    /// <summary>
    /// 添加旧版本 Minecraft 参数
    /// </summary>
    private void AddLegacyMinecraftArgs(List<string> arguments) {
        var minecraftArgs = GetMinecraftArgumentsFromJson();

        if (!minecraftArgs.Contains("--height")) {
            minecraftArgs += DEFAULT_RESOLUTION_ARGS;
        }

        arguments.Add(minecraftArgs);
    }

    /// <summary>
    /// 从 JSON 中获取 Minecraft 参数
    /// </summary>
    private string GetMinecraftArgumentsFromJson() {
        return _jsonBasedInstance.VersionJson?["minecraftArguments"]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 从 JSON 中提取游戏参数
    /// </summary>
    private List<string> ExtractJsonGameArguments() {
        var arguments = new List<string>();

        if (_jsonBasedInstance.VersionJson?["arguments"]?["game"] is not JsonArray gameArgs) {
            return arguments;
        }

        foreach (var argNode in gameArgs) {
            ProcessArgumentNode(argNode, arguments);
        }

        return arguments;
    }

    /// <summary>
    /// 处理单个参数节点
    /// </summary>
    private static void ProcessArgumentNode(JsonNode? argNode, List<string> arguments) {
        if (argNode == null) return;

        if (argNode.GetValueKind() == JsonValueKind.String) {
            // 简单字符串参数
            arguments.Add(argNode.ToString());
        } else if (IsConditionalArgumentValid(argNode)) {
            // 有条件的参数且满足规则
            AddConditionalArgumentValue(argNode, arguments);
        }
    }

    /// <summary>
    /// 检查条件参数是否有效
    /// </summary>
    private static bool IsConditionalArgumentValid(JsonNode argNode) {
        var rules = argNode["rules"];
        return rules != null && McLaunchUtils.CheckRules(rules.AsObject());
    }

    /// <summary>
    /// 添加条件参数的值
    /// </summary>
    private static void AddConditionalArgumentValue(JsonNode argNode, List<string> arguments) {
        var valueNode = argNode["value"];
        if (valueNode == null) return;

        if (valueNode.GetValueKind() == JsonValueKind.String) {
            arguments.Add(valueNode.ToString());
        } else if (valueNode is JsonArray values) {
            arguments.AddRange(values.Where(v => v != null).Select(v => v!.ToString()));
        }
    }

    /// <summary>
    /// 合并以"-"开头的连续参数
    /// </summary>
    private static List<string> MergeConsecutiveArguments(List<string> arguments) {
        var mergedArguments = new List<string>();

        for (var i = 0; i < arguments.Count; i++) {
            var currentArg = arguments[i];

            if (currentArg.StartsWith('-')) {
                // 合并后续不以"-"开头的参数
                while (i < arguments.Count - 1 && !arguments[i + 1].StartsWith('-')) {
                    i++;
                    currentArg += " " + arguments[i];
                }
            }

            mergedArguments.Add(currentArg);
        }

        return mergedArguments;
    }

    #endregion

    #region 私有方法 - OptiFine处理

    /// <summary>
    /// 处理 OptiFine Tweaker 参数
    /// </summary>
    private List<string> ProcessOptiFineTweaker(List<string> arguments) {
        if (!ShouldProcessOptiFine()) {
            return arguments;
        }

        return FixOptiFineTweakerOrder(arguments);
    }

    /// <summary>
    /// 检查是否需要处理 OptiFine
    /// </summary>
    private bool ShouldProcessOptiFine() {
        var instanceInfo = instance.InstanceInfo;
        var hasForgeOrLiteLoader = instanceInfo.HasPatch("forge") || instanceInfo.HasPatch("liteloader");
        var hasOptiFine = instanceInfo.HasPatch("optifine");

        return hasForgeOrLiteLoader && hasOptiFine;
    }

    /// <summary>
    /// 修正 OptiFine Tweaker 的顺序
    /// </summary>
    private List<string> FixOptiFineTweakerOrder(List<string> arguments) {
        var argumentsString = string.Join(" ", arguments);

        if (argumentsString.Contains($"--tweakClass {OPTIFINE_FORGE_TWEAKER}")) {
            return EnsureOptiFineForgeTweakerIsLast(arguments, OPTIFINE_FORGE_TWEAKER);
        }

        if (argumentsString.Contains($"--tweakClass {OPTIFINE_TWEAKER}")) {
            return ReplaceWithOptiFineForgeTweaker(arguments);
        }

        return arguments;
    }

    /// <summary>
    /// 确保 OptiFineForgeTweaker 在最后
    /// </summary>
    private List<string> EnsureOptiFineForgeTweakerIsLast(List<string> arguments, string tweakerClass) {
        var argumentsString = string.Join(" ", arguments);
        LogWrapper.Info($"Found correct OptiFineForge TweakClass, current args: {argumentsString}");

        var result = new List<string>();
        var tweakClassArgs = new[] { "--tweakClass", tweakerClass };

        // 移除现有的 tweakClass 参数
        for (var i = 0; i < arguments.Count; i++) {
            if (arguments[i] == "--tweakClass" && i + 1 < arguments.Count && arguments[i + 1] == tweakerClass) {
                i++; // 跳过下一个参数
                continue;
            }
            result.Add(arguments[i]);
        }

        // 在最后添加 tweakClass 参数
        result.AddRange(tweakClassArgs);
        return result;
    }

    /// <summary>
    /// 替换为 OptiFineForgeTweaker
    /// </summary>
    private List<string> ReplaceWithOptiFineForgeTweaker(List<string> arguments) {
        var argumentsString = string.Join(" ", arguments);
        LogWrapper.Info($"Found incorrect OptiFineForge TweakClass, current args: {argumentsString}");

        var result = new List<string>();
        var newTweakClassArgs = new[] { "--tweakClass", OPTIFINE_FORGE_TWEAKER };

        // 移除现有的错误 tweakClass 参数
        for (var i = 0; i < arguments.Count; i++) {
            if (arguments[i] == "--tweakClass" && i + 1 < arguments.Count && arguments[i + 1] == OPTIFINE_TWEAKER) {
                i++; // 跳过下一个参数
                continue;
            }
            result.Add(arguments[i]);
        }

        // 在最后添加正确的 tweakClass 参数
        result.AddRange(newTweakClassArgs);

        UpdateInstanceJsonFile();
        return result;
    }

    /// <summary>
    /// 更新实例 JSON 文件
    /// </summary>
    private void UpdateInstanceJsonFile() {
        try {
            var filePath = $"{instance.Path}{instance.Name}.json";
            var content = File.ReadAllText(filePath);
            var updatedContent = content.Replace(OPTIFINE_TWEAKER, OPTIFINE_FORGE_TWEAKER);
            File.WriteAllText(filePath, updatedContent);
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "Failed to replace OptiFineForge TweakClass in instance JSON file");
        }
    }

    #endregion
}

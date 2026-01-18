using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL.Core.Minecraft.Launch.Utils;

public static class McLaunchUtils {
    // ReSharper disable InconsistentNaming
    private const string DEFAULT_ASSET_INDEX = "legacy";
    // ReSharper restore InconsistentNaming
    
    
    public static void Log(string msg) {
        // TODO: UI Log
        LogWrapper.Info("McLaunch", msg);
    }

    // 检查是否满足 rules 条件
    public static bool CheckRules(JsonObject? rulesObj) {
        if (rulesObj == null) return false;

        var rules = rulesObj.Deserialize<List<Rule>>();
        if (rules == null || rules.Count == 0)
            return true; // 没有规则，默认允许

        var required = false;

        foreach (var rule in rules) {
            var ruleMatches = true; // 当前规则是否匹配

            // 检查操作系统条件
            if (rule.Os?.Name != null) {
                var osName = rule.Os.Name.ToLowerInvariant();
                var currentOs = EnvironmentInterop.GetCurrentOsName();

                // 仅当操作系统名称匹配时继续检查
                if (osName == "unknown" || osName != currentOs) {
                    ruleMatches = false;
                } else if (osName == currentOs && rule.Os.Version != null) {
                    // 检查操作系统版本
                    try {
                        var versionPattern = rule.Os.Version;
                        var osVersion = Environment.OSVersion.Version.ToString();
                        ruleMatches = ruleMatches && Regex.IsMatch(osVersion, versionPattern);
                    } catch (RegexParseException) {
                        // 无效的正则表达式，规则不匹配
                        ruleMatches = false;
                    }
                }

                // 检查系统架构（x86 或 x64）
                if (rule.Os.Arch != null) {
                    var is32BitSystem = !Environment.Is64BitOperatingSystem;
                    ruleMatches = ruleMatches && string.Equals(rule.Os.Arch, "x86", StringComparison.OrdinalIgnoreCase) == is32BitSystem;
                }
            }

            // 根据 action 更新结果
            switch (rule.Action) {
                case "allow":
                    if (ruleMatches) {
                        required = true; // 规则匹配，允许使用
                    }
                    break;
                case "disallow":
                    if (ruleMatches) {
                        required = false; // 规则匹配，禁止使用
                    }
                    break;
            }
        }

        return required;
    }
    
    /// <summary>
    /// 获取资源文件索引名称
    /// </summary>
    public static string GetAssetsIndexName(IJsonBasedInstance jsonBasedInstance) {
        try {
            // 优先使用 assetIndex.id
            if (jsonBasedInstance.VersionJson!.TryGetPropertyValue("assetIndex", out var assetIndexElement) &&
                assetIndexElement!.GetValueKind() == JsonValueKind.Object &&
                assetIndexElement.AsObject().TryGetPropertyValue("id", out var idElement) &&
                idElement!.GetValueKind() == JsonValueKind.String) {
                return idElement.ToString();
            }

            // 其次使用 assets
            if (jsonBasedInstance.VersionJson.TryGetPropertyValue("assets", out var assetsElement) &&
                assetsElement!.GetValueKind() == JsonValueKind.String) {
                return assetsElement.ToString();
            }
        } catch (Exception ex) {
            LogWrapper.Warn(ex, "获取资源文件索引名失败，使用默认值");
        }

        return DEFAULT_ASSET_INDEX;
    }

    /// <summary>
    /// 打码字符串中的 AccessToken。
    /// </summary>
    /// <param name="raw">原始字符串</param>
    /// <param name="filterChar">用于替换的字符</param>
    /// <returns>打码后的字符串</returns>
    public static string FilterAccessToken(string raw, char filterChar) {
        // 打码 "accessToken " 后的内容
        if (raw.Contains("accessToken ")) {
            foreach (Match match in RegexPatterns.AccessToken.Matches(raw)) {
                var token = match.Value;
                raw = raw.Replace(token, new string(filterChar, token.Length));
            }
        }

        // TODO: 账户系统
        /*
        // 打码当前登录的结果
        string accessToken = McLoginLoader.Output.AccessToken;
        if (accessToken != null && accessToken.Length >= 10 &&
            raw.IndexOf(accessToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
            McLoginLoader.Output.Uuid != McLoginLoader.Output.AccessToken) // UUID 和 AccessToken 一样则不打码
        {
            raw = raw.Replace(accessToken,
                accessToken.Substring(0, 5) + new string(filterChar, accessToken.Length - 10) + accessToken.Substring(accessToken.Length - 5));
        }
        */

        return raw;
    }
}

// Rule object for conditional actions
public class Rule {
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("os")]
    public Os? Os { get; set; }
}

// Os object for operating system conditions in rules
public class Os {
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("arch")]
    public string? Arch { get; set; }
}

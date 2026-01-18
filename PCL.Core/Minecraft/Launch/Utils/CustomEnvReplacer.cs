using System;
using System.Collections.Generic;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Launch.Utils;

/// <summary>
/// 提供环境变量和参数替换功能，处理 PCL 约定的替换标记
/// </summary>
public class CustomEnvReplacer(IMcInstance instance, JavaInfo selectedJava) {
    private readonly IMcInstance _instance = instance ?? throw new ArgumentNullException(nameof(instance));
    private readonly JavaInfo _selectedJava = selectedJava ?? throw new ArgumentNullException(nameof(selectedJava));

    /// <summary>
    /// 在启动结束时，对 PCL 约定的替换标记进行处理
    /// </summary>
    /// <param name="raw">原始字符串</param>
    /// <param name="replaceTimeAndDate">是否替换日期和时间</param>
    /// <returns>替换后的字符串</returns>
    /// <exception cref="ArgumentNullException">当 raw 参数为 null 时抛出</exception>
    public string ArgumentReplace(string raw, bool replaceTimeAndDate = false) {
        if (string.IsNullOrEmpty(raw))
            return raw;

        var result = raw;

        // 应用所有替换规则
        result = ApplyPathReplacements(result);
        result = ApplyInstanceReplacements(result);
        result = ApplyApplicationReplacements(result);

        if (replaceTimeAndDate) {
            result = ApplyDateTimeReplacements(result);
        }

        // TODO: 账户系统 - 用户信息替换
        result = ApplyUserReplacements(result);

        // TODO: 账户系统 - 登录类型替换  
        result = ApplyLoginTypeReplacements(result);

        return result;
    }

    /// <summary>
    /// 应用路径相关的替换
    /// </summary>
    private string ApplyPathReplacements(string input) {
        var replacements = new Dictionary<string, string> {
            ["{minecraft}"] = _instance.Folder.Path,
            ["{verpath}"] = _instance.Path,
            ["{verindie}"] = _instance.IsolatedPath,
            ["{java}"] = _selectedJava.JavaFolder
        };

        return ApplyReplacements(input, replacements);
    }

    /// <summary>
    /// 应用实例相关的替换
    /// </summary>
    private string ApplyInstanceReplacements(string input) {
        var replacements = new Dictionary<string, string> {
            ["{name}"] = _instance.Name,
            ["{version}"] = _instance.InstanceInfo.FormattedVersion
        };

        return ApplyReplacements(input, replacements);
    }

    /// <summary>
    /// 应用应用程序相关的替换
    /// </summary>
    private static string ApplyApplicationReplacements(string input) {
        var replacements = new Dictionary<string, string> {
            ["{path}"] = Basics.ExecutablePath
        };

        return ApplyReplacements(input, replacements);
    }

    /// <summary>
    /// 应用日期时间替换（仅在需要时调用）
    /// </summary>
    private static string ApplyDateTimeReplacements(string input) {
        var now = DateTime.Now;
        var replacements = new Dictionary<string, string> {
            ["{date}"] = now.ToString("yyyy/M/d"),
            ["{time}"] = now.ToString("HH:mm:ss")
        };

        return ApplyReplacements(input, replacements);
    }

    /// <summary>
    /// TODO: 账户系统 - 应用用户相关的替换
    /// </summary>
    private static string ApplyUserReplacements(string input) {
        // TODO: 账户系统实现后启用以下代码
        /*
        var replacements = new Dictionary<string, string>
        {
            ["{user}"] = McLoginLoader.Output.Name,
            ["{uuid}"] = McLoginLoader.Output.Uuid
        };

        return ApplyReplacements(input, replacements);
        */

        return input;
    }

    /// <summary>
    /// TODO: 账户系统 - 应用登录类型相关的替换
    /// </summary>
    private static string ApplyLoginTypeReplacements(string input) {
        // TODO: 账户系统实现后启用以下代码
        /*
        var loginTypeText = McLoginLoader.Input.Type switch
        {
            McLoginType.Legacy => "离线",
            McLoginType.Ms => "正版",
            McLoginType.Auth => "Authlib-Injector",
            _ => "未知"
        };

        var replacements = new Dictionary<string, string>
        {
            ["{login}"] = loginTypeText
        };

        return ApplyReplacements(input, replacements);
        */

        return input;
    }

    /// <summary>
    /// 统一的字符串替换方法
    /// </summary>
    private static string ApplyReplacements(string input, IReadOnlyDictionary<string, string> replacements) {
        var result = input;

        foreach (var (placeholder, replacement) in replacements) {
            result = result.Replace(placeholder, replacement, StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// 批量替换多个字符串
    /// </summary>
    public IEnumerable<string> ArgumentReplaceMultiple(IEnumerable<string> inputs, bool replaceTimeAndDate = false) {
        foreach (var input in inputs) {
            yield return ArgumentReplace(input, replaceTimeAndDate);
        }
    }

    /// <summary>
    /// 检查字符串是否包含任何替换标记
    /// </summary>
    public static bool ContainsReplacementTokens(string input) {
        if (string.IsNullOrEmpty(input))
            return false;

        var commonTokens = new[] {
            "{minecraft}", "{verpath}", "{verindie}", "{java}",
            "{user}", "{uuid}", "{login}", "{name}", "{version}",
            "{path}", "{date}", "{time}"
        };

        return Array.Exists(commonTokens, token => input.Contains(token, StringComparison.Ordinal));
    }
}

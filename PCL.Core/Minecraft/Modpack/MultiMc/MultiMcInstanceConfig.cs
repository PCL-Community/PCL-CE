using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// <c>instance.cfg</c> 的解析结果。语法与 Prism Launcher 的 <c>INIFile</c> 一致：
/// 只以 <c>=</c> 分隔键值，未转义的 <c>#</c> 开始行内注释，并支持其定义的转义序列。
/// </summary>
public sealed class MultiMcInstanceConfig
{
    private readonly FrozenDictionary<string, string> _values;

    private MultiMcInstanceConfig(FrozenDictionary<string, string> values) => _values = values;

    /// <summary>解析 <c>instance.cfg</c> 的文本内容。</summary>
    public static MultiMcInstanceConfig Parse(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in content.Split('\n'))
        {
            var line = _StripComment(rawLine.TrimEnd('\r'));
            if (line.Length == 0) continue;

            var separator = line.IndexOf('=');
            if (separator < 0) continue;

            var key = line[..separator].Trim();
            if (key.Length == 0) continue;

            values[key] = _Unescape(line[(separator + 1)..].Trim());
        }

        return new MultiMcInstanceConfig(values.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>读取字符串值，缺失或为空白时返回 <c>null</c>。</summary>
    public string? GetString(string key)
        => _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>读取布尔值，缺失或无法解析时返回 <paramref name="fallback"/>。</summary>
    public bool GetBoolean(string key, bool fallback = false)
    {
        var raw = GetString(key);
        if (raw is null) return fallback;

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => fallback
        };
    }

    /// <summary>读取整数值，缺失或无法解析时返回 <c>null</c>。</summary>
    public int? GetInt32(string key)
    {
        var raw = GetString(key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>
    /// 读取受 <c>Override*</c> 开关控制的值。
    /// <para>
    /// MultiMC 的实例设置分两层：只有对应的 <c>Override…</c> 为 <c>true</c> 时，
    /// 实例级取值才生效，否则应沿用启动器全局设置。忽略这一点会把整合包
    /// 遗留的空值或默认值当成用户意图强行写入实例。
    /// </para>
    /// </summary>
    /// <param name="overrideKey">开关键名，例如 <c>OverrideJavaArgs</c>。</param>
    /// <param name="valueKey">取值键名，例如 <c>JvmArgs</c>。</param>
    public string? GetOverridden(string overrideKey, string valueKey)
        => GetBoolean(overrideKey) ? GetString(valueKey) : null;

    private static string _StripComment(string line)
    {
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '#' && (index == 0 || line[index - 1] != '\\'))
                return line[..index].Trim();
        }

        return line.Trim();
    }

    /// <summary>
    /// 还原 Prism INIFile 支持的转义序列。除 <c>\n</c>、<c>\t</c>、<c>\#</c>
    /// 外的反斜杠转义均按其后字符的字面值处理。
    /// </summary>
    private static string _Unescape(string value)
    {
        if (!value.Contains('\\')) return value;

        var builder = new StringBuilder(value.Length);

        var escaped = false;
        foreach (var character in value)
        {
            if (escaped)
            {
                builder.Append(character switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '#' => '#',
                    _ => character
                });
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}

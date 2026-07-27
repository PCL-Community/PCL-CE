using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// <c>instance.cfg</c> 的解析结果。
/// <para>
/// 该文件按 Java <c>Properties</c> 的语法书写（<c>key=value</c>，值中可含转义序列），
/// 因此这里实现一个专用解析器，而不是套用通用 INI 读取 ——
/// 通用 INI 会把值里的 <c>:</c> 当作分隔符，进而截断 <c>JvmArgs</c> 之类的内容。
/// </para>
/// </summary>
public sealed class MultiMcInstanceConfig
{
    private readonly FrozenDictionary<string, string> _values;

    private MultiMcInstanceConfig(FrozenDictionary<string, string> values) => _values = values;

    /// <summary>解析 <c>instance.cfg</c> 的文本内容。</summary>
    public static MultiMcInstanceConfig Parse(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var logicalLine in _EnumerateLogicalLines(content))
        {
            var line = logicalLine.TrimStart();
            if (line.Length == 0 || line[0] is '#' or '!') continue;

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].TrimEnd();
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

    /// <summary>
    /// 将物理行合并为逻辑行 —— 以反斜杠结尾的行与下一行相连。
    /// </summary>
    private static IEnumerable<string> _EnumerateLogicalLines(string content)
    {
        var builder = new StringBuilder();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // 结尾的反斜杠表示续行，但成对的反斜杠是转义后的字面反斜杠
            var trailingBackslashes = 0;
            for (var i = line.Length - 1; i >= 0 && line[i] == '\\'; i--) trailingBackslashes++;

            if (trailingBackslashes % 2 == 1)
            {
                builder.Append(line, 0, line.Length - 1);
                continue;
            }

            if (builder.Length == 0)
            {
                yield return line;
                continue;
            }

            builder.Append(line);
            yield return builder.ToString();
            builder.Clear();
        }

        if (builder.Length > 0) yield return builder.ToString();
    }

    /// <summary>
    /// 还原 Java <c>Properties</c> 的转义序列。
    /// </summary>
    private static string _Unescape(string value)
    {
        if (!value.Contains('\\')) return value;

        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var escaped = value[++i];
            switch (escaped)
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'f': builder.Append('\f'); break;
                case 'u' when i + 4 < value.Length
                              && ushort.TryParse(value.AsSpan(i + 1, 4), NumberStyles.HexNumber,
                                  CultureInfo.InvariantCulture, out var codePoint):
                    builder.Append((char)codePoint);
                    i += 4;
                    break;
                // 其余情况（含 \\ 、\: 、\= 、\# 、\! 与未知转义）一律取字面字符
                default: builder.Append(escaped); break;
            }
        }

        return builder.ToString();
    }
}

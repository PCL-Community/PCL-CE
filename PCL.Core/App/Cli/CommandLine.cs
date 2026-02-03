using System;
using System.Collections.Generic;

namespace PCL.Core.App.Cli;

/// <summary>
/// 命令行模型
/// </summary>
public class CommandLine
{
    /// <summary>
    /// 命令文本
    /// </summary>
    public required string CommandText { get; init; }

    /// <summary>
    /// 子命令
    /// </summary>
    public CommandLine? Subcommand { get; init; } = null;

    /// <summary>
    /// 子命令文本
    /// </summary>
    public string? SubcommandText => Subcommand?.CommandText;

    /// <summary>
    /// 参数字典
    /// </summary>
    public required IReadOnlyDictionary<string, CommandArgument> Arguments { get; init; }

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <param name="key">参数键</param>
    /// <param name="value">参数值，若获取失败则为对应类型默认值</param>
    /// <typeparam name="TValue">参数值的类型</typeparam>
    /// <returns>是否获取成功，若不存在该键或值类型不匹配则失败</returns>
    public bool TryGetArgumentValue<TValue>(string key, out TValue? value)
    {
        var result = Arguments.TryGetValue(key, out var arg);
        if (result && arg!.TryCaseValue(out TValue? typedValue))
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <param name="key">参数键</param>
    /// <typeparam name="TValue">参数值的类型</typeparam>
    /// <returns>参数值</returns>
    /// <exception cref="InvalidCastException">不存在该键或值类型不匹配</exception>
    public TValue? GetArgumentValue<TValue>(string key)
    {
        var result = TryGetArgumentValue(key, out TValue? value);
        return result ? value : throw new InvalidCastException($"Key '{key}' not found or value type mismatch");
    }
}

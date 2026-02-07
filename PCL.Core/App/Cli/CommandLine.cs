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
        if (result && arg!.TryCastValue(out TValue? typedValue))
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

    /// <summary>
    /// 解析参数数组，第一个元素会被视为主命令
    /// </summary>
    /// <param name="args">参数数组</param>
    /// <param name="subcommands">各级子命令列表</param>
    /// <returns>命令行模型实例</returns>
    public static CommandLine Parse(ReadOnlySpan<string> args, IEnumerable<SubcommandDefinition>? subcommands = null)
    {
        subcommands ??= [];
        SubcommandDefinition root = (args[0], subcommands);
        return CommandLineParser.Parse(args, root);
    }
}

file static class CommandLineParser
{
    private static (CommandArgument, bool) _ParseArgument(string key, string possibleValueText)
    {
        if (possibleValueText.Length == 0 || possibleValueText.StartsWith("--"))
            return (new BoolArgument { Key = key, ValueText = string.Empty }, false);
        if (possibleValueText.ToLowerInvariant() is "true" or "false")
            return (new BoolArgument { Key = key, ValueText = possibleValueText }, true);
        if (decimal.TryParse(possibleValueText, out var d))
            return (new DecimalArgument { Key = key, ValueText = possibleValueText, Value = d }, true);
        return (new TextArgument { Key = key, ValueText = possibleValueText }, true);
    }

    public static CommandLine Parse(ReadOnlySpan<string> args, SubcommandDefinition subcommands)
    {
        if (args.IsEmpty) throw new ArgumentException("The argument span must contain at least 1 element", nameof(args));
        var i = 1;
        var commandText = args[0];
        var argumentList = new Dictionary<string, CommandArgument>();
        CommandLine? subcommand = null;
        while (i < args.Length)
        {
            var currentText = args[i];
            if (subcommands.Contains(currentText))
            {
                subcommand = Parse(args[i..], subcommands.SubcommandMap[currentText]);
                break;
            }
            var (commandArgument, hasValueText) = _ParseArgument(currentText, (i == args.Length - 1) ? "" : args[i + 1]);
            argumentList[commandArgument.Key] = commandArgument;
            i += hasValueText ? 2 : 1;
        }
        return new CommandLine
        {
            CommandText = commandText,
            Arguments = argumentList.AsReadOnly(),
            Subcommand = subcommand
        };
    }
}

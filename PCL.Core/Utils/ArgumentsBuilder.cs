using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PCL.Core.Utils;

public class ArgumentsBuilder
{
    private readonly List<Argument> _args = [];

    private enum ArgumentStyle
    {
        Flag,

        /// <summary>
        /// Concated by '=' symbol.
        /// </summary>
        Equals,

        /// <summary>
        /// Concated by space.
        /// </summary>
        Space
    }

    private readonly struct Argument(string key, string? value, ArgumentStyle style)
    {
        public readonly string Key = key ?? throw new ArgumentNullException(nameof(key));
        public readonly string? Value = value;
        public readonly ArgumentStyle Style = style;
    }

    /// <summary>
    /// 添加键值对参数（自动处理空格转义）
    /// </summary>
    /// <param name="key">参数名（不带前缀）</param>
    /// <param name="value">参数值</param>
    public ArgumentsBuilder Add(string key, string value)
    {
        if (key is null) throw new NullReferenceException(nameof(key));
        if (value is null) throw new NullReferenceException(nameof(value));
        _args.Add(new Argument(key, _HandleValue(value), ArgumentStyle.Equals));
        return this;
    }

    /// <summary>
    /// 添加由空格连接到键值对参数（自动处理空格转义）
    /// </summary>
    /// <param name="key">参数名（不带前缀）</param>
    /// <param name="value">参数值</param>
    public ArgumentsBuilder AddWithSpace(string key, string value)
    {
        if (key is null) throw new NullReferenceException(nameof(key));
        if (value is null) throw new NullReferenceException(nameof(value));
        _args.Add(new Argument(key, _HandleValue(value), ArgumentStyle.Space));
        return this;
    }

    /// <summary>
    /// 添加标志参数（无值参数）
    /// </summary>
    /// <param name="flag">标志名（不带前缀）</param>
    public ArgumentsBuilder AddFlag(string flag)
    {
        if (flag is null) throw new NullReferenceException(nameof(flag));
        _args.Add(new Argument(flag, null, ArgumentStyle.Flag));
        return this;
    }

    /// <summary>
    /// 条件添加参数（仅当condition为true时添加）
    /// </summary>
    public ArgumentsBuilder AddIf(bool condition, string key, string value)
    {
        if (condition) Add(key, value);
        return this;
    }

    /// <summary>
    /// 条件添加由空格连接的参数（仅当condition为true时添加）
    /// </summary>
    public ArgumentsBuilder AddWithSpaceIf(bool condition, string key, string value)
    {
        if (condition) AddWithSpace(key, value);
        return this;
    }

    /// <summary>
    /// 条件添加标志（仅当condition为true时添加）
    /// </summary>
    public ArgumentsBuilder AddFlagIf(bool condition, string flag)
    {
        if (condition) AddFlag(flag);
        return this;
    }

    public enum PrefixStyle
    {
        /// <summary>
        /// 自动（单字符用-，多字符用--）
        /// </summary>
        Auto,
        /// <summary>
        /// 强制单横线
        /// </summary>
        SingleLine,
        /// <summary>
        /// 强制双横线
        /// </summary>
        DoubleLine
    }

    /// <summary>
    /// 构建参数字符串
    /// </summary>
    /// <param name="prefixStyle">前缀样式</param>
    public string GetResult(PrefixStyle prefixStyle = 0)
    {
        var sb = new StringBuilder();

        foreach (var arg in _args)
        {
            if (sb.Length > 0) sb.Append(' ');

            // 添加前缀
            switch (prefixStyle)
            {
                case PrefixStyle.SingleLine: // 强制单横线
                    sb.Append('-').Append(arg.Key);
                    break;
                case PrefixStyle.DoubleLine: // 强制双横线
                    sb.Append("--").Append(arg.Key);
                    break;
                default: // 自动判断
                    sb.Append(arg.Key.Length == 1 ? "-" : "--").Append(arg.Key);
                    break;
            }

            // 添加值（如果有）
            if (arg.Value is not null)
            {
                switch (arg.Style)
                {
                    case ArgumentStyle.Equals:
                        sb.Append('=')
                            .Append(arg.Value);
                        break;
                    case ArgumentStyle.Space:
                        sb.Append(' ')
                            .Append(arg.Value);
                        break;
                }
            }
        }

        return sb.ToString();
    }

    public override string ToString()
    {
        return GetResult();
    }

    /// <summary>
    /// 清空所有参数
    /// </summary>
    public void Clear() => _args.Clear();

    private static readonly char[] _CharNeedToQute = [' ', '=', '|', '"'];

    // 转义包含空格的值（用双引号包裹）
    private static string _HandleValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return $"\"{value}\"";
        return value.All(x => !_CharNeedToQute.Contains(x))
            ? value
            : $"\"{value.Replace("\"", "\\\"")}\""; // 处理双引号转义
    }
}
using System.Collections.Generic;
using System.Linq;
using fNbt;
using PCL.Core.Minecraft.Saves.Parsing.Internal;

namespace PCL.Core.Minecraft.Saves.Parsing;

/// <summary>
/// 解析器工厂 —— 按优先级遍历已注册的解析器，返回第一个能处理给定数据的解析器。
/// 默认注册顺序从高版本到低版本，确保最特化的解析器优先匹配。
/// 可通过构造函数注入自定义解析器列表。
/// </summary>
public sealed class SaveParserFactory
{
    private readonly IReadOnlyList<ISaveParser> _parsers;

    /// <summary>使用内置的默认解析器列表初始化（从高版本到低版本）。</summary>
    public SaveParserFactory()
    {
        _parsers =
        [
            new Version261PlusSaveParser(),  // >= 26.1-snapshot-6
            new Version116To1211SaveParser(), // 1.16 ~ 1.21.11
            new Version113To1152SaveParser(), // 1.13 ~ 1.15.2
            new Version19To1122SaveParser(),  // 1.9 ~ 1.12.2
            new Version131To189SaveParser(),  // 1.3.1 ~ 1.8.9
            new Pre113SaveParser(),           // Alpha ~ 1.2.5
        ];
    }

    /// <summary>使用自定义解析器列表初始化（支持 DI 注入）。解析器按传入顺序求值。</summary>
    public SaveParserFactory(IEnumerable<ISaveParser> customParsers)
    {
        _parsers = customParsers?.ToArray() ?? [];
    }

    /// <summary>
    /// 查找第一个能处理给定 NBT 数据的解析器。
    /// </summary>
    /// <param name="data">level.dat 中的 Data 复合标签。</param>
    /// <param name="dataVersion">DataVersion 字段值，如果不存在则为 null。</param>
    /// <returns>匹配的解析器，未找到时返回 null。</returns>
    public ISaveParser? Resolve(NbtCompound data, int? dataVersion)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanHandle(data, dataVersion))
                return parser;
        }
        return null;
    }
}

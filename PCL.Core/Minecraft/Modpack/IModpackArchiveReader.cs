using System.Collections.Generic;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包压缩包条目的最小读取抽象，供格式识别与清单解析使用。
/// 条目名统一使用 <c>/</c> 分隔的完整相对路径（与 zip 条目命名一致）。
/// </summary>
public interface IModpackArchiveReader
{
    /// <summary>
    /// 压缩包内的全部条目名。
    /// </summary>
    IEnumerable<string> EntryNames { get; }

    /// <summary>
    /// 判断压缩包内是否存在指定条目。
    /// </summary>
    /// <param name="entryName">条目名，如 <c>modrinth.index.json</c>。</param>
    bool EntryExists(string entryName);

    /// <summary>
    /// 以文本方式读取指定条目的全部内容。
    /// </summary>
    /// <param name="entryName">条目名。</param>
    string ReadEntryText(string entryName);
}

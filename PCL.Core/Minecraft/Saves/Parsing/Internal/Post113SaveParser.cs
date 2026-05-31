using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// Post113 解析器 —— 对应 1.13 ~ 1.16(20w20a) 之间的存档格式。
/// 特征：DataVersion 在 [1444, 2567) 之间，新增 DataPacks 字段。
/// 其他字段布局与 Modern 一致，直接复用解析即可。
/// </summary>
internal sealed class Post113SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Post113;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value >= 1444 && dataVersion.Value < 2567;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        // 字段布局与 Modern 相同，直接复用
        return new ModernSaveParser().Parse(folderPath, data, createdAt, modifiedAt);
    }
}

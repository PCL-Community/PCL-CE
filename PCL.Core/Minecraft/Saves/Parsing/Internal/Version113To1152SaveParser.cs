using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 1.13 ~ 1.15.2 的存档格式。
/// 特征：DataVersion 在 [1444, 2567) 之间，新增 DataPacks 字段。
/// 其他字段布局与 1.9 ~ 1.12.2 一致，直接复用。
/// </summary>
internal sealed class Version113To1152SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version113To1152;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value >= 1444 && dataVersion.Value < 2567;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
        => new Version19To1122SaveParser().Parse(folderPath, data, createdAt, modifiedAt);
}

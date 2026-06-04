using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 17w47a(1.13) ~ 1.15.2 的存档格式。
/// 特征：DataVersion 在 [1443, 2536) 之间，新增 DataPacks 字段。
/// </summary>
internal sealed class Version113To1152SaveParser : ISaveParser
{
    private readonly ISaveParser _baseParser;

    public Version113To1152SaveParser() : this(new Version19To1122SaveParser()) { }
    public Version113To1152SaveParser(ISaveParser baseParser) => _baseParser = baseParser;

    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version113To1152;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue
        && dataVersion.Value >= DataVersionBoundaries._17w47a
        && dataVersion.Value < DataVersionBoundaries._20w20a;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
        => _baseParser.Parse(folderPath, data, createdAt, modifiedAt);
}

using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 15w32a(1.9) ~ 1.12.2 的存档格式。
/// 特征：DataVersion >= 100 且 &lt; 1443，新增 DataVersion 和 Version 复合标签。
/// </summary>
internal sealed class Version19To1122SaveParser : ISaveParser
{
    private readonly ISaveParser _baseParser;

    public Version19To1122SaveParser() : this(new Version131To189SaveParser()) { }
    public Version19To1122SaveParser(ISaveParser baseParser) => _baseParser = baseParser;

    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version19To1122;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue
        && dataVersion.Value >= DataVersionBoundaries._15w32a
        && dataVersion.Value < DataVersionBoundaries._17w47a;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = _baseParser.Parse(folderPath, data, createdAt, modifiedAt);
        (var versionName, var versionId) = NbtReadHelper.ReadVersion(data);
        return baseInfo with { VersionName = versionName, VersionId = versionId };
    }
}

using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 20w20a(1.16) ~ 1.21.11 的存档格式。
/// 特征：DataVersion 在 [2536, 4774) 之间。
/// 变更：种子从 Data.RandomSeed 迁移到 Data.WorldGenSettings.seed。
/// </summary>
internal sealed class Version116To1211SaveParser : ISaveParser
{
    private readonly ISaveParser _baseParser;

    public Version116To1211SaveParser() : this(new Version19To1122SaveParser()) { }
    public Version116To1211SaveParser(ISaveParser baseParser) => _baseParser = baseParser;

    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version116To1211;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue
        && dataVersion.Value >= DataVersionBoundaries._20w20a
        && dataVersion.Value < DataVersionBoundaries._261snapshot6;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = _baseParser.Parse(folderPath, data, createdAt, modifiedAt);
        return baseInfo with
        {
            Seed = ReadWorldGenSeed(data),
            Spawn = NbtReadHelper.TryReadSpawnFromPos(data)
                 ?? NbtReadHelper.TryReadSpawnFromFields(data),
        };
    }

    internal static long? ReadWorldGenSeed(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("WorldGenSettings", out var wgs) &&
            wgs!.TryGet<NbtLong>("seed", out var seed))
            return seed!.Value;
        return NbtReadHelper.TryGetLong(data, "RandomSeed");
    }
}

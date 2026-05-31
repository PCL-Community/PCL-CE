using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 1.16 ~ 1.21.11 的存档格式。
/// 特征：DataVersion 在 [2567, 4189) 之间。
/// 变更：种子从 Data.RandomSeed 迁移到 Data.WorldGenSettings.seed。
/// </summary>
internal sealed class Version116To1211SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version116To1211;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value >= 2567 && dataVersion.Value < 4189;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = new Version19To1122SaveParser().Parse(folderPath, data, createdAt, modifiedAt);
        return baseInfo with
        {
            Seed = ReadWorldGenSeed(data),
            Spawn = NbtReadHelper.TryReadSpawnFromPos(data)
                 ?? NbtReadHelper.TryReadSpawnFromFields(data),
        };
    }

    /// <summary>从 Data.WorldGenSettings.seed 读取种子，失败时回退到 Data.RandomSeed。</summary>
    internal static long? ReadWorldGenSeed(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("WorldGenSettings", out var wgs) &&
            wgs!.TryGet<NbtLong>("seed", out var seed))
            return seed!.Value;
        return NbtReadHelper.TryGetLong(data, "RandomSeed");
    }
}

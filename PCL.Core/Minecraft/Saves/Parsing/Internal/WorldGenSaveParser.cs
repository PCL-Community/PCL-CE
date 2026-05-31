using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// WorldGen 解析器 —— 对应 20w20a(1.16) ~ 26.1-snapshot-5 之间的存档格式。
/// 特征：DataVersion 在 [2567, 4189) 之间。
/// 变更：种子从 Data.RandomSeed 迁移到 Data.WorldGenSettings.seed。
/// </summary>
internal sealed class WorldGenSaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.WorldGen;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion.HasValue && dataVersion.Value >= 2567 && dataVersion.Value < 4189;

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = new ModernSaveParser().Parse(folderPath, data, createdAt, modifiedAt);

        // 种子从 RandomSeed 迁移到了 WorldGenSettings.seed
        var seed = ReadWorldGenSeed(data);

        // 出生点优先从 spawn.pos 读取，其次从 SpawnX/Y/Z 读取
        var spawn = PreLegacySaveParser.TryReadSpawnFromPos(data)
                 ?? PreLegacySaveParser.TryReadSpawnFromFields(data);

        return baseInfo with
        {
            Seed = seed,
            Spawn = spawn,
        };
    }

    /// <summary>从 Data.WorldGenSettings.seed 读取种子，失败时回退到 Data.RandomSeed。</summary>
    internal static long? ReadWorldGenSeed(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("WorldGenSettings", out var wgs) &&
            wgs!.TryGet<NbtLong>("seed", out var seed))
            return seed!.Value;
        return PreLegacySaveParser.TryGetLong(data, "RandomSeed");
    }
}

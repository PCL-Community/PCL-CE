using System;
using System.IO;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// NextGen 解析器 —— 对应 26.1-snapshot-6 及之后的存档格式。
/// 特征：DataVersion >= 4189 或存在 difficulty_settings 复合标签。
/// 变更：
///   - 出生点迁移到 spawn.pos int[3]
///   - 难度迁移到 difficulty_settings 复合标签（字符串型难度）
///   - 种子可能在外部文件 data/minecraft/world_gen_settings.dat 中
/// </summary>
internal sealed class NextGenSaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.NextGen;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion >= 4189 || data.Contains("difficulty_settings");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = new ModernSaveParser().Parse(folderPath, data, createdAt, modifiedAt);

        // 种子：优先 WorldGenSettings，其次外部文件 world_gen_settings.dat
        var seed = WorldGenSaveParser.ReadWorldGenSeed(data)
                ?? ReadSeedFromExternalFile(folderPath);

        // 出生点：优先 spawn.pos，其次 SpawnX/Y/Z
        var spawn = PreLegacySaveParser.TryReadSpawnFromPos(data)
                 ?? PreLegacySaveParser.TryReadSpawnFromFields(data);

        // 难度信息现在存储在一个复合标签中，值为字符串
        var difficulty = ReadNextGenDifficulty(data);
        var isHardcore = ReadNextGenHardcore(data);
        var isLocked = ReadNextGenLocked(data);

        return baseInfo with
        {
            Seed = seed,
            Spawn = spawn,
            Difficulty = difficulty,
            IsHardcore = isHardcore,
            IsDifficultyLocked = isLocked,
            GameMode = isHardcore ? GameMode.Hardcore : baseInfo.GameMode,
        };
    }

    /// <summary>
    /// 从 difficulty_settings 复合标签读取难度（字符串型）。
    /// 如果不存在则回退到旧版字节型 Difficulty 字段。
    /// </summary>
    internal static Difficulty? ReadNextGenDifficulty(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtString>("difficulty", out var diffStr))
        {
            return diffStr!.Value switch
            {
                "peaceful" => Difficulty.Peaceful,
                "easy" => Difficulty.Easy,
                "normal" => Difficulty.Normal,
                "hard" => Difficulty.Hard,
                _ => null,
            };
        }
        // 回退到旧版字节型难度
        return LegacySaveParser.ReadDifficulty(data);
    }

    /// <summary>
    /// 从 difficulty_settings.hardcore 读取极限模式标志。
    /// 如果不存在则回退到旧版 Data.hardcore 字段。
    /// </summary>
    internal static bool ReadNextGenHardcore(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("hardcore", out var hc))
            return hc!.Value == 1;
        return data.TryGet<NbtByte>("hardcore", out var legacyHc) && legacyHc!.Value == 1;
    }

    /// <summary>
    /// 从 difficulty_settings.locked 读取难度锁定标志。
    /// 如果不存在则回退到旧版 DifficultyLocked 字段。
    /// </summary>
    internal static bool ReadNextGenLocked(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("locked", out var locked))
            return locked!.Value == 1;
        return data.TryGet<NbtByte>("DifficultyLocked", out var dl) && dl!.Value == 1;
    }

    /// <summary>
    /// 从外部文件 data/minecraft/world_gen_settings.dat 中读取种子。
    /// 此文件在 26.1+ 中替代 level.dat 内的常规存放位置。
    /// </summary>
    internal static long? ReadSeedFromExternalFile(string folderPath)
    {
        var externalPath = Path.Combine(folderPath, "data", "minecraft", "world_gen_settings.dat");
        if (!File.Exists(externalPath))
            return null;

        try
        {
            var nbtFile = new NbtFile(externalPath);
            var rootData = nbtFile.RootTag.Get<NbtCompound>("data");
            return rootData?.TryGet<NbtLong>("seed", out var seed) == true ? seed!.Value : null;
        }
        catch
        {
            return null;
        }
    }
}

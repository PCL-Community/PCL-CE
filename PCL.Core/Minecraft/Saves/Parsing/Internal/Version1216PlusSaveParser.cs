using System;
using System.IO;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 26w04a(1.21.6) 及之后的存档格式。
/// 特征：DataVersion >= 4189 或存在 difficulty_settings 复合标签。
/// 变更：
///   - 出生点迁移到 spawn.pos int[3]
///   - 难度迁移到 difficulty_settings 复合标签（字符串型）
///   - 种子可能在外部文件 data/minecraft/world_gen_settings.dat 中
/// </summary>
internal sealed class Version1216PlusSaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version1216Plus;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion >= 4189 || data.Contains("difficulty_settings");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = new Version19To1122SaveParser().Parse(folderPath, data, createdAt, modifiedAt);

        var seed = Version116To1215SaveParser.ReadWorldGenSeed(data)
                ?? ReadSeedFromExternalFile(folderPath);

        var spawn = Pre113SaveParser.TryReadSpawnFromPos(data)
                 ?? Pre113SaveParser.TryReadSpawnFromFields(data);

        var difficulty = ReadDifficultySettings(data);
        var isHardcore = ReadHardcore(data);
        var isLocked = ReadLocked(data);

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

    // ── difficulty_settings 复合标签解析 ──

    /// <summary>
    /// 从 difficulty_settings.difficulty 读取字符串型难度。
    /// 不存在时回退到旧版字节型 Difficulty 字段。
    /// </summary>
    internal static Difficulty? ReadDifficultySettings(NbtCompound data)
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
        return Pre113SaveParser.ReadDifficultyByte(data);
    }

    /// <summary>从 difficulty_settings.hardcore 读取极限模式标志。</summary>
    internal static bool ReadHardcore(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("hardcore", out var hc))
            return hc!.Value == 1;
        return data.TryGet<NbtByte>("hardcore", out var legacyHc) && legacyHc!.Value == 1;
    }

    /// <summary>从 difficulty_settings.locked 读取难度锁定标志。</summary>
    internal static bool ReadLocked(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("locked", out var locked))
            return locked!.Value == 1;
        return data.TryGet<NbtByte>("DifficultyLocked", out var dl) && dl!.Value == 1;
    }

    /// <summary>
    /// 从外部文件 data/minecraft/world_gen_settings.dat 中读取种子。
    /// 此文件在 26w04a+ 中替代 level.dat 内的常规存放位置。
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
        catch { return null; }
    }
}

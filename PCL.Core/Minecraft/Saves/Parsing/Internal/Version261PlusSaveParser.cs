using System;
using System.IO;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 26.1-snapshot-6 及之后的存档格式（2026 新版本号体系）。
/// 特征：DataVersion >= 4774 或存在 difficulty_settings 复合标签。
/// 变更：
///   - 出生点迁移到 spawn.pos int[3]
///   - 难度迁移到 difficulty_settings 复合标签（字符串型）
///   - 种子可能在外部文件 data/minecraft/world_gen_settings.dat 中
/// </summary>
internal sealed class Version261PlusSaveParser : ISaveParser
{
    private readonly ISaveParser _baseParser;

    public Version261PlusSaveParser() : this(new Version19To1122SaveParser()) { }
    public Version261PlusSaveParser(ISaveParser baseParser) => _baseParser = baseParser;

    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version261Plus;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion >= DataVersionBoundaries._261snapshot6
        || data.Contains("difficulty_settings");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var baseInfo = _baseParser.Parse(folderPath, data, createdAt, modifiedAt);

        var seed = Version116To1211SaveParser.ReadWorldGenSeed(data)
                ?? ReadSeedFromExternalFile(folderPath);

        var spawn = NbtReadHelper.TryReadSpawnFromPos(data)
                 ?? NbtReadHelper.TryReadSpawnFromFields(data);

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
        return NbtReadHelper.ReadDifficultyByte(data);
    }

    internal static bool ReadHardcore(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("hardcore", out var hc))
            return hc!.Value == 1;
        return data.TryGet<NbtByte>("hardcore", out var legacyHc) && legacyHc!.Value == 1;
    }

    internal static bool ReadLocked(NbtCompound data)
    {
        if (data.TryGet<NbtCompound>("difficulty_settings", out var ds) &&
            ds!.TryGet<NbtByte>("locked", out var locked))
            return locked!.Value == 1;
        return data.TryGet<NbtByte>("DifficultyLocked", out var dl) && dl!.Value == 1;
    }

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

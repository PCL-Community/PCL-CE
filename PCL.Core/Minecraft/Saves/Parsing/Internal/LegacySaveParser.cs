using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// Legacy 解析器 —— 对应 1.3.1 ~ 1.8-pre 之间的存档格式。
/// 特征：没有 DataVersion，有 allowCommands。
/// </summary>
internal sealed class LegacySaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Legacy;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion is null && data.Contains("allowCommands");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        var difficulty = ReadDifficulty(data);
        var isHardcore = data.TryGet<NbtByte>("hardcore", out var hc) && hc!.Value == 1;
        var isLocked = data.TryGet<NbtByte>("DifficultyLocked", out var dl) && dl!.Value == 1;
        var allowCommands = data.TryGet<NbtByte>("allowCommands", out var ac) && ac!.Value == 1;

        return new SaveInfo
        {
            LevelName = data.TryGet<NbtString>("LevelName", out var ln) ? ln!.Value : "unknown",
            VersionName = null,
            VersionId = null,
            Seed = PreLegacySaveParser.TryGetLong(data, "RandomSeed"),
            LastPlayedUtc = PreLegacySaveParser.ReadLastPlayed(data),
            Spawn = PreLegacySaveParser.TryReadSpawnFromFields(data),
            GameMode = PreLegacySaveParser.ReadGameMode(data, out _),
            Difficulty = difficulty,
            IsDifficultyLocked = isLocked,
            IsHardcore = isHardcore,
            AllowCommands = allowCommands,
            PlayTime = PreLegacySaveParser.ReadPlayTime(data),
            FolderPath = folderPath,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
        };
    }

    /// <summary>读取字节型难度字段（0=和平, 1=简单, 2=普通, 3=困难）。</summary>
    internal static Difficulty? ReadDifficulty(NbtCompound data)
    {
        if (data.TryGet<NbtByte>("Difficulty", out var diff))
            return (Difficulty)diff!.Value;
        return null;
    }
}

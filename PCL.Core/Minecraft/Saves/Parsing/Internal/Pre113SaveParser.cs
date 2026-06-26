using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// Alpha ~ 1.2.5 的存档格式。
/// 特征：没有 DataVersion、没有 allowCommands、没有 Difficulty。
/// </summary>
internal sealed class Pre113SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Pre113;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion is null && !data.Contains("allowCommands");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        return new SaveInfo
        {
            LevelName = data.TryGet<NbtString>("LevelName", out var ln) ? ln!.Value : "unknown",
            VersionName = null,
            VersionId = null,
            Seed = NbtReadHelper.TryGetLong(data, "RandomSeed"),
            LastPlayedUtc = NbtReadHelper.ReadLastPlayed(data),
            Spawn = NbtReadHelper.TryReadSpawnFromFields(data),
            GameMode = NbtReadHelper.ReadGameMode(data, out var isHardcore),
            Difficulty = null,
            IsDifficultyLocked = false,
            IsHardcore = isHardcore,
            AllowCommands = false,
            PlayTime = NbtReadHelper.ReadPlayTime(data),
            FolderPath = folderPath,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
        };
    }
}

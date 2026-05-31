using System;
using fNbt;

namespace PCL.Core.Minecraft.Saves.Parsing.Internal;

/// <summary>
/// 1.3.1 ~ 1.8.9 的存档格式。
/// 特征：没有 DataVersion，有 allowCommands。
/// </summary>
internal sealed class Version131To189SaveParser : ISaveParser
{
    public SaveFormatVersion FormatVersion => SaveFormatVersion.Version131To189;

    public bool CanHandle(NbtCompound data, int? dataVersion)
        => dataVersion is null && data.Contains("allowCommands");

    public SaveInfo Parse(string folderPath, NbtCompound data, DateTime createdAt, DateTime modifiedAt)
    {
        return new SaveInfo
        {
            LevelName = data.TryGet<NbtString>("LevelName", out var ln) ? ln!.Value : "unknown",
            VersionName = null,
            VersionId = null,
            Seed = Pre113SaveParser.TryGetLong(data, "RandomSeed"),
            LastPlayedUtc = Pre113SaveParser.ReadLastPlayed(data),
            Spawn = Pre113SaveParser.TryReadSpawnFromFields(data),
            GameMode = Pre113SaveParser.ReadGameMode(data, out _),
            Difficulty = Pre113SaveParser.ReadDifficultyByte(data),
            IsDifficultyLocked = data.TryGet<NbtByte>("DifficultyLocked", out var dl) && dl!.Value == 1,
            IsHardcore = data.TryGet<NbtByte>("hardcore", out var hc) && hc!.Value == 1,
            AllowCommands = data.TryGet<NbtByte>("allowCommands", out var ac) && ac!.Value == 1,
            PlayTime = Pre113SaveParser.ReadPlayTime(data),
            FolderPath = folderPath,
            CreatedAt = createdAt,
            ModifiedAt = modifiedAt,
        };
    }
}

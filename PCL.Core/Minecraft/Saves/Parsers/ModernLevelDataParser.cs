using System;
using System.IO;
using fNbt;
using PCL.Core.Minecraft.Saves.Models;

namespace PCL.Core.Minecraft.Saves.Parsers;

/// <summary>
/// 26.1+ 新格式解析器（DataVersion >= 4774）
/// </summary>
public class ModernLevelDataParser : ILevelDataParser
{
    public LevelDataInfo Parse(NbtCompound dataTag, string saveFolderPath)
    {
        var result = new LevelDataInfo
        {
            HasDataVersion = dataTag.Contains("DataVersion"),
            DataVersion = dataTag.Get<NbtInt>("DataVersion")?.Value
        };

        // 存档名称
        result.LevelName = dataTag.Get<NbtString>("LevelName")?.Value ?? "未知";

        // 版本信息（Version 复合标签，位置未变）
        var versionCompound = dataTag.Get<NbtCompound>("Version");
        if (versionCompound != null)
        {
            result.VersionName = versionCompound.Get<NbtString>("Name")?.Value;
            result.VersionId = versionCompound.Get<NbtInt>("Id")?.Value;
        }

        // 种子：从 world_gen_settings.dat 读取
        result.Seed = ReadSeedFromWorldGenSettings(saveFolderPath) ?? "获取失败";

        // 命令权限
        result.HasAllowCommands = dataTag.Contains("allowCommands");
        result.AllowCommands = dataTag.Get<NbtByte>("allowCommands")?.Value;

        // 难度和锁定：从 difficulty_settings 读取
        var difficultySettings = dataTag.Get<NbtCompound>("difficulty_settings");
        if (difficultySettings != null)
        {
            result.HasDifficulty = true;
            var difficulty = difficultySettings.Get<NbtString>("difficulty")?.Value;
            result.DifficultyDisplay = difficulty switch
            {
                "peaceful" => "和平",
                "easy" => "简单",
                "normal" => "普通",
                "hard" => "困难",
                _ => "未知"
            };
            result.IsDifficultyLocked = difficultySettings.Get<NbtByte>("locked")?.Value == 1;
        }

        // 极限模式
        result.IsHardcore = dataTag.Get<NbtByte>("hardcore")?.Value == 1;

        // 最后游玩时间
        var lastPlayedTag = dataTag.Get<NbtLong>("LastPlayed");
        if (lastPlayedTag != null)
        {
            result.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedTag.Value).LocalDateTime;
        }

        // 出生点：新格式在 spawn.pos
        result.SpawnPoint = GetSpawnPointModern(dataTag);

        // 游戏模式
        result.GameType = GetGameTypeModern(dataTag, result.IsHardcore);

        // 游戏时长
        var timeTag = dataTag.Get<NbtLong>("Time");
        if (timeTag != null)
        {
            result.PlayTime = TimeSpan.FromSeconds(timeTag.Value / 20.0);
        }

        return result;
    }

    private string? ReadSeedFromWorldGenSettings(string saveFolderPath)
    {
        var worldGenPath = Path.Combine(saveFolderPath, "data", "minecraft", "world_gen_settings.dat");
        if (!File.Exists(worldGenPath))
            return null;

        try
        {
            using var fs = new FileStream(worldGenPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var worldGenFile = new NbtFile();
            worldGenFile.LoadFromStream(fs, NbtCompression.AutoDetect);

            var dataTag = worldGenFile.RootTag?.Get<NbtCompound>("data");
            var seedTag = dataTag?.Get<NbtLong>("seed");
            return seedTag?.Value.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string GetSpawnPointModern(NbtCompound dataTag)
    {
        var spawnCompound = dataTag.Get<NbtCompound>("spawn");
        if (spawnCompound != null)
        {
            var posArray = spawnCompound.Get<NbtIntArray>("pos");
            if (posArray?.Value.Length >= 3)
                return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
        }
        return "获取失败";
    }

    private string GetGameTypeModern(NbtCompound dataTag, bool isHardcore)
    {
        if (isHardcore)
            return "极限模式";

        var gameTypeTag = dataTag.Get<NbtInt>("GameType");
        if (gameTypeTag == null)
            return "生存模式";

        return gameTypeTag.Value switch
        {
            0 => "生存模式",
            1 => "创造模式",
            2 => "冒险模式",
            3 => "旁观模式",
            _ => "生存模式"
        };
    }
}
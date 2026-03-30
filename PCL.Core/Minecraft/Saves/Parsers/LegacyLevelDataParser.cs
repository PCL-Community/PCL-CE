using System;
using System.IO;
using fNbt;
using PCL.Core.Minecraft.Saves.Models;

namespace PCL.Core.Minecraft.Saves.Parsers;

/// <summary>
/// 26.1 前旧格式解析器（DataVersion < 4774）
/// </summary>
public class LegacyLevelDataParser : ILevelDataParser
{
    public LevelDataInfo Parse(NbtCompound dataTag, string saveFolderPath)
    {
        var result = new LevelDataInfo
        {
            HasDataVersion = dataTag.Contains("DataVersion"),
            DataVersion = dataTag.Get<NbtInt>("DataVersion")?.Value
        };

        // 存档名称
        result.LevelName = dataTag.Get<NbtString>("LevelName")?.Value ?? "获取失败";

        // 版本信息（Version 复合标签）
        var versionCompound = dataTag.Get<NbtCompound>("Version");
        if (versionCompound != null)
        {
            result.VersionName = versionCompound.Get<NbtString>("Name")?.Value;
            result.VersionId = versionCompound.Get<NbtInt>("Id")?.Value;
        }

        // 种子：从 RandomSeed 或 WorldGenSettings.seed 读取
        result.Seed = GetSeedLegacy(dataTag);

        // 命令权限
        result.HasAllowCommands = dataTag.Contains("allowCommands");
        result.AllowCommands = dataTag.Get<NbtByte>("allowCommands")?.Value;

        // 难度和锁定
        result.HasDifficulty = dataTag.Contains("Difficulty");
        if (result.HasDifficulty)
        {
            var difficulty = dataTag.Get<NbtByte>("Difficulty")?.Value ?? 2;
            result.DifficultyDisplay = difficulty switch
            {
                0 => "和平",
                1 => "简单",
                2 => "普通",
                3 => "困难",
                _ => "获取失败"
            };
        }
        result.IsDifficultyLocked = dataTag.Get<NbtByte>("DifficultyLocked")?.Value == 1;

        // 极限模式
        result.IsHardcore = dataTag.Get<NbtByte>("hardcore")?.Value == 1;

        // 最后游玩时间
        var lastPlayedTag = dataTag.Get<NbtLong>("LastPlayed");
        if (lastPlayedTag != null)
        {
            result.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedTag.Value).LocalDateTime;
        }

        // 出生点
        result.SpawnPoint = GetSpawnPointLegacy(dataTag);

        // 游戏模式
        result.GameType = GetGameTypeLegacy(dataTag, result.IsHardcore);

        // 游戏时长
        var timeTag = dataTag.Get<NbtLong>("Time");
        if (timeTag != null)
        {
            result.PlayTime = TimeSpan.FromSeconds(timeTag.Value / 20.0);
        }

        return result;
    }

    private string GetSeedLegacy(NbtCompound dataTag)
    {
        var seedLong = dataTag.Get<NbtLong>("RandomSeed");
        if (seedLong != null)
            return seedLong.Value.ToString();

        var worldGen = dataTag.Get<NbtCompound>("WorldGenSettings");
        if (worldGen != null)
        {
            var seed = worldGen.Get<NbtLong>("seed");
            if (seed != null)
                return seed.Value.ToString();
        }
        return "获取失败";
    }

    private string GetSpawnPointLegacy(NbtCompound dataTag)
    {
        var spawnX = dataTag.Get<NbtInt>("SpawnX");
        if (spawnX != null)
        {
            var spawnY = dataTag.Get<NbtInt>("SpawnY");
            var spawnZ = dataTag.Get<NbtInt>("SpawnZ");
            return $"{spawnX.Value} / {spawnY?.Value ?? 0} / {spawnZ?.Value ?? 0}";
        }

        // 兼容部分旧版本的 spawn 复合标签
        var spawnCompound = dataTag.Get<NbtCompound>("spawn");
        if (spawnCompound != null)
        {
            var posArray = spawnCompound.Get<NbtIntArray>("pos");
            if (posArray?.Value.Length >= 3)
                return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
        }
        return "获取失败";
    }

    private string GetGameTypeLegacy(NbtCompound dataTag, bool isHardcore)
    {
        if (isHardcore)
            return "极限模式";

        var gameTypeTag = dataTag.Get<NbtInt>("GameType");
        if (gameTypeTag == null)
            return "获取失败";

        return gameTypeTag.Value switch
        {
            0 => "生存模式",
            1 => "创造模式",
            2 => "冒险模式",
            3 => "旁观模式",
            _ => "获取失败"
        };
    }
}
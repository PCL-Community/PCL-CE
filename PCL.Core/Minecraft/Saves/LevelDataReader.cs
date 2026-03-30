﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档数据读取器
/// </summary>
public class LevelDataReader
{
    private readonly string _levelDatPath;
    private readonly string? _saveFolderPath;
    private NbtFile? _nbtFile;
    private NbtCompound? _gameLevel;
    
    /// <summary>
    /// 初始化读取器
    /// </summary>
    /// <param name="levelDatPath">level.dat 文件完整路径</param>
    public LevelDataReader(string levelDatPath)
    {
        _levelDatPath = levelDatPath;
        _saveFolderPath = Path.GetDirectoryName(levelDatPath);
    }
    
    /// <summary>
    /// 异步加载并解析存档数据
    /// </summary>
    public async Task<LevelDataInfo?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 使用现有的 NbtFileHandler 读取文件
            var dataTag = await NbtFileHandler.ReadTagInNbtFileAsync<NbtCompound>(_levelDatPath, "Data", cancellationToken);
            if (dataTag == null)
            {
                LogWrapper.Warn($"无法读取 level.dat 中的 Data 标签: {_levelDatPath}");
                return null;
            }
            
            // 重要：创建副本，因为 dataTag 可能已经属于其他 NbtFile
            // 使用 Clone() 方法创建独立的副本
            var clonedDataTag = (NbtCompound)dataTag.Clone();
            
            var rootTag = new NbtCompound("");
            rootTag.Add(clonedDataTag);
            _nbtFile = new NbtFile(rootTag);
            _gameLevel = clonedDataTag;
            
            return Parse();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"解析存档数据失败: {_levelDatPath}");
            return null;
        }
    }
    
    /// <summary>
    /// 同步解析已加载的 NBT 数据
    /// </summary>
    private LevelDataInfo Parse()
    {
        if (_gameLevel == null)
            throw new InvalidOperationException("请先调用 LoadAsync 加载数据");
        
        var result = new LevelDataInfo();
        
        // DataVersion
        var dataVersionTag = _gameLevel.Get<NbtInt>("DataVersion");
        result.HasDataVersion = dataVersionTag != null;
        result.DataVersion = dataVersionTag?.Value;
        
        // 存档名称
        var levelNameTag = _gameLevel.Get<NbtString>("LevelName");
        result.LevelName = levelNameTag?.Value ?? "未知";
        
        // 版本信息
        var versionCompound = _gameLevel.Get<NbtCompound>("Version");
        if (versionCompound != null)
        {
            var nameTag = versionCompound.Get<NbtString>("Name");
            var idTag = versionCompound.Get<NbtInt>("Id");
            result.VersionName = nameTag?.Value;
            result.VersionId = idTag?.Value;
        }
        
        // 种子
        result.Seed = GetSeed(result);
        
        // 命令权限
        result.HasAllowCommands = _gameLevel.Contains("allowCommands");
        var allowCmdTag = _gameLevel.Get<NbtByte>("allowCommands");
        result.AllowCommands = allowCmdTag?.Value;
        
        // 难度和锁定
        ParseDifficulty(result);
        
        // 极限模式
        var hardcoreTag = _gameLevel.Get<NbtByte>("hardcore");
        result.IsHardcore = hardcoreTag?.Value == 1;
        
        // 最后游玩时间
        var lastPlayedTag = _gameLevel.Get<NbtLong>("LastPlayed");
        if (lastPlayedTag != null)
        {
            result.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedTag.Value).LocalDateTime;
        }
        
        // 出生点
        result.SpawnPoint = GetSpawnPoint(result);
        
        // 游戏模式
        result.GameType = GetGameType(result);
        
        // 游戏时长
        var timeTag = _gameLevel.Get<NbtLong>("Time");
        if (timeTag != null)
        {
            result.PlayTime = TimeSpan.FromSeconds(timeTag.Value / 20.0);
        }
        
        return result;
    }
    
    private string GetSeed(LevelDataInfo result)
    {
        if (_gameLevel == null) return "获取失败";
        
        if (result.IsNewFormat && _saveFolderPath != null)
        {
            // 26.1+ 从 world_gen_settings.dat 读取
            var worldGenPath = Path.Combine(_saveFolderPath, "data", "minecraft", "world_gen_settings.dat");
            if (File.Exists(worldGenPath))
            {
                try
                {
                    using var fs = new FileStream(worldGenPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var worldGenFile = new NbtFile();
                    worldGenFile.LoadFromStream(fs, NbtCompression.AutoDetect);
                    
                    var dataTag = worldGenFile.RootTag?.Get<NbtCompound>("data");
                    var seedTag = dataTag?.Get<NbtLong>("seed");
                    if (seedTag != null)
                        return seedTag.Value.ToString();
                }
                catch (Exception ex)
                {
                    LogWrapper.Debug($"读取 world_gen_settings.dat 失败: {ex.Message}");
                }
            }
        }
        
        // 旧格式或备用方案
        var seedLong = _gameLevel.Get<NbtLong>("RandomSeed");
        if (seedLong != null)
            return seedLong.Value.ToString();
        
        var worldGen = _gameLevel.Get<NbtCompound>("WorldGenSettings");
        if (worldGen != null)
        {
            var seed = worldGen.Get<NbtLong>("seed");
            if (seed != null)
                return seed.Value.ToString();
        }
        
        return "获取失败";
    }
    
    private void ParseDifficulty(LevelDataInfo result)
    {
        if (_gameLevel == null) return;
        
        var difficultySettings = _gameLevel.Get<NbtCompound>("difficulty_settings");
        
        if (difficultySettings != null)
        {
            // 26.1+ 格式
            var difficultyTag = difficultySettings.Get<NbtString>("difficulty");
            if (difficultyTag != null)
            {
                result.Difficulty = difficultyTag.Value;
                result.HasDifficulty = true;
            }
            
            var lockedTag = difficultySettings.Get<NbtByte>("locked");
            result.IsDifficultyLocked = lockedTag?.Value == 1;
        }
        else if (_gameLevel.Contains("Difficulty"))
        {
            // 旧格式
            var difficultyTag = _gameLevel.Get<NbtByte>("Difficulty");
            if (difficultyTag != null)
            {
                result.DifficultyOld = difficultyTag.Value;
                result.HasDifficulty = true;
            }
            
            var lockedTag = _gameLevel.Get<NbtByte>("DifficultyLocked");
            result.IsDifficultyLocked = lockedTag?.Value == 1;
        }
    }
    
    private string GetSpawnPoint(LevelDataInfo result)
    {
        if (_gameLevel == null) return "获取失败";
        
        if (result.IsNewFormat)
        {
            var spawnCompound = _gameLevel.Get<NbtCompound>("spawn");
            if (spawnCompound != null)
            {
                var posArray = spawnCompound.Get<NbtIntArray>("pos");
                if (posArray?.Value.Length >= 3)
                    return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
            }
        }
        
        // 旧格式
        var spawnX = _gameLevel.Get<NbtInt>("SpawnX");
        if (spawnX != null)
        {
            var spawnY = _gameLevel.Get<NbtInt>("SpawnY");
            var spawnZ = _gameLevel.Get<NbtInt>("SpawnZ");
            return $"{spawnX.Value} / {spawnY?.Value ?? 0} / {spawnZ?.Value ?? 0}";
        }
        
        // 兼容格式
        var spawnCompoundCompat = _gameLevel.Get<NbtCompound>("spawn");
        if (spawnCompoundCompat != null)
        {
            var posArray = spawnCompoundCompat.Get<NbtIntArray>("pos");
            if (posArray?.Value.Length >= 3)
                return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
        }
        
        return "获取失败";
    }
    
    private string GetGameType(LevelDataInfo result)
    {
        if (_gameLevel == null) return "生存模式";
        
        if (result.IsHardcore)
            return "极限模式";
        
        var gameTypeTag = _gameLevel.Get<NbtInt>("GameType");
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
    
    /// <summary>
    /// 获取原始 NbtFile 对象（用于写入）
    /// </summary>
    public NbtFile? GetNbtFile() => _nbtFile;
}
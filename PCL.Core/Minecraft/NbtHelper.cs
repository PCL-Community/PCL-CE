// PCL.Core/Minecraft/NbtHelper.cs
using fNbt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft
{
    public class LevelDataInfo
    {
        public string LevelName { get; set; }
        public string VersionName { get; set; }
        public int? VersionId { get; set; }
        public string Seed { get; set; }
        public bool HasAllowCommands { get; set; }
        public int? AllowCommands { get; set; }
        public bool HasDifficulty { get; set; }
        public string Difficulty { get; set; }  // 26.1后改为字符串: peaceful/easy/normal/hard
        public int? DifficultyOld { get; set; } // 26.1前的数字难度
        public bool IsDifficultyLocked { get; set; }
        public bool IsHardcore { get; set; }
        public DateTime LastPlayed { get; set; }
        public string SpawnPoint { get; set; }
        public string GameType { get; set; }
        public TimeSpan PlayTime { get; set; }
        public int? DataVersion { get; set; }
        public bool IsNewFormat { get; set; }
        public bool HasDataVersion { get; set; }
    }

    public static class NbtHelper
    {
        // 版本常量
        private const int DATA_VERSION_26_1 = 4774; // 26.1版本的数据版本
        private const int DATA_VERSION_1_13 = 1444; // 1.13版本的数据版本
        
        /// <summary>
        /// 解析level.dat文件，自动适配新老格式
        /// </summary>
        public static LevelDataInfo ParseLevelData(NbtFile saveInfo, string savePath = null)
        {
            var result = new LevelDataInfo();
            var gameLevel = saveInfo.RootTag.Get<NbtCompound>("Data");
            
            // 检查DataVersion是否存在
            var dataVersionTag = gameLevel.Get<NbtInt>("DataVersion");
            result.HasDataVersion = dataVersionTag != null;
            result.DataVersion = dataVersionTag?.Value;
            
            // 判断格式版本
            result.IsNewFormat = result.HasDataVersion && result.DataVersion.Value >= DATA_VERSION_26_1;
            
            // 存档名称
            var levelNameTag = gameLevel.Get<NbtString>("LevelName");
            result.LevelName = levelNameTag?.Value ?? "未知";
            
            // 版本信息 - 统一从Version复合标签读取（26.1前后都在这里）
            var versionCompound = gameLevel.Get<NbtCompound>("Version");
            if (versionCompound != null)
            {
                var nameTag = versionCompound.Get<NbtString>("Name");
                var idTag = versionCompound.Get<NbtInt>("Id");
                result.VersionName = nameTag?.Value;
                result.VersionId = idTag?.Value;
            }
            
            // 种子 - 根据格式不同从不同位置读取
            result.Seed = GetSeed(gameLevel, result.IsNewFormat, savePath);
            
            // 命令权限
            result.HasAllowCommands = gameLevel.Contains("allowCommands");
            if (result.HasAllowCommands)
            {
                var allowCmdTag = gameLevel.Get<NbtByte>("allowCommands");
                result.AllowCommands = allowCmdTag?.Value ?? 0;
            }
            
            // 难度和锁定 - 根据格式不同读取方式不同
            result.HasDifficulty = false;
            var difficultySettings = gameLevel.Get<NbtCompound>("difficulty_settings");
            
            if (difficultySettings != null)
            {
                // 26.1后新格式：难度和锁定都在difficulty_settings中
                var difficultyTag = difficultySettings.Get<NbtString>("difficulty");
                if (difficultyTag != null)
                {
                    result.Difficulty = difficultyTag.Value;
                    result.HasDifficulty = true;
                }
                
                // 读取锁定状态
                var lockedTag = difficultySettings.Get<NbtByte>("locked");
                result.IsDifficultyLocked = lockedTag?.Value == 1;
            }
            else if (gameLevel.Contains("Difficulty"))
            {
                // 26.1前旧格式：难度在Difficulty字节，锁定在DifficultyLocked
                var difficultyTag = gameLevel.Get<NbtByte>("Difficulty");
                if (difficultyTag != null)
                {
                    result.DifficultyOld = difficultyTag.Value;
                    result.HasDifficulty = true;
                }
                
                // 读取锁定状态
                var lockedTag = gameLevel.Get<NbtByte>("DifficultyLocked");
                result.IsDifficultyLocked = lockedTag?.Value == 1;
            }
            
            // 极限模式
            var hardcoreTag = gameLevel.Get<NbtByte>("hardcore");
            result.IsHardcore = hardcoreTag?.Value == 1;
            
            // 最后游玩时间
            var lastPlayedTag = gameLevel.Get<NbtLong>("LastPlayed");
            if (lastPlayedTag != null)
            {
                result.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(lastPlayedTag.Value).LocalDateTime;
            }
            
            // 出生点
            result.SpawnPoint = GetSpawnPoint(gameLevel, result.IsNewFormat);
            
            // 游戏模式
            result.GameType = GetGameType(gameLevel, result.IsHardcore);
            
            // 游戏时长
            var timeTag = gameLevel.Get<NbtLong>("Time");
            if (timeTag != null)
            {
                result.PlayTime = TimeSpan.FromSeconds(timeTag.Value / 20.0);
            }
            
            return result;
        }
        
        private static string GetSeed(NbtCompound gameLevel, bool isNewFormat, string savePath)
        {
            if (isNewFormat)
            {
                // 26.1后新格式：种子在 {存档名}/data/minecraft/world_gen_settings.dat
                if (!string.IsNullOrEmpty(savePath))
                {
                    var worldGenSettingsPath = Path.Combine(
                        Path.GetDirectoryName(savePath), 
                        "data", 
                        "minecraft", 
                        "world_gen_settings.dat");
                    
                    if (File.Exists(worldGenSettingsPath))
                    {
                        try
                        {
                            using var fs = new FileStream(worldGenSettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var worldGenFile = new NbtFile();
                            worldGenFile.LoadFromStream(fs, NbtCompression.AutoDetect);
                            
                            var dataTag = worldGenFile.RootTag.Get<NbtCompound>("data");
                            if (dataTag != null)
                            {
                                var seedTag = dataTag.Get<NbtLong>("seed");
                                if (seedTag != null)
                                    return seedTag.Value.ToString();
                            }
                        }
                        catch
                        {
                            // 如果读取失败，继续尝试其他方式
                        }
                    }
                }
                
                // 备用：尝试从level.dat读取（某些版本可能存在）
                var worldGen = gameLevel.Get<NbtCompound>("WorldGenSettings");
                if (worldGen != null)
                {
                    var seed = worldGen.Get<NbtLong>("seed");
                    if (seed != null)
                        return seed.Value.ToString();
                }
            }
            else
            {
                // 26.1前旧格式：种子在RandomSeed
                var seedLong = gameLevel.Get<NbtLong>("RandomSeed");
                if (seedLong != null)
                    return seedLong.Value.ToString();
                    
                // 兼容1.16+的旧格式
                var worldGen = gameLevel.Get<NbtCompound>("WorldGenSettings");
                if (worldGen != null)
                {
                    var seed = worldGen.Get<NbtLong>("seed");
                    if (seed != null)
                        return seed.Value.ToString();
                }
            }
            
            return "获取失败";
        }
        
        private static string GetSpawnPoint(NbtCompound gameLevel, bool isNewFormat)
        {
            if (isNewFormat)
            {
                // 26.1后新格式：出生点在spawn.pos
                var spawnCompound = gameLevel.Get<NbtCompound>("spawn");
                if (spawnCompound != null)
                {
                    var posArray = spawnCompound.Get<NbtIntArray>("pos");
                    if (posArray != null && posArray.Value.Length >= 3)
                    {
                        return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
                    }
                }
            }
            else
            {
                // 26.1前旧格式：出生点在SpawnX, SpawnY, SpawnZ
                var spawnX = gameLevel.Get<NbtInt>("SpawnX");
                if (spawnX != null)
                {
                    var spawnY = gameLevel.Get<NbtInt>("SpawnY");
                    var spawnZ = gameLevel.Get<NbtInt>("SpawnZ");
                    return $"{spawnX.Value} / {spawnY?.Value ?? 0} / {spawnZ?.Value ?? 0}";
                }
                
                // 兼容部分旧版本的spawn复合标签
                var spawnCompound = gameLevel.Get<NbtCompound>("spawn");
                if (spawnCompound != null)
                {
                    var posArray = spawnCompound.Get<NbtIntArray>("pos");
                    if (posArray != null && posArray.Value.Length >= 3)
                    {
                        return $"{posArray.Value[0]} / {posArray.Value[1]} / {posArray.Value[2]}";
                    }
                }
            }
            
            return "获取失败";
        }
        
        private static string GetGameType(NbtCompound gameLevel, bool isHardcore)
        {
            if (isHardcore)
                return "极限模式";
            
            var gameTypeTag = gameLevel.Get<NbtInt>("GameType");
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
        
        public static string GetDifficultyName(string difficulty)
        {
            return difficulty switch
            {
                "peaceful" => "和平",
                "easy" => "简单",
                "normal" => "普通",
                "hard" => "困难",
                _ => "未知"
            };
        }
        
        public static string GetDifficultyName(int? difficulty)
        {
            return difficulty switch
            {
                0 => "和平",
                1 => "简单",
                2 => "普通",
                3 => "困难",
                _ => "未知"
            };
        }
        
        public static string GetVersionHint(bool hasDataVersion, bool hasDifficulty, bool hasAllowCommands)
        {
            // 有DataVersion表示1.9及以上版本
            if (hasDataVersion)
                return null;
                
            // 没有DataVersion表示1.9之前的极老版本
            if (hasDifficulty)
                return "1.9 以下的版本无法获取存档版本";
            
            if (hasAllowCommands)
                return "1.8 以下的版本无法获取存档版本和游戏难度";
                
            return "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊";
        }
        
        public static bool ShouldShowDataPack(int? dataVersion)
        {
            // 数据包功能在1.13(数据版本1444)引入
            return dataVersion.HasValue && dataVersion.Value >= DATA_VERSION_1_13;
        }
        
        /// <summary>
        /// 修改存档的命令权限
        /// </summary>
        public static void ModifyAllowCommands(NbtFile saveInfo, int newValue)
        {
            var gameLevel = saveInfo.RootTag.Get<NbtCompound>("Data");
            var allowCmdTag = gameLevel.Get<NbtByte>("allowCommands");
            if (allowCmdTag != null)
            {
                allowCmdTag.Value = (byte)newValue;
            }
        }
        
        /// <summary>
        /// 修改存档的难度设置
        /// </summary>
        public static void ModifyDifficulty(NbtFile saveInfo, int newDifficulty, bool? newLocked = null, bool isHardcore = false, bool isNewFormat = false)
        {
            var gameLevel = saveInfo.RootTag.Get<NbtCompound>("Data");
            
            if (isNewFormat)
            {
                // 26.1后新格式：修改difficulty_settings
                var difficultySettings = gameLevel.Get<NbtCompound>("difficulty_settings");
                if (difficultySettings == null)
                {
                    difficultySettings = new NbtCompound("difficulty_settings");
                    gameLevel.Add(difficultySettings);
                }
                
                // 转换难度值为字符串
                string difficultyStr = newDifficulty switch
                {
                    0 => "peaceful",
                    1 => "easy",
                    2 => "normal",
                    3 => "hard",
                    _ => "normal"
                };
                
                // 修改难度值
                var difficultyTag = difficultySettings.Get<NbtString>("difficulty");
                if (difficultyTag != null)
                {
                    difficultyTag.Value = difficultyStr;
                }
                else
                {
                    difficultySettings.Add(new NbtString("difficulty", difficultyStr));
                }
                
                // 修改难度锁定(非极限模式)
                if (!isHardcore && newLocked.HasValue)
                {
                    if (difficultySettings.Contains("locked"))
                    {
                        difficultySettings.Get<NbtByte>("locked").Value = (byte)(newLocked.Value ? 1 : 0);
                    }
                    else if (newLocked.Value)
                    {
                        difficultySettings.Add(new NbtByte("locked", 1));
                    }
                    else if (!newLocked.Value && difficultySettings.Contains("locked"))
                    {
                        // 如果设置为未锁定，且locked标签存在，则设置为0
                        difficultySettings.Get<NbtByte>("locked").Value = 0;
                    }
                }
            }
            else
            {
                // 26.1前旧格式：修改Difficulty字节
                var difficultyTag = gameLevel.Get<NbtByte>("Difficulty");
                if (difficultyTag != null)
                {
                    difficultyTag.Value = (byte)newDifficulty;
                }
                
                // 修改难度锁定(非极限模式)
                if (!isHardcore && newLocked.HasValue)
                {
                    if (gameLevel.Contains("DifficultyLocked"))
                    {
                        gameLevel.Get<NbtByte>("DifficultyLocked").Value = (byte)(newLocked.Value ? 1 : 0);
                    }
                    else if (newLocked.Value)
                    {
                        gameLevel.Add(new NbtByte("DifficultyLocked", 1));
                    }
                }
            }
        }
        
        /// <summary>
        /// 保存level.dat文件
        /// </summary>
        public static void SaveLevelData(string filePath, NbtFile saveInfo)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            saveInfo.SaveToStream(fileStream, NbtCompression.GZip);
        }
        
        /// <summary>
        /// 加载level.dat文件
        /// </summary>
        public static NbtFile LoadLevelData(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var saveInfo = new NbtFile();
            saveInfo.LoadFromStream(fs, NbtCompression.AutoDetect);
            return saveInfo;
        }
        
        /// <summary>
        /// 获取Chunkbase支持的版本字符串
        /// </summary>
        public static string GetChunkbaseVersion(string versionName)
        {
            if (string.IsNullOrEmpty(versionName))
                return null;
                
            if (versionName.Any(char.IsLetter))
                return null; // 预览版不支持
                
            if (versionName.StartsWith("1.21"))
                return versionName.Replace(".", "_");
                
            if (versionName.Contains("."))
            {
                var parts = versionName.Split('.');
                return $"{parts[0]}_{parts[1]}";
            }
            
            return versionName.Replace(".", "_");
        }
        
        /// <summary>
        /// 构建Chunkbase URL
        /// </summary>
        public static string BuildChunkbaseUrl(string seed, string versionName)
        {
            var version = GetChunkbaseVersion(versionName);
            if (version == null)
                return null;
                
            return $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{version}&dimension=overworld";
        }
    }
}
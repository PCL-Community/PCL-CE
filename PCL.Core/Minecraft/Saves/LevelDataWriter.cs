using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using fNbt;
using PCL.Core.Logging;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档数据写入器
/// </summary>
public class LevelDataWriter
{
    private readonly string _levelDatPath;
    private readonly NbtFile _nbtFile;
    private readonly NbtCompound _gameLevel;
    
    public LevelDataWriter(string levelDatPath, NbtFile nbtFile)
    {
        _levelDatPath = levelDatPath;
        _nbtFile = nbtFile;
        
        var gameLevel = _nbtFile.RootTag?.Get<NbtCompound>("Data");
        if (gameLevel == null)
        {
            throw new InvalidOperationException("level.dat 中没有 Data 标签");
        }
        _gameLevel = gameLevel;
    }
    
    /// <summary>
    /// 修改命令权限
    /// </summary>
    public void ModifyAllowCommands(int newValue)
    {
        var allowCmdTag = _gameLevel.Get<NbtByte>("allowCommands");
        if (allowCmdTag != null)
        {
            allowCmdTag.Value = (byte)newValue;
        }
    }
    
    /// <summary>
    /// 修改难度设置
    /// </summary>
    public void ModifyDifficulty(int newDifficulty, bool? newLocked = null, bool isHardcore = false, bool isNewFormat = false)
    {
        if (isNewFormat)
            ModifyDifficultyNewFormat(newDifficulty, newLocked, isHardcore);
        else
            ModifyDifficultyOldFormat(newDifficulty, newLocked, isHardcore);
    }
    
    private void ModifyDifficultyNewFormat(int newDifficulty, bool? newLocked, bool isHardcore)
    {
        var difficultySettings = _gameLevel.Get<NbtCompound>("difficulty_settings");
        if (difficultySettings == null)
        {
            difficultySettings = new NbtCompound("difficulty_settings");
            _gameLevel.Add(difficultySettings);
        }
        
        string difficultyStr = newDifficulty switch
        {
            0 => "peaceful",
            1 => "easy",
            2 => "normal",
            3 => "hard",
            _ => "normal"
        };
        
        var difficultyTag = difficultySettings.Get<NbtString>("difficulty");
        if (difficultyTag != null)
            difficultyTag.Value = difficultyStr;
        else
            difficultySettings.Add(new NbtString("difficulty", difficultyStr));
        
        if (!isHardcore && newLocked.HasValue)
        {
            if (difficultySettings.Contains("locked"))
            {
                var lockedTag = difficultySettings.Get<NbtByte>("locked");
                if (lockedTag != null)
                {
                    lockedTag.Value = (byte)(newLocked.Value ? 1 : 0);
                }
            }
            else if (newLocked.Value)
            {
                difficultySettings.Add(new NbtByte("locked", 1));
            }
            else if (!newLocked.Value && difficultySettings.Contains("locked"))
            {
                // 如果设置为未锁定，将 locked 设为 0
                var lockedTag = difficultySettings.Get<NbtByte>("locked");
                if (lockedTag != null)
                {
                    lockedTag.Value = 0;
                }
            }
        }
    }
    
    private void ModifyDifficultyOldFormat(int newDifficulty, bool? newLocked, bool isHardcore)
    {
        var difficultyTag = _gameLevel.Get<NbtByte>("Difficulty");
        if (difficultyTag != null)
            difficultyTag.Value = (byte)newDifficulty;
        
        if (!isHardcore && newLocked.HasValue)
        {
            if (_gameLevel.Contains("DifficultyLocked"))
            {
                var lockedTag = _gameLevel.Get<NbtByte>("DifficultyLocked");
                if (lockedTag != null)
                {
                    lockedTag.Value = (byte)(newLocked.Value ? 1 : 0);
                }
            }
            else if (newLocked.Value)
            {
                _gameLevel.Add(new NbtByte("DifficultyLocked", 1));
            }
        }
    }
    
    /// <summary>
    /// 保存修改 - 保存完整的 NBT 文件
    /// </summary>
    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 直接保存整个 NbtFile，而不是只保存单个标签
            // 确保文件目录存在
            var directory = Path.GetDirectoryName(_levelDatPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 使用 FileStream 直接保存完整的 NBT 文件
            await Task.Run(() =>
            {
                using var fileStream = new FileStream(_levelDatPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                _nbtFile.SaveToStream(fileStream, NbtCompression.GZip);
            }, cancellationToken);
            
            LogWrapper.Info($"NBT 文件成功保存于：{_levelDatPath}");
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, $"保存 level.dat 失败: {_levelDatPath}");
            return false;
        }
    }
}
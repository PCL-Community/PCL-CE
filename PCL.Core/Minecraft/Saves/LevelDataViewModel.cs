using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using fNbt;
using PCL.Core.Minecraft.Saves.Models;
using PCL.Core.Minecraft.Saves.Services;
using PCL.Core.UI;

namespace PCL.Core.Minecraft.Saves;

public class LevelDataViewModel : INotifyPropertyChanged
{
    private readonly LevelDataService _service = new();
    private LevelDataLoadResult? _loadResult;
    private string _saveDatPath = string.Empty;
    private bool _isLoading;
    private bool _hasData;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? OnDataChanged; // 数据变化时通知 UI 刷新

    // 基础信息属性
    public string LevelName { get; private set; } = string.Empty;
    public string VersionDisplay { get; private set; } = string.Empty;
    public string Seed { get; private set; } = string.Empty;
    public string SpawnPoint { get; private set; } = string.Empty;
    public string GameType { get; private set; } = string.Empty;
    public string PlayTime { get; private set; } = string.Empty;
    public string LastPlayed { get; private set; } = string.Empty;
    
    // 设置项属性
    public bool HasAllowCommands { get; private set; }
    public int AllowCommands { get; private set; }
    public bool HasDifficulty { get; private set; }
    public string DifficultyDisplay { get; private set; } = string.Empty;
    public bool IsDifficultyLocked { get; private set; }
    public bool IsHardcore { get; private set; }
    
    // UI 状态属性
    public bool IsLoading { get => _isLoading; private set => SetField(ref _isLoading, value); }
    public bool HasData { get => _hasData; private set => SetField(ref _hasData, value); }
    public bool ShowDatapackButton { get; private set; }
    public string? VersionHint { get; private set; }
    public bool ShowVersionHint => !string.IsNullOrEmpty(VersionHint);
    public bool ShowSettings => HasAllowCommands || HasDifficulty;
    public bool ShowDifficultyLock => !IsHardcore;

    // 控件数据源
    public List<ComboItem> AllowCommandsOptions => new()
    {
        new ComboItem(0, "不允许"),
        new ComboItem(1, "允许")
    };
    
    public List<ComboItem> DifficultyOptions => new()
    {
        new ComboItem(0, "和平"),
        new ComboItem(1, "简单"),
        new ComboItem(2, "普通"),
        new ComboItem(3, "困难")
    };

    public async Task LoadAsync(string saveDatPath)
    {
        if (IsLoading) return;
        
        IsLoading = true;
        _saveDatPath = saveDatPath;
        
        try
        {
            if (!File.Exists(_saveDatPath))
            {
                HintWrapper.Show("未找到 level.dat 文件，可能存档已损坏", HintTheme.Error);
                HasData = false;
                return;
            }
            
            _loadResult = await _service.LoadAsync(_saveDatPath);
            if (_loadResult == null) 
                throw new Exception("无法解析存档数据");
            
            UpdateInfo();
            HasData = true;
            OnDataChanged?.Invoke();
        }
        catch (Exception ex)
        {
            HintWrapper.Show($"获取存档信息失败：{ex.Message}", HintTheme.Error);
            HasData = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> SaveAllowCommandsAsync(int newValue)
    {
        if (_loadResult == null) return false;
        
        try
        {
            var writer = _service.GetWriter(_loadResult.NbtFile);
            var gameLevel = _loadResult.NbtFile.RootTag?.Get<NbtCompound>("Data");
            writer.ModifyAllowCommands(gameLevel!, newValue);
            
            if (await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile))
            {
                AllowCommands = newValue;
                HintWrapper.Show("作弊设置修改成功", HintTheme.Success);
                return true;
            }
            else
            {
                HintWrapper.Show("作弊设置修改失败", HintTheme.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            HintWrapper.Show($"作弊设置修改失败：{ex.Message}", HintTheme.Error);
            return false;
        }
    }

    public async Task<bool> SaveDifficultyAsync(int newDifficulty, bool newLocked)
    {
        if (_loadResult == null) return false;
        
        try
        {
            var writer = _service.GetWriter(_loadResult.NbtFile);
            var gameLevel = _loadResult.NbtFile.RootTag?.Get<NbtCompound>("Data");
            writer.ModifyDifficulty(gameLevel!, newDifficulty, newLocked);
            
            if (await _service.SaveAsync(_saveDatPath, _loadResult.NbtFile))
            {
                DifficultyDisplay = newDifficulty switch
                {
                    0 => "和平",
                    1 => "简单",
                    2 => "普通",
                    3 => "困难",
                    _ => DifficultyDisplay
                };
                IsDifficultyLocked = newLocked;
                OnPropertyChanged(nameof(DifficultyDisplay));
                OnPropertyChanged(nameof(ShowDifficultyLock));
                HintWrapper.Show("难度设置修改成功", HintTheme.Success);
                return true;
            }
            else
            {
                HintWrapper.Show("难度设置修改失败", HintTheme.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            HintWrapper.Show($"难度设置修改失败：{ex.Message}", HintTheme.Error);
            return false;
        }
    }

    public void CopySeedToClipboard()
    {
        try
        {
            System.Windows.Clipboard.SetText(Seed);
            HintWrapper.Show("已复制到剪贴板", HintTheme.Success);
        }
        catch (Exception ex)
        {
            HintWrapper.Show($"复制失败：{ex.Message}", HintTheme.Error);
        }
    }

    public void OpenChunkbase()
    {
        var url = ChunkbaseHelper.BuildUrl(Seed, _loadResult?.Info.VersionName);
        if (url == null)
        {
            var versionName = _loadResult?.Info.VersionName;
            if (versionName == null)
                HintWrapper.Show("无法确定存档版本", HintTheme.Error);
            else
                HintWrapper.Show($"当前存档版本 '{versionName}' 可能是预览版，Chunkbase 不支持查看此类版本的地图", HintTheme.Error);
            return;
        }
        
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            HintWrapper.Show($"打开链接失败：{ex.Message}", HintTheme.Error);
        }
    }

    private void UpdateInfo()
    {
        var info = _loadResult!.Info;
        
        LevelName = info.LevelName;
        Seed = info.Seed;
        SpawnPoint = info.SpawnPoint;
        GameType = info.GameType;
        PlayTime = SavesPlayTime.FormatPlayTime(info.PlayTime);
        LastPlayed = info.LastPlayed.ToString("yyyy-MM-dd HH:mm:ss");
        
        VersionDisplay = info.VersionName ?? string.Empty;
        if (info.VersionId.HasValue && !string.IsNullOrEmpty(VersionDisplay))
            VersionDisplay = $"{VersionDisplay} ({info.VersionId.Value})";
        
        HasAllowCommands = info.HasAllowCommands;
        AllowCommands = info.AllowCommands ?? 0;
        
        HasDifficulty = info.HasDifficulty;
        DifficultyDisplay = info.DifficultyDisplay;
        IsDifficultyLocked = info.IsDifficultyLocked;
        IsHardcore = info.IsHardcore;
        
        ShowDatapackButton = info.DataVersion.HasValue && info.DataVersion.Value >= 1444;
        VersionHint = GetVersionHint(info);
        
        OnPropertyChanged(nameof(LevelName));
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(Seed));
        OnPropertyChanged(nameof(SpawnPoint));
        OnPropertyChanged(nameof(GameType));
        OnPropertyChanged(nameof(PlayTime));
        OnPropertyChanged(nameof(LastPlayed));
        OnPropertyChanged(nameof(HasAllowCommands));
        OnPropertyChanged(nameof(AllowCommands));
        OnPropertyChanged(nameof(HasDifficulty));
        OnPropertyChanged(nameof(DifficultyDisplay));
        OnPropertyChanged(nameof(IsDifficultyLocked));
        OnPropertyChanged(nameof(ShowDatapackButton));
        OnPropertyChanged(nameof(VersionHint));
        OnPropertyChanged(nameof(ShowVersionHint));
        OnPropertyChanged(nameof(ShowSettings));
        OnPropertyChanged(nameof(ShowDifficultyLock));
    }

    private string? GetVersionHint(LevelDataInfo info)
    {
        if (info.HasDataVersion) return null;
        
        if (info.HasDifficulty)
            return "1.9 以下的版本无法获取存档版本";
        if (info.HasAllowCommands)
            return "1.8 以下的版本无法获取存档版本和游戏难度";
        return "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊";
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public class ComboItem
{
    public int Value { get; }
    public string Display { get; }
    
    public ComboItem(int value, string display)
    {
        Value = value;
        Display = display;
    }
}
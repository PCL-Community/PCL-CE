using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>存档信息页的视图模型：加载存档、生成信息行、维护设置项与版本提示。</summary>
public partial class PageInstanceSavesInfoViewModel : ViewModelBase
{
    private static readonly SaveManager SaveManager = new();

    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private CancellationTokenSource? _cts;

    private string? _versionHint13Key;
    private string? _versionHint18Key;
    private string? _versionHint19Key;

    [ObservableProperty] public partial string LevelName { get; set; } = string.Empty;

    [ObservableProperty] public partial int? VersionId { get; set; }

    /// <summary>版本显示文本，随存档数据与语言变化。</summary>
    public string VersionDisplay =>
        Save is null
            ? string.Empty
            : Save.VersionName is null
                ? Save.VersionId?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed")
                : $"{Save.VersionName} ({Save.VersionId?.ToString() ?? "?"})";

    /// <summary>游玩时间文本，随存档数据与语言变化。</summary>
    public string PlayTimeText =>
        Save is null
            ? string.Empty
            : Lang.TimeSpan(Save.PlayTime, 3, false, TimeUnit.Day, TimeUnit.Second);

    /// <summary>游戏模式文本，随存档数据与语言变化。</summary>
    public string GameModeText => Save is null ? string.Empty : GameModeName(Save);

    /// <summary>难度文本，随存档数据与语言变化。</summary>
    public string DifficultyText =>
        Save is null
            ? string.Empty
            : Save.Difficulty is null
                ? Lang.Text("Instance.Saves.Info.GetFailed")
                : DifficultyName(Save.Difficulty.Value);

    [ObservableProperty] public partial bool IsHardcore { get; set; }

    [ObservableProperty] public partial bool IsDifficultyLocked { get; set; }

    [ObservableProperty] public partial bool ShowLockDifficulty { get; set; } = true;

    [ObservableProperty] public partial bool HasAllowCommandsSetting { get; set; }

    [ObservableProperty] public partial bool HasDifficultySetting { get; set; }

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool ShowSettings { get; set; }

    [ObservableProperty] public partial bool ShowVersionHint13 { get; set; }

    [ObservableProperty] public partial bool ShowVersionHint18 { get; set; }

    [ObservableProperty] public partial bool ShowVersionHint19 { get; set; }

    /// <summary>1.3 版本提示文本，随语言变化。</summary>
    public string VersionHint13Text =>
        _versionHint13Key is null ? string.Empty : Lang.Text(_versionHint13Key);

    /// <summary>1.8 版本提示文本，随语言变化。</summary>
    public string VersionHint18Text =>
        _versionHint18Key is null ? string.Empty : Lang.Text(_versionHint18Key);

    /// <summary>1.9 版本提示文本，随语言变化。</summary>
    public string VersionHint19Text =>
        _versionHint19Key is null ? string.Empty : Lang.Text(_versionHint19Key);

    [ObservableProperty] public partial int AllowCommandsIndex { get; set; }

    [ObservableProperty] public partial int DifficultyIndex { get; set; } = -1;

    [ObservableProperty] public partial bool IsApplying { get; set; }

    public ObservableCollection<SaveInfoRow> Rows { get; } = [];

    public string? FolderPath { get; private set; }

    /// <summary>页面标题：存档名或读取失败提示，随语言变化。</summary>
    public string TitleDisplay =>
        string.IsNullOrEmpty(LevelName)
            ? Lang.Text("Instance.Saves.Info.GetFailed")
            : LevelName;

    public SaveInfo? Save { get; private set; }

    public bool SupportsAllowCommands =>
        Save is not null
        && (Save.VersionName is not null
            || Save.VersionId is not null
            || Save.Difficulty.HasValue);

    public bool SupportsDifficulty =>
        Save?.VersionName is null
            ? Save?.VersionId is not null
            : McVersionComparer.CompareVersion(Save.VersionName, "1.8") >= 0;

    partial void OnLevelNameChanged(string value)
    {
        OnPropertyChanged(nameof(TitleDisplay));
    }

    /// <summary>加载指定存档；为 null 或目录不存在时显示读取失败。</summary>
    public async Task LoadAsync(string? folder)
    {
        if (_cts is not null)
            await _cts.CancelAsync();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        FolderPath = folder;
        Reset();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            LevelName = string.Empty;
            ShowContent = false;
            return;
        }

        State = AsyncState.Loading;
        Error = null;
        try
        {
            var save = await SaveManager.LoadSaveAsync(folder, token);
            if (token.IsCancellationRequested)
                return;
            ApplySave(save);
            State = AsyncState.Ready;
        }
        catch (OperationCanceledException)
        {
            State = AsyncState.Idle;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = AsyncState.Failed;
            Reset();
            LevelName = string.Empty;
            ShowContent = false;
            ShowSettings = false;
            LauncherCommon.Log(
                ex,
                Lang.Text("Instance.Saves.Info.Error.LoadFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Info.Error.LoadFailed"));
            HintService.Show(Hints.Error(Lang.Text("Instance.Saves.Info.Error.LoadFailed")));
        }
    }

    /// <summary>修改作弊命令开关；返回是否实际写入。</summary>
    public async Task<bool> ApplyAllowCommandsAsync(int index)
    {
        if (Save is null
            || string.IsNullOrEmpty(FolderPath)
            || IsApplying
            || !SupportsAllowCommands)
            return false;
        var allowCommands = index == 1;
        if (allowCommands == Save.AllowCommands)
            return false;

        return await ApplyAsync(
            new SaveChanges
            {
                AllowCommands = new Editable<bool>(allowCommands)
            },
            "Instance.Saves.Info.Modify.CheatSuccess",
            "Instance.Saves.Info.Modify.CheatFailed");
    }

    /// <summary>修改难度与难度锁定；返回是否实际写入。</summary>
    public async Task<bool> ApplyDifficultyAsync(int difficultyIndex, bool lockDifficulty)
    {
        if (Save is null || string.IsNullOrEmpty(FolderPath) || IsApplying || !SupportsDifficulty)
            return false;
        if (difficultyIndex is < 0 or > 3)
            return false;

        var difficulty = (Difficulty)difficultyIndex;
        var locked = !Save.IsHardcore && lockDifficulty;
        if (difficulty == Save.Difficulty && locked == Save.IsDifficultyLocked)
            return false;

        return await ApplyAsync(
            new SaveChanges
            {
                Difficulty = new Editable<Difficulty>(difficulty),
                LockDifficulty = new Editable<bool>(locked)
            },
            "Instance.Saves.Info.Modify.DifficultySuccess",
            "Instance.Saves.Info.Modify.DifficultyFailed");
    }

    public static string GameModeName(SaveInfo save)
    {
        if (save.IsHardcore)
            return Lang.Text("Instance.Saves.Info.GameMode.Hardcore");
        return save.GameMode switch
        {
            GameMode.Survival => Lang.Text("Instance.Saves.Info.GameMode.Survival"),
            GameMode.Creative => Lang.Text("Instance.Saves.Info.GameMode.Creative"),
            GameMode.Adventure => Lang.Text("Instance.Saves.Info.GameMode.Adventure"),
            GameMode.Spectator => Lang.Text("Instance.Saves.Info.GameMode.Spectator"),
            _ => Lang.Text("Instance.Saves.Info.GetFailed")
        };
    }

    public static string DifficultyName(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Peaceful => Lang.Text("Instance.Saves.Info.Difficulty.Peaceful"),
            Difficulty.Easy => Lang.Text("Instance.Saves.Info.Difficulty.Easy"),
            Difficulty.Normal => Lang.Text("Instance.Saves.Info.Difficulty.Normal"),
            Difficulty.Hard => Lang.Text("Instance.Saves.Info.Difficulty.Hard"),
            _ => Lang.Text("Instance.Saves.Info.GetFailed")
        };
    }

    /// <summary>按 WPF 规则生成 Chunkbase 链接：1.21+ 保留补丁版本，其余只取主次版本； 快照、预发布版与未知版本返回 null 并给出提示。</summary>
    public string? BuildChunkbaseUrl(long? seed)
    {
        if (seed is null || Save is null)
            return null;
        var version = Save.VersionName;
        if (string.IsNullOrEmpty(version))
        {
            HintService.Show(
                Hints.Info(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion")));
            return null;
        }

        var match = Regex.Match(version, @"^(?<major>\d+)\.(?<minor>\d+)");
        if (!match.Success || version.Any(char.IsLetter))
        {
            HintService.Show(
                Hints.Info(Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", version)));
            return null;
        }

        var usedVersion = version.StartsWith("1.21", StringComparison.Ordinal)
            ? version.Replace('.', '_')
            : version.Contains('.')
                ? string.Join("_", version.Split('.').Take(2))
                : version.Replace('.', '_');
        return $"https://www.chunkbase.com/apps/seed-map#seed={seed}"
            + $"&platform=java_{usedVersion}&dimension=overworld";
    }

    public void OpenChunkbase(long? seed)
    {
        try
        {
            var url = BuildChunkbaseUrl(seed);
            if (url is not null)
                Basics.OpenPath(url);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.Info.Error.ChunkbaseFailed", ex);
        }
    }

    /// <summary>复制种子文本（由页面提供剪贴板）。</summary>
    public static string? GetSeedCopyText(long? seed)
    {
        return seed?.ToString();
    }

    private async Task<bool> ApplyAsync(SaveChanges changes, string successKey, string failureKey)
    {
        var folder = FolderPath;
        if (folder is null)
            return false;

        IsApplying = true;
        try
        {
            await WriteLock.WaitAsync();
            try
            {
                var applied = await SaveManager.ApplyChangesAsync(folder, changes);
                if (!applied)
                    throw new InvalidOperationException();

                var save = await SaveManager.LoadSaveAsync(folder);
                ApplySave(save);
            }
            finally
            {
                WriteLock.Release();
            }

            HintService.Show(Hints.Success(Lang.Text(successKey)));
            return true;
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(Lang.Text(failureKey)));
            LauncherCommon.Log(ex, Lang.Text(failureKey), LauncherLogLevel.Feedback);
            return false;
        }
        finally
        {
            IsApplying = false;
        }
    }

    private void ApplySave(SaveInfo save)
    {
        Save = save;
        LevelName = save.LevelName;
        VersionId = save.VersionId;
        IsHardcore = save.IsHardcore;
        IsDifficultyLocked = save.IsDifficultyLocked;
        AllowCommandsIndex = save.AllowCommands ? 1 : 0;
        DifficultyIndex = save.Difficulty.HasValue ? (int)save.Difficulty.Value : -1;
        HasAllowCommandsSetting = SupportsAllowCommands;
        HasDifficultySetting = save.Difficulty.HasValue && SupportsDifficulty;
        ShowLockDifficulty = HasDifficultySetting && !save.IsHardcore;
        ShowSettings = HasAllowCommandsSetting || HasDifficultySetting;
        ShowContent = true;

        PageInstanceSavesLeft.ShowDatapackTab =
            save.VersionId is not null && save.VersionId >= DataVersionBoundaries._17w47a;
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(GameModeText));
        OnPropertyChanged(nameof(DifficultyText));
        BuildRows(save);
        BuildVersionHints(save);
    }

    private void BuildRows(SaveInfo save)
    {
        Rows.Clear();
        if (save.VersionName is not null || save.VersionId is not null)
            AddRow("Instance.Saves.Info.Version", () => VersionDisplay);
        AddRow("Instance.Saves.Info.LevelName", () => save.LevelName);
        AddRow(
            "Instance.Saves.Info.Seed",
            () => save.Seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed"),
            save.Seed,
            true);
        AddRow("Instance.Saves.Info.GameMode", () => GameModeText);
        AddRow("Instance.Saves.Info.Hardness", () => DifficultyText);
        if (save.Difficulty.HasValue)
            AddRow(
                "Instance.Saves.Info.LockDifficulty",
                () => save.IsDifficultyLocked
                    ? Lang.Text("Common.Option.Yes")
                    : Lang.Text("Common.Option.No"));
        AddRow("Instance.Saves.Info.PlayTime", () => PlayTimeText);
        AddRow(
            "Instance.Saves.Info.LastPlayed",
            () => Lang.Date(save.LastPlayedUtc.ToLocalTime(), "g"));
        if (save.Spawn is { } spawn)
            AddRow(
                "Instance.Saves.Info.SpawnPoint",
                () => $"{spawn.X:0}, {spawn.Y:0}, {spawn.Z:0}");
    }

    private void BuildVersionHints(SaveInfo save)
    {
        ShowVersionHint13 = false;
        ShowVersionHint18 = false;
        ShowVersionHint19 = false;
        _versionHint13Key = null;
        _versionHint18Key = null;
        _versionHint19Key = null;

        if (save.VersionName is null)
        {
            if (save.VersionId is null)
            {
                _versionHint13Key = "Instance.Saves.Info.VersionHint.1_3";
                ShowVersionHint13 = true;
            }
            else if (save.Difficulty.HasValue)
            {
                _versionHint19Key = "Instance.Saves.Info.VersionHint.1_9";
                ShowVersionHint19 = true;
            }
            else if (save.AllowCommands)
            {
                _versionHint18Key = "Instance.Saves.Info.VersionHint.1_8";
                ShowVersionHint18 = true;
            }
            else
            {
                _versionHint13Key = "Instance.Saves.Info.VersionHint.1_3";
                ShowVersionHint13 = true;
            }

            OnVersionHintTextsChanged();
            return;
        }

        if (McVersionComparer.CompareVersion(save.VersionName, "1.3") < 0)
        {
            _versionHint13Key = "Instance.Saves.Info.VersionHint.1_3";
            _versionHint18Key = "Instance.Saves.Info.VersionHint.1_8";
            _versionHint19Key = "Instance.Saves.Info.VersionHint.1_9";
            ShowVersionHint13 = true;
            ShowVersionHint18 = true;
            ShowVersionHint19 = true;
        }
        else if (McVersionComparer.CompareVersion(save.VersionName, "1.8") < 0)
        {
            _versionHint18Key = "Instance.Saves.Info.VersionHint.1_8";
            _versionHint19Key = "Instance.Saves.Info.VersionHint.1_9";
            ShowVersionHint18 = true;
            ShowVersionHint19 = true;
        }
        else if (McVersionComparer.CompareVersion(save.VersionName, "1.9") < 0)
        {
            _versionHint19Key = "Instance.Saves.Info.VersionHint.1_9";
            ShowVersionHint19 = true;
        }

        OnVersionHintTextsChanged();
    }

    private void OnVersionHintTextsChanged()
    {
        OnPropertyChanged(nameof(VersionHint13Text));
        OnPropertyChanged(nameof(VersionHint18Text));
        OnPropertyChanged(nameof(VersionHint19Text));
    }

    private void AddRow(
        string headKey,
        Func<string> content,
        long? seed = null,
        bool isSeed = false)
    {
        Rows.Add(new SaveInfoRow(headKey, content, seed, isSeed));
    }

    private void Reset()
    {
        Save = null;
        Rows.Clear();
        LevelName = string.Empty;
        VersionId = null;
        IsHardcore = false;
        IsDifficultyLocked = false;
        ShowLockDifficulty = true;
        HasAllowCommandsSetting = false;
        HasDifficultySetting = false;
        ShowSettings = false;
        ShowContent = false;
        ShowVersionHint13 = false;
        ShowVersionHint18 = false;
        ShowVersionHint19 = false;
        _versionHint13Key = null;
        _versionHint18Key = null;
        _versionHint19Key = null;
        OnVersionHintTextsChanged();
        OnPropertyChanged(nameof(VersionDisplay));
        OnPropertyChanged(nameof(PlayTimeText));
        OnPropertyChanged(nameof(GameModeText));
        OnPropertyChanged(nameof(DifficultyText));
        AllowCommandsIndex = 0;
        DifficultyIndex = -1;
    }
}

/// <summary>存档信息表中的一行。</summary>
public sealed class SaveInfoRow(
    string headKey,
    Func<string> content,
    long? seed,
    bool isSeed)
    : LocalizedObservableObject
{
    public string Head => Lang.Text(headKey);

    public string Content => content();

    public long? Seed { get; } = seed;

    public bool IsSeed { get; } = isSeed;

    public bool CanCopySeed { get; } = isSeed && seed is not null;
}
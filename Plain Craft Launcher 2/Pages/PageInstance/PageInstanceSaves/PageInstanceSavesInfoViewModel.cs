using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>存档信息页的视图模型。</summary>
public partial class PageInstanceSavesInfoViewModel : ViewModelBase
{
    private static readonly SaveManager SaveManager = new();

    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    [ObservableProperty] private bool _allowCommands;

    [ObservableProperty] private int _difficultyValue;

    [ObservableProperty] private bool _hasAllowCommandsSetting;

    [ObservableProperty] private bool _hasDifficultySetting;

    [ObservableProperty] private bool _isDifficultyLocked;

    [ObservableProperty] private bool _isHardcore;

    [ObservableProperty] private bool _showContent = true;

    [ObservableProperty] private bool _showDatapackTab;

    [ObservableProperty] private bool _showSettings;

    [ObservableProperty] private bool _showVersionHint13;

    [ObservableProperty] private bool _showVersionHint18;

    [ObservableProperty] private bool _showVersionHint19;

    [ObservableProperty] private string _versionHint13Text = "";

    [ObservableProperty] private string _versionHint18Text = "";

    [ObservableProperty] private string _versionHint19Text = "";

    private CancellationTokenSource? _cts;

    public ObservableCollection<SaveInfoRow> Rows { get; } = [];

    public IReadOnlyList<SaveComboOption> AllowCommandsOptions =>
    [
        new(0, Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed")),
        new(1, Lang.Text("Instance.Saves.Info.AllowCommands.Allowed"))
    ];

    public IReadOnlyList<SaveComboOption> DifficultyOptions =>
    [
        new(0, Lang.Text("Instance.Saves.Info.Difficulty.Peaceful")),
        new(1, Lang.Text("Instance.Saves.Info.Difficulty.Easy")),
        new(2, Lang.Text("Instance.Saves.Info.Difficulty.Normal")),
        new(3, Lang.Text("Instance.Saves.Info.Difficulty.Hard"))
    ];

    public async Task LoadAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        Reset();
        try
        {            var save = await SaveManager.LoadSaveAsync(PageInstanceSavesLeft.currentSave, ct);

            ShowDatapackTab = save.VersionId is not null and >= DataVersionBoundaries._17w47a;

            if (save.VersionName is null)
            {
                if (save.Difficulty.HasValue)
                {
                    VersionHint19Text = Lang.Text("Instance.Saves.Info.VersionHint.1_9");
                    ShowVersionHint19 = true;
                }
                else if (save.AllowCommands)
                {
                    VersionHint18Text = Lang.Text("Instance.Saves.Info.VersionHint.1_8");
                    ShowVersionHint18 = true;
                }
                else
                {
                    VersionHint13Text = Lang.Text("Instance.Saves.Info.VersionHint.1_3");
                    ShowVersionHint13 = true;
                }
            }

            if (save.VersionName is not null)
                AddRow(Lang.Text("Instance.Saves.Info.Version"), $"{save.VersionName} ({save.VersionId})");

            AddRow(Lang.Text("Instance.Saves.Info.LevelName"), save.LevelName);
            AddRow(Lang.Text("Instance.Saves.Info.Seed"),
                save.Seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed"),
                isSeed: true, versionName: save.VersionName);
            AddRow(Lang.Text("Instance.Saves.Info.LastPlayed"), Lang.Date(save.LastPlayedUtc.ToLocalTime(), "g"));

            if (save.Spawn.HasValue)
            {
                var s = save.Spawn.Value;
                AddRow(Lang.Text("Instance.Saves.Info.SpawnPoint"), $"{s.X:F0} / {s.Y:F0} / {s.Z:F0}");
            }

            AddRow(Lang.Text("Instance.Saves.Info.GameMode"), GameModeName(save.GameMode));
            AddRow(Lang.Text("Instance.Saves.Info.PlayTime"),
                Lang.TimeSpan(save.PlayTime, 3, false, TimeUnit.Day, TimeUnit.Second));

            HasAllowCommandsSetting = save.VersionName is not null || save.Difficulty.HasValue;
            AllowCommands = save.AllowCommands;
            HasDifficultySetting = save.Difficulty.HasValue;
            IsHardcore = save.IsHardcore;
            IsDifficultyLocked = save.IsDifficultyLocked;
            DifficultyValue = save.Difficulty.HasValue ? (int)save.Difficulty.Value : 0;

            ShowContent = true;
            ShowSettings = HasAllowCommandsSetting || HasDifficultySetting;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.Info.Error.LoadFailed"), Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Info.Error.LoadFailed"));
            Reset();
            ShowContent = false;
        }
    }

    public async Task ApplyAllowCommandsAsync(string folder, int value)
    {
        var applied = await RunAsync(ct => Task.Run(async () =>
        {
            try
            {
                await WriteLock.WaitAsync(ct);
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        AllowCommands = new Editable<bool>(value == 1)
                    });
                }
                finally
                {
                    WriteLock.Release();
                }

                return true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.Info.Modify.CheatFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Modify.CheatFailed"));
                return false;
            }
        }, ct));

        if (applied is not true)
            return;
        HintService.Hint(Lang.Text("Instance.Saves.Info.Modify.CheatSuccess"), HintType.Success);
    }

    public async Task ApplyDifficultyAsync(string folder, int difficulty, bool isHardcore, bool lockChecked)
    {
        var applied = await RunAsync(ct => Task.Run(async () =>
        {
            try
            {
                await WriteLock.WaitAsync(ct);
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        Difficulty = new Editable<Difficulty>((Difficulty)difficulty),
                        LockDifficulty = new Editable<bool>(!isHardcore && lockChecked)
                    });
                }
                finally
                {
                    WriteLock.Release();
                }

                return true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"));
                return false;
            }
        }, ct));

        if (applied is not true)
            return;
        HintService.Hint(Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess"), HintType.Success);
    }

    public static string GameModeName(GameMode mode)
    {
        return mode switch
        {
            GameMode.Hardcore => Lang.Text("Instance.Saves.Info.GameMode.Hardcore"),
            GameMode.Creative => Lang.Text("Instance.Saves.Info.GameMode.Creative"),
            GameMode.Adventure => Lang.Text("Instance.Saves.Info.GameMode.Adventure"),
            GameMode.Spectator => Lang.Text("Instance.Saves.Info.GameMode.Spectator"),
            _ => Lang.Text("Instance.Saves.Info.GameMode.Survival")
        };
    }

    public static void OpenChunkbase(string seed, string? versionName)
    {
        try
        {
            if (versionName is null)
            {
                Common.Log(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion"));
                return;
            }

            if (versionName.Any(char.IsLetter))
            {
                Common.Log(Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName),
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName));
                return;
            }

            var usedVersion = versionName.StartsWith("1.21")
                ? versionName.Replace(".", "_")
                : versionName.Contains('.')
                    ? string.Join("_", versionName.Split('.').Take(2))
                    : versionName.Replace(".", "_");
            Common.OpenWebsite(
                $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{usedVersion}&dimension=overworld");
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.Info.Error.ChunkbaseFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.Info.Error.ChunkbaseFailed"));
        }
    }

    private void AddRow(string head, string content, bool isSeed = false, string? versionName = null)
    {
        Rows.Add(new SaveInfoRow(head, content, isSeed, versionName));
    }

    private void Reset()
    {
        Rows.Clear();
        ShowVersionHint19 = false;
        ShowVersionHint18 = false;
        ShowVersionHint13 = false;
        ShowSettings = false;
        HasAllowCommandsSetting = false;
        HasDifficultySetting = false;
        AllowCommands = false;
        IsHardcore = false;
        IsDifficultyLocked = false;
        DifficultyValue = 0;
    }
}

/// <summary>存档信息表中的一行。</summary>
public class SaveInfoRow(string head, string content, bool isSeed, string? versionName)
{
    public string Head { get; } = head;

    public string Content { get; } = content;

    public bool IsSeed { get; } = isSeed;

    public string? VersionName { get; } = versionName;
}

/// <summary>存档设置下拉框中的一个选项。</summary>
public class SaveComboOption(int value, string display)
{
    public int Value { get; } = value;

    public string Display { get; } = display;
}

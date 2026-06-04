using System.Windows;
using System.Windows.Controls;
using Humanizer;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.UI;

namespace PCL;

public partial class PageInstanceSavesInfo : IRefreshable
{
    /// <summary>无状态服务，线程安全，所有实例可共享。</summary>
    private static readonly SaveManager SaveManager = new();

    /// <summary>防并发冲突</summary>
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private CancellationTokenSource? _cts;

    public PageInstanceSavesInfo()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            PanBack.ScrollToHome();
            await RefreshInfoAsync();
        };
    }

    void IRefreshable.Refresh() => Refresh();
    public void Refresh() => RefreshInfoAsync().ContinueWith(
        t => LogWrapper.Warn(t.Exception, "Saves", "刷新存档信息异常"), //only 兜底
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    private async Task RefreshInfoAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            ClearInfoTable();
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            Hintversion1_9.Visibility = Visibility.Collapsed;
            Hintversion1_8.Visibility = Visibility.Collapsed;
            Hintversion1_3.Visibility = Visibility.Collapsed;
            PanSettings.Visibility = Visibility.Collapsed;

            var save = await SaveManager.LoadSaveAsync(PageInstanceSavesLeft.currentSave, ct);

            ModMain.frmInstanceSavesLeft.ItemDatapack.Visibility =
                save.VersionId is null or < DataVersionBoundaries._17w47a ? Visibility.Collapsed : Visibility.Visible;

            if (save.VersionName is null)
            {
                if (save.Difficulty.HasValue)
                    ShowHint(Hintversion1_9, "Instance.Saves.Info.VersionHint.1_9");
                else if (save.AllowCommands)
                    ShowHint(Hintversion1_8, "Instance.Saves.Info.VersionHint.1_8");
                else
                    ShowHint(Hintversion1_3, "Instance.Saves.Info.VersionHint.1_3");
            }
            else
                AddInfoRow(Lang.Text("Instance.Saves.Info.Version"), $"{save.VersionName} ({save.VersionId})");

            AddInfoRow(Lang.Text("Instance.Saves.Info.LevelName"), save.LevelName);
            AddInfoRow(Lang.Text("Instance.Saves.Info.Seed"),
                save.Seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed"),
                isSeed: true, versionName: save.VersionName);
            AddInfoRow(Lang.Text("Instance.Saves.Info.LastPlayed"),
                Lang.Date(save.LastPlayedUtc.ToLocalTime(), "g"));

            if (save.Spawn.HasValue)
            {
                var s = save.Spawn.Value;
                AddInfoRow(Lang.Text("Instance.Saves.Info.SpawnPoint"), $"{s.X:F0} / {s.Y:F0} / {s.Z:F0}");
            }

            AddInfoRow(Lang.Text("Instance.Saves.Info.GameMode"), GameModeName(save.GameMode));

            AddInfoRow(Lang.Text("Instance.Saves.Info.PlayTime"),
                Lang.TimeSpan(save.PlayTime, 3, false, TimeUnit.Day, TimeUnit.Second));

            if (save.VersionName is not null || save.Difficulty.HasValue)
                BuildAllowCommandsSetting(save.AllowCommands);

            if (save.Difficulty.HasValue)
                BuildDifficultySetting(save.IsHardcore, save.IsDifficultyLocked, (int)save.Difficulty.Value);

            PanContent.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Error.LoadFailed"), ModBase.LogLevel.Msgbox);
            PanContent.Visibility = Visibility.Collapsed;
            PanSettings.Visibility = Visibility.Collapsed;
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            Hintversion1_9.Visibility = Visibility.Collapsed;
            Hintversion1_8.Visibility = Visibility.Collapsed;
            Hintversion1_3.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildAllowCommandsSetting(bool allowCommands)
    {
        PanSettings.Visibility = Visibility.Visible;
        var folder = PageInstanceSavesLeft.currentSave;

        var combo = new MyComboBox
        {
            Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            SelectedValuePath = "Value", DisplayMemberPath = "Display",
        };
        combo.Items.Add(new { Value = 0, Display = Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed") });
        combo.Items.Add(new { Value = 1, Display = Lang.Text("Instance.Saves.Info.AllowCommands.Allowed") });
        combo.SelectedValue = allowCommands ? 1 : 0;

        combo.SelectionChanged += async (_, _) =>
        {
            try
            {
                if (combo.SelectedValue is null) return;
                await WriteLock.WaitAsync();
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        AllowCommands = new Editable<bool>((int)combo.SelectedValue == 1),
                    });
                }
                finally { WriteLock.Release(); }
                ModMain.Hint(Lang.Text("Instance.Saves.Info.Modify.CheatSuccess"), ModMain.HintType.Finish);
            }
            catch (Exception ex) { ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Modify.CheatFailed"), ModBase.LogLevel.Hint); }
        };

        AddSettingRow(Lang.Text("Instance.Saves.Info.AllowCommands"), combo);
    }

    private void BuildDifficultySetting(bool isHardcore, bool isLocked, int difficultyValue)
    {
        PanSettings.Visibility = Visibility.Visible;
        var folder = PageInstanceSavesLeft.currentSave;

        var combo = new MyComboBox
        {
            Width = 100d, HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            SelectedValuePath = "Value", DisplayMemberPath = "Display",
        };
        combo.Items.Add(new { Value = 0, Display = Lang.Text("Instance.Saves.Info.Difficulty.Peaceful") });
        combo.Items.Add(new { Value = 1, Display = Lang.Text("Instance.Saves.Info.Difficulty.Easy") });
        combo.Items.Add(new { Value = 2, Display = Lang.Text("Instance.Saves.Info.Difficulty.Normal") });
        combo.Items.Add(new { Value = 3, Display = Lang.Text("Instance.Saves.Info.Difficulty.Hard") });
        combo.SelectedValue = difficultyValue;

        var lockCheckBox = new MyCheckBox
        {
            Text = Lang.Text("Instance.Saves.Info.LockDifficulty"),
            ToolTip = Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10d, 0d, 0d, 0d),
            Checked = isLocked,
            Visibility = isHardcore ? Visibility.Collapsed : Visibility.Visible,
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(combo);
        panel.Children.Add(lockCheckBox);

        async Task ApplyAsync()
        {
            try
            {
                if (combo.SelectedValue is null) return;
                await WriteLock.WaitAsync();
                try
                {
                    await SaveManager.ApplyChangesAsync(folder, new SaveChanges
                    {
                        Difficulty = new Editable<Difficulty>((Difficulty)(int)combo.SelectedValue),
                        LockDifficulty = new Editable<bool>(!isHardcore && lockCheckBox.Checked == true),
                    });
                }
                finally { WriteLock.Release(); }
                ModMain.Hint(Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess"), ModMain.HintType.Finish);
            }
            catch (Exception ex) { ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"), ModBase.LogLevel.Hint); }
        }

        combo.SelectionChanged += async (_, _) => await ApplyAsync();
        lockCheckBox.Change += async (_, _) => await ApplyAsync();

        AddSettingRow(Lang.Text("Instance.Saves.Info.GameDifficultyLabel"), panel);
    }

    private static string GameModeName(GameMode mode) => mode switch
    {
        GameMode.Hardcore => Lang.Text("Instance.Saves.Info.GameMode.Hardcore"),
        GameMode.Creative => Lang.Text("Instance.Saves.Info.GameMode.Creative"),
        GameMode.Adventure => Lang.Text("Instance.Saves.Info.GameMode.Adventure"),
        GameMode.Spectator => Lang.Text("Instance.Saves.Info.GameMode.Spectator"),
        _ => Lang.Text("Instance.Saves.Info.GameMode.Survival"),
    };

    private static void ShowHint(MyHint hint, string langKey)
    {
        hint.Text = Lang.Text(langKey);
        hint.Visibility = Visibility.Visible;
    }

    private void ClearInfoTable()
    {
        PanList.Children.Clear();
        PanList.RowDefinitions.Clear();
    }

    private void AddInfoRow(string head, string content, bool isSeed = false, string? versionName = null)
    {
        var headBlock = new TextBlock { Text = head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        var contentStack = new StackPanel { Orientation = Orientation.Horizontal };

        if (isSeed && content != Lang.Text("Instance.Saves.Info.GetFailed"))
        {
            var seedBtn = new MyTextButton { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) };
            seedBtn.Click += (_, _) =>
            {
                try { ModBase.ClipboardSet(content); }
                catch (Exception ex) { ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Error.ClipboardFailed"), ModBase.LogLevel.Hint); }
            };
            contentStack.Children.Add(seedBtn);

            var chunkbaseBtn = new MyIconButton
            {
                Logo = Icon.IconButtonlink, Width = 22d, Height = 22d,
                ToolTip = Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"),
            };
            chunkbaseBtn.Click += (_, _) => OpenChunkbase(content, versionName);
            contentStack.Children.Add(chunkbaseBtn);
        }
        else
        {
            contentStack.Children.Add(new TextBlock { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) });
        }

        PanList.Children.Add(headBlock);
        PanList.Children.Add(contentStack);
        var rowDef = new RowDefinition();
        PanList.RowDefinitions.Add(rowDef);
        var rowIndex = PanList.RowDefinitions.IndexOf(rowDef);
        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(contentStack, rowIndex);
        Grid.SetColumn(contentStack, 2);
    }

    private void AddSettingRow(string head, UIElement control)
    {
        var rowIndex = PanSettingsList.RowDefinitions.Count;
        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Auto) });

        var headBlock = new TextBlock { Text = head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(control, rowIndex);
        Grid.SetColumn(control, 2);
        PanSettingsList.Children.Add(headBlock);
        PanSettingsList.Children.Add(control);
        PanSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8d, GridUnitType.Pixel) });
    }

    private static void OpenChunkbase(string seed, string? versionName)
    {
        try
        {
            if (versionName is null)
            {
                ModBase.Log(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion"), ModBase.LogLevel.Hint);
                return;
            }

            if (versionName.Any(char.IsLetter))
            {
                ModBase.Log(Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName),
                    ModBase.LogLevel.Hint);
                return;
            }

            var usedVersion = versionName.StartsWith("1.21")
                ? versionName.Replace(".", "_")
                : versionName.Contains('.')
                    ? string.Join("_", versionName.Split('.').Take(2))
                    : versionName.Replace(".", "_");
            ModBase.OpenWebsite(
                $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{usedVersion}&dimension=overworld");
        }
        catch (Exception ex) { ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Error.ChunkbaseFailed"), ModBase.LogLevel.Hint); }
    }
}

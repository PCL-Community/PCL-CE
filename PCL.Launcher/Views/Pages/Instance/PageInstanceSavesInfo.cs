using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档详情页：显示存档名称、版本、种子、游戏模式、难度、游玩时间与出生点，
///     并允许修改作弊命令、难度与难度锁定（通过 <see cref="SaveManager.ApplyChangesAsync" /> 原子写入 level.dat）。
///     种子可点击复制，也可跳转 Chunkbase 预览；低版本存档会提示哪些字段无法获取。
/// </summary>
public class PageInstanceSavesInfo : MyPageRight, IRefreshable
{
    private StackPanel _panMain = null!;
    private readonly SaveManager _saveManager = new();
    private StackPanel _panInfo = null!;
    private TextBlock _titleText = null!;
    private StackPanel _panHints = null!;
    private MyComboBox _comboAllowCommands = null!;
    private MyComboBox _comboDifficulty = null!;
    private MyCheckBox _checkLockDifficulty = null!;
    private string? _saveFolder;
    private SaveInfo? _save;
    private bool _syncing;

    public PageInstanceSavesInfo()
    {
        BuildUi();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _saveFolder = parameter as string;
        if (string.IsNullOrEmpty(_saveFolder))
            _saveFolder = PageInstanceSavesLeft.currentSave;
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    public void Refresh()
    {
        Reload();
    }

    private void BuildUi()
    {
        var panel = new StackPanel { Margin = new Thickness(25d, 25d, 25d, 25d) };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        Child = scroll;

        var infoCard = InstancePageHelper.CreateCard("Instance.Saves.Info.Details.Title", out var infoBody);
        _titleText = new TextBlock { FontSize = 18d, FontWeight = FontWeight.SemiBold };
        infoBody.Children.Add(_titleText);
        _panHints = new StackPanel { Spacing = 6d };
        infoBody.Children.Add(_panHints);
        _panInfo = new StackPanel { Spacing = 4d };
        infoBody.Children.Add(_panInfo);
        panel.Children.Add(infoCard);

        var settingsCard = InstancePageHelper.CreateCard("Instance.Saves.Info.Settings.Title", out var settingsBody);
        settingsBody.Children.Add(
            new MyHint
            {
                Text = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
                Theme = MyHint.Themes.Blue,
                Margin = new Thickness(0d, 0d, 0d, 8d)
            });

        _comboAllowCommands = new MyComboBox { Width = 220d };
        _comboAllowCommands.Items.Add(Lang.Text("Instance.Saves.Info.AllowCommands.Allowed"));
        _comboAllowCommands.Items.Add(Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed"));
        _comboAllowCommands.SelectionChanged += (_, _) => { if (!_syncing) _ = ApplyChangesAsync(); };
        settingsBody.Children.Add(InstancePageHelper.Row(Lang.Text("Instance.Saves.Info.AllowCommands"), _comboAllowCommands));

        _comboDifficulty = new MyComboBox { Width = 220d };
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Peaceful"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Easy"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Normal"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Hard"));
        _comboDifficulty.SelectionChanged += (_, _) => { if (!_syncing) _ = ApplyChangesAsync(); };
        settingsBody.Children.Add(
            InstancePageHelper.Row(Lang.Text("Instance.Saves.Info.GameDifficultyLabel"), _comboDifficulty));

        _checkLockDifficulty = new MyCheckBox { Text = Lang.Text("Instance.Saves.Info.LockDifficulty") };
        ToolTip.SetTip(_checkLockDifficulty, Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"));
        _checkLockDifficulty.Change += (_, _) => { if (!_syncing) _ = ApplyChangesAsync(); };
        settingsBody.Children.Add(_checkLockDifficulty);
        panel.Children.Add(settingsCard);
    }

    public void Reload()
    {
        if (string.IsNullOrEmpty(_saveFolder) || !Directory.Exists(_saveFolder))
        {
            _titleText.Text = Lang.Text("Instance.Saves.Info.GetFailed");
            _panInfo.Children.Clear();
            return;
        }

        _ = LoadAsync(_saveFolder);
    }

    private async Task LoadAsync(string folder)
    {
        try
        {
            _save = await _saveManager.LoadSaveAsync(folder);
            PageInstanceSavesLeft.ShowDatapackTab =
                _save.VersionId is not null && _save.VersionId >= DataVersionBoundaries._17w47a;
            Render();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.Info.Error.LoadFailed", ex);
        }
    }

    private void Render()
    {
        if (_save is null)
            return;
        _syncing = true;
        try
        {
            _titleText.Text = _save.LevelName;
            _panInfo.Children.Clear();

            AddInfoRow("Instance.Saves.Info.Version", _save.VersionName ?? Lang.Text("Instance.Saves.Info.GetFailed"));
            AddSeedRow(_save.Seed);
            AddInfoRow("Instance.Saves.Info.GameMode", FormatGameMode(_save));
            AddInfoRow(
                "Instance.Saves.Info.Hardness",
                _save.Difficulty is null ? Lang.Text("Instance.Saves.Info.GetFailed") : FormatDifficulty(_save.Difficulty.Value));
            AddInfoRow(
                "Instance.Saves.Info.LockDifficulty",
                _save.IsDifficultyLocked
                    ? Lang.Text("Common.Option.Yes")
                    : Lang.Text("Common.Option.No"));
            AddInfoRow(
                "Instance.Saves.Info.PlayTime",
                _save.PlayTime.ToString(@"d\.hh\:mm\:ss"));
            AddInfoRow(
                "Instance.Saves.Info.LastPlayed",
                Lang.Date(_save.LastPlayedUtc.ToLocalTime()));
            AddInfoRow(
                "Instance.Saves.Info.SpawnPoint",
                _save.Spawn is { } spawn
                    ? $"{spawn.X:0}, {spawn.Y:0}, {spawn.Z:0}"
                    : Lang.Text("Instance.Saves.Info.GetFailed"));

            RenderHints();

            _comboAllowCommands.SelectedIndex = _save.AllowCommands ? 0 : 1;
            _comboAllowCommands.IsEnabled = SupportsAllowCommands;
            _comboDifficulty.SelectedIndex = _save.Difficulty switch
            {
                Difficulty.Peaceful => 0,
                Difficulty.Easy => 1,
                Difficulty.Normal => 2,
                Difficulty.Hard => 3,
                _ => -1
            };
            _comboDifficulty.IsEnabled = _save.Difficulty is not null && SupportsDifficulty;
            _checkLockDifficulty.Checked = _save.IsDifficultyLocked;
            _checkLockDifficulty.IsEnabled = _save.Difficulty is not null && SupportsDifficulty;
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool SupportsAllowCommands => _save?.VersionName is null
        ? _save?.VersionId is not null
        : McVersionComparer.CompareVersion(_save.VersionName, "1.9") >= 0;

    private bool SupportsDifficulty => _save?.VersionName is null
        ? _save?.VersionId is not null
        : McVersionComparer.CompareVersion(_save.VersionName, "1.8") >= 0;

    private void RenderHints()
    {
        _panHints.Children.Clear();
        if (_save?.VersionName is not { } version)
        {
            if (_save?.VersionId is null)
                _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_3"));
            return;
        }

        if (McVersionComparer.CompareVersion(version, "1.3") < 0)
        {
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_3"));
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_8"));
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_9"));
        }
        else if (McVersionComparer.CompareVersion(version, "1.8") < 0)
        {
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_8"));
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_9"));
        }
        else if (McVersionComparer.CompareVersion(version, "1.9") < 0)
        {
            _panHints.Children.Add(NewVersionHint("Instance.Saves.Info.VersionHint.1_9"));
        }
    }

    private static MyHint NewVersionHint(string key)
    {
        return new MyHint
        {
            Text = Lang.Text(key),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 2d)
        };
    }

    private void AddInfoRow(string labelKey, string value)
    {
        AddInfoRow(labelKey, value, value);
    }

    private void AddInfoRow(string labelKey, string display, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Height = 26d
        };
        grid.Children.Add(
            new TextBlock
            {
                Text = Lang.Text(labelKey),
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 0d, 25d, 0d)
            });
        var valueBlock = new TextBlock
        {
            Text = display,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        _panInfo.Children.Add(grid);
    }

    private void AddSeedRow(long? seed)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Height = 28d
        };
        grid.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Instance.Saves.Info.Seed"),
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 0d, 25d, 0d)
            });
        var seedButton = new MyButton
        {
            Text = seed?.ToString() ?? Lang.Text("Instance.Saves.Info.GetFailed"),
            MinWidth = 140d,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = seed is not null
        };
        ToolTip.SetTip(seedButton, Lang.Text("Common.Action.Copy"));
        seedButton.Click += (_, _) => _ = CopySeedAsync(seed);
        Grid.SetColumn(seedButton, 1);
        grid.Children.Add(seedButton);
        var chunkbase = new MyIconButton
        {
            SvgIcon = "lucide/external-link",
            LogoScale = 0.9d,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = seed is not null
        };
        ToolTip.SetTip(chunkbase, Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"));
        chunkbase.Click += (_, _) => OpenChunkbase(seed);
        Grid.SetColumn(chunkbase, 2);
        grid.Children.Add(chunkbase);
        _panInfo.Children.Add(grid);
    }

    private async Task CopySeedAsync(long? seed)
    {
        if (seed is null)
            return;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(seed.Value.ToString());
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.Info.Error.ClipboardFailed", ex);
        }
    }

    private void OpenChunkbase(long? seed)
    {
        if (seed is null)
            return;
        var version = _save?.VersionName;
        if (string.IsNullOrEmpty(version))
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Info.Chunkbase.UnknownVersion")));
            return;
        }

        var match = Regex.Match(version, @"^(?<major>\d+)\.(?<minor>\d+)");
        if (!match.Success || version.Contains('w', StringComparison.OrdinalIgnoreCase) || version.Contains("pre", StringComparison.OrdinalIgnoreCase))
        {
            HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Info.Chunkbase.PreviewVersion", version)));
            return;
        }

        var major = match.Groups["major"].Value;
        var minor = match.Groups["minor"].Value;
        try
        {
            var url = $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{major}_{minor}";
            Basics.OpenPath(url);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.Info.Error.ChunkbaseFailed", ex);
        }
    }

    private static string FormatGameMode(SaveInfo save)
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

    private static string FormatDifficulty(Difficulty difficulty)
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

    private async Task ApplyChangesAsync()
    {
        if (_save is null || string.IsNullOrEmpty(_saveFolder) || _syncing)
            return;
        _syncing = true;
        try
        {
            var allowCommands = _comboAllowCommands.SelectedIndex == 0;
            var difficulty = _comboDifficulty.SelectedIndex switch
            {
                0 => Difficulty.Peaceful,
                1 => Difficulty.Easy,
                2 => Difficulty.Normal,
                3 => Difficulty.Hard,
                _ => _save.Difficulty
            };
            var lockDifficulty = _checkLockDifficulty.Checked == true;

            var cheatChanged = SupportsAllowCommands && allowCommands != _save.AllowCommands;
            var difficultyChanged = SupportsDifficulty && difficulty is not null
                                    && (difficulty != _save.Difficulty || lockDifficulty != _save.IsDifficultyLocked);
            if (!cheatChanged && !difficultyChanged)
                return;

            var changes = new SaveChanges();
            if (SupportsAllowCommands)
                changes.AllowCommands = new Editable<bool>(allowCommands);
            if (SupportsDifficulty && difficulty is not null)
            {
                changes.Difficulty = new Editable<Difficulty>(difficulty.Value);
                changes.LockDifficulty = new Editable<bool>(lockDifficulty);
            }
            var applied = await _saveManager.ApplyChangesAsync(_saveFolder, changes);
            if (!applied)
                throw new InvalidOperationException();
            _save = await _saveManager.LoadSaveAsync(_saveFolder);

            if (cheatChanged)
                HintService.Show(Hints.Success(Lang.Text("Instance.Saves.Info.Modify.CheatSuccess")));
            if (difficultyChanged)
                HintService.Show(Hints.Success(Lang.Text("Instance.Saves.Info.Modify.DifficultySuccess")));
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(Lang.Text("Instance.Saves.Info.Modify.CheatFailed")));
            LauncherCommon.Log(ex, "修改存档设置失败", LauncherLogLevel.Feedback);
        }
        finally
        {
            _syncing = false;
            Render();
        }
    }
}

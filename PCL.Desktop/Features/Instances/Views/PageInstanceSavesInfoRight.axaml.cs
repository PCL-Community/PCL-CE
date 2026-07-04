// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Core.Minecraft.Saves;
using PCL.Core.Minecraft.Saves.Editing;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceSavesInfoRight : MyPageRight, IRefreshable
{
    private static readonly SaveManager SaveManager = new();
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private string _saveFolder = string.Empty;

    public PageInstanceSavesInfoRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
    }

    public event EventHandler<string>? StatusMessage;

    public void Refresh() => _ = RefreshInfoAsync();

    public override void Dispose()
    {
        base.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }

    public async Task SetSaveFolderAsync(string saveFolder, CancellationToken cancellationToken = default)
    {
        _saveFolder = saveFolder;
        PanScroll?.ScrollToHome();
        await RefreshInfoAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task RefreshInfoAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken ct = _cts.Token;

        try
        {
            ClearInfoTable();
            ClearSettingsTable();
            HideVersionHints();
            FindPanSettings().IsVisible = false;
            FindPanContent().IsVisible = true;
            AddInfoRow(Text("Instance.Saves.Info.Loading"), string.Empty);

            SaveInfo save = await SaveManager.LoadSaveAsync(_saveFolder, ct).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();

            ClearInfoTable();
            if (save.VersionName is null)
            {
                if (save.Difficulty.HasValue)
                    ShowHint("Hintversion1_9", "Instance.Saves.Info.VersionHint.1_9");
                else if (save.AllowCommands)
                    ShowHint("Hintversion1_8", "Instance.Saves.Info.VersionHint.1_8");
                else
                    ShowHint("Hintversion1_3", "Instance.Saves.Info.VersionHint.1_3");
            }
            else
            {
                AddInfoRow(Text("Instance.Saves.Info.Version"), $"{save.VersionName} ({save.VersionId})");
            }

            AddInfoRow(Text("Instance.Saves.Info.LevelName"), save.LevelName);
            AddInfoRow(
                Text("Instance.Saves.Info.Seed"),
                save.Seed?.ToString(CultureInfo.InvariantCulture) ?? Text("Instance.Saves.Info.GetFailed"),
                isSeed: true,
                versionName: save.VersionName);
            AddInfoRow(
                Text("Instance.Saves.Info.LastPlayed"),
                save.LastPlayedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

            if (save.Spawn.HasValue)
            {
                var spawn = save.Spawn.Value;
                AddInfoRow(Text("Instance.Saves.Info.SpawnPoint"), $"{spawn.X:F0} / {spawn.Y:F0} / {spawn.Z:F0}");
            }

            AddInfoRow(Text("Instance.Saves.Info.GameMode"), GameModeName(save.GameMode));
            AddInfoRow(Text("Instance.Saves.Info.PlayTime"), FormatPlayTime(save.PlayTime));

            if (save.VersionName is not null || save.Difficulty.HasValue)
                BuildAllowCommandsSetting(save.AllowCommands);

            if (save.Difficulty.HasValue)
                BuildDifficultySetting(save.IsHardcore, save.IsDifficultyLocked, (int)save.Difficulty.Value);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ClearInfoTable();
            ClearSettingsTable();
            HideVersionHints();
            FindPanSettings().IsVisible = false;
            AddInfoRow(Text("Instance.Saves.Info.Error.LoadFailed"), ex.Message);
            StatusMessage?.Invoke(this, Text("Instance.Saves.Info.Error.LoadFailed"));
        }
    }

    private void BuildAllowCommandsSetting(bool allowCommands)
    {
        FindPanSettings().IsVisible = true;
        MyComboBox combo = new()
        {
            Width = 100d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ToolTip.SetTip(combo, Text("Instance.Saves.Info.Modify.BeforeSave"));
        ComboChoice[] choices =
        [
            new(0, Text("Instance.Saves.Info.AllowCommands.NotAllowed")),
            new(1, Text("Instance.Saves.Info.AllowCommands.Allowed"))
        ];
        foreach (ComboChoice choice in choices)
            combo.Items.Add(new MyComboBoxItem { Content = choice.Display, Tag = choice });
        combo.SelectedIndex = allowCommands ? 1 : 0;

        combo.SelectionChanged += async (_, _) =>
        {
            if (combo.SelectedItem is not MyComboBoxItem { Tag: ComboChoice choice })
                return;

            await ApplyChangesAsync(
                new SaveChanges { AllowCommands = new Editable<bool>(choice.Value == 1) },
                Text("Instance.Saves.Info.Modify.CheatSuccess"),
                Text("Instance.Saves.Info.Modify.CheatFailed")).ConfigureAwait(true);
        };

        AddSettingRow(Text("Instance.Saves.Info.AllowCommands"), combo);
    }

    private void BuildDifficultySetting(bool isHardcore, bool isLocked, int difficultyValue)
    {
        FindPanSettings().IsVisible = true;
        MyComboBox combo = new()
        {
            Width = 100d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        ToolTip.SetTip(combo, Text("Instance.Saves.Info.Modify.BeforeSave"));

        ComboChoice[] choices =
        [
            new(0, Text("Instance.Saves.Info.Difficulty.Peaceful")),
            new(1, Text("Instance.Saves.Info.Difficulty.Easy")),
            new(2, Text("Instance.Saves.Info.Difficulty.Normal")),
            new(3, Text("Instance.Saves.Info.Difficulty.Hard"))
        ];
        foreach (ComboChoice choice in choices)
            combo.Items.Add(new MyComboBoxItem { Content = choice.Display, Tag = choice });
        combo.SelectedIndex = Math.Clamp(difficultyValue, 0, combo.Items.Count - 1);

        MyCheckBox lockCheckBox = new()
        {
            Text = Text("Instance.Saves.Info.LockDifficulty"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10d, 0d, 0d, 0d),
            Checked = isLocked,
            IsVisible = !isHardcore
        };
        ToolTip.SetTip(lockCheckBox, Text("Instance.Saves.Info.LockDifficulty.ToolTip"));

        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(combo);
        panel.Children.Add(lockCheckBox);

        async Task ApplyAsync()
        {
            if (combo.SelectedItem is not MyComboBoxItem { Tag: ComboChoice choice })
                return;

            await ApplyChangesAsync(
                new SaveChanges
                {
                    Difficulty = new Editable<Difficulty>((Difficulty)choice.Value),
                    LockDifficulty = new Editable<bool>(!isHardcore && lockCheckBox.Checked == true)
                },
                Text("Instance.Saves.Info.Modify.DifficultySuccess"),
                Text("Instance.Saves.Info.Modify.DifficultyFailed")).ConfigureAwait(true);
        }

        combo.SelectionChanged += async (_, _) => await ApplyAsync().ConfigureAwait(true);
        lockCheckBox.Change += async (_, _) => await ApplyAsync().ConfigureAwait(true);

        AddSettingRow(Text("Instance.Saves.Info.GameDifficultyLabel"), panel);
    }

    private async Task ApplyChangesAsync(SaveChanges changes, string successMessage, string failureMessage)
    {
        try
        {
            await WriteLock.WaitAsync().ConfigureAwait(true);
            try
            {
                await SaveManager.ApplyChangesAsync(_saveFolder, changes).ConfigureAwait(true);
            }
            finally
            {
                WriteLock.Release();
            }

            StatusMessage?.Invoke(this, successMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage?.Invoke(this, failureMessage);
        }
    }

    private void AddInfoRow(string head, string content, bool isSeed = false, string? versionName = null)
    {
        Grid panList = FindPanList();
        int rowIndex = panList.RowDefinitions.Count;
        panList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock headBlock = new()
        {
            Text = head,
            Margin = new Thickness(0d, 3d, 0d, 3d)
        };
        StackPanel contentStack = new()
        {
            Orientation = Orientation.Horizontal
        };

        if (isSeed && content != Text("Instance.Saves.Info.GetFailed"))
            AddSeedControls(contentStack, content, versionName);
        else if (!string.IsNullOrEmpty(content))
            contentStack.Children.Add(new TextBlock { Text = content, Margin = new Thickness(0d, 3d, 0d, 3d) });

        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(contentStack, rowIndex);
        Grid.SetColumn(contentStack, 2);
        panList.Children.Add(headBlock);
        panList.Children.Add(contentStack);
    }

    private void AddSeedControls(StackPanel contentStack, string seed, string? versionName)
    {
        MyTextButton seedButton = new()
        {
            Text = seed,
            Margin = new Thickness(0d, 3d, 0d, 3d)
        };
        seedButton.Click += async (_, _) =>
        {
            try
            {
                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                    throw new InvalidOperationException("Clipboard is not available.");

                await clipboard.SetTextAsync(seed).ConfigureAwait(true);
                StatusMessage?.Invoke(this, Text("Instance.Saves.Info.SeedCopied"));
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
            {
                StatusMessage?.Invoke(this, Text("Instance.Saves.Info.Error.ClipboardFailed"));
            }
        };
        contentStack.Children.Add(seedButton);

        MyIconButton chunkbaseButton = new()
        {
            SvgIcon = "lucide/external-link",
            Width = 22d,
            Height = 22d,
            ToolTip = Text("Instance.Saves.Info.Chunkbase.ToolTip")
        };
        chunkbaseButton.Click += (_, _) => OpenChunkbase(seed, versionName);
        contentStack.Children.Add(chunkbaseButton);
    }

    private void AddSettingRow(string head, Control control)
    {
        Grid panSettingsList = FindPanSettingsList();
        int rowIndex = panSettingsList.RowDefinitions.Count;
        panSettingsList.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock headBlock = new()
        {
            Text = head,
            Margin = new Thickness(0d, 3d, 0d, 3d)
        };
        Grid.SetRow(headBlock, rowIndex);
        Grid.SetColumn(headBlock, 0);
        Grid.SetRow(control, rowIndex);
        Grid.SetColumn(control, 2);
        panSettingsList.Children.Add(headBlock);
        panSettingsList.Children.Add(control);
        panSettingsList.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8d, GridUnitType.Pixel) });
    }

    private void OpenChunkbase(string seed, string? versionName)
    {
        try
        {
            if (versionName is null)
            {
                StatusMessage?.Invoke(this, Text("Instance.Saves.Info.Chunkbase.UnknownVersion"));
                return;
            }

            if (versionName.Any(char.IsLetter))
            {
                StatusMessage?.Invoke(this, Text("Instance.Saves.Info.Chunkbase.PreviewVersion", versionName));
                return;
            }

            string usedVersion = versionName.StartsWith("1.21", StringComparison.Ordinal)
                ? versionName.Replace(".", "_", StringComparison.Ordinal)
                : versionName.Contains('.', StringComparison.Ordinal)
                    ? string.Join("_", versionName.Split('.').Take(2))
                    : versionName.Replace(".", "_", StringComparison.Ordinal);
            Process.Start(new ProcessStartInfo(
                $"https://www.chunkbase.com/apps/seed-map#seed={seed}&platform=java_{usedVersion}&dimension=overworld")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.Info.Error.ChunkbaseFailed"));
        }
    }

    private static string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime.TotalDays >= 1d)
            return string.Format(CultureInfo.CurrentCulture, "{0} 天 {1} 小时 {2} 分钟", (int)playTime.TotalDays, playTime.Hours, playTime.Minutes);
        if (playTime.TotalHours >= 1d)
            return string.Format(CultureInfo.CurrentCulture, "{0} 小时 {1} 分钟 {2} 秒", (int)playTime.TotalHours, playTime.Minutes, playTime.Seconds);
        if (playTime.TotalMinutes >= 1d)
            return string.Format(CultureInfo.CurrentCulture, "{0} 分钟 {1} 秒", (int)playTime.TotalMinutes, playTime.Seconds);
        return string.Format(CultureInfo.CurrentCulture, "{0} 秒", Math.Max(0, (int)playTime.TotalSeconds));
    }

    private string GameModeName(GameMode mode) => mode switch
    {
        GameMode.Hardcore => Text("Instance.Saves.Info.GameMode.Hardcore"),
        GameMode.Creative => Text("Instance.Saves.Info.GameMode.Creative"),
        GameMode.Adventure => Text("Instance.Saves.Info.GameMode.Adventure"),
        GameMode.Spectator => Text("Instance.Saves.Info.GameMode.Spectator"),
        _ => Text("Instance.Saves.Info.GameMode.Survival")
    };

    private void ShowHint(string controlName, string langKey)
    {
        if (this.FindControl<MyHint>(controlName) is not { } hint)
            return;

        hint.Text = Text(langKey);
        hint.IsVisible = true;
    }

    private void HideVersionHints()
    {
        foreach (string hintName in new[] { "Hintversion1_9", "Hintversion1_8", "Hintversion1_3" })
        {
            if (this.FindControl<MyHint>(hintName) is { } hint)
                hint.IsVisible = false;
        }
    }

    private void ClearInfoTable()
    {
        Grid panList = FindPanList();
        panList.Children.Clear();
        panList.RowDefinitions.Clear();
    }

    private void ClearSettingsTable()
    {
        Grid panSettingsList = FindPanSettingsList();
        panSettingsList.Children.Clear();
        panSettingsList.RowDefinitions.Clear();
    }

    private string Text(string key, params string[] args)
    {
        string value = TryGetResource(key, null, out object? resource) && resource is string text
            ? text
            : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    private MyCard FindPanContent() =>
        this.FindControl<MyCard>("PanContent") ?? throw new InvalidOperationException("PanContent not found.");

    private MyCard FindPanSettings() =>
        this.FindControl<MyCard>("PanSettings") ?? throw new InvalidOperationException("PanSettings not found.");

    private Grid FindPanList() =>
        this.FindControl<Grid>("PanList") ?? throw new InvalidOperationException("PanList not found.");

    private Grid FindPanSettingsList() =>
        this.FindControl<Grid>("PanSettingsList") ?? throw new InvalidOperationException("PanSettingsList not found.");

    private sealed record ComboChoice(int Value, string Display);
}

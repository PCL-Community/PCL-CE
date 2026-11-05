using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档详情页：显示存档名称、版本、种子、游戏模式、难度、游玩时间与出生点，
///     并允许修改作弊命令、难度与难度锁定（通过存档管理器原子写入 level.dat）。
///     种子可点击复制，也可跳转 Chunkbase 预览；低版本存档会提示哪些字段无法获取。
/// </summary>
public class PageInstanceSavesInfo : MyPageRight, IRefreshable
{
    private MyCheckBox _checkLockDifficulty = null!;
    private MyComboBox _comboAllowCommands = null!;
    private MyComboBox _comboDifficulty = null!;
    private StackPanel _panHints = null!;
    private StackPanel _panInfo = null!;
    private StackPanel _panMain = null!;
    private Grid _rowAllowCommands = null!;
    private Grid _rowDifficulty = null!;
    private string? _saveFolder;
    private MyCard _settingsCard = null!;
    private bool _syncing;
    private TextBlock _titleText = null!;

    public PageInstanceSavesInfo()
    {
        BuildUi();
    }

    public PageInstanceSavesInfoViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _saveFolder = parameter as string;
        if (string.IsNullOrEmpty(_saveFolder))
            _saveFolder = PageInstanceSavesLeft.CurrentSave;
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 25d, 25d, 25d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        Child = scroll;

        var infoCard = InstancePageHelper.CreateCard(
            "Instance.Saves.Info.Details.Title",
            out var infoBody);
        _titleText = new TextBlock
        {
            FontSize = 18d,
            FontWeight = FontWeight.SemiBold
        };
        infoBody.Children.Add(_titleText);
        _panHints = new StackPanel
        {
            Spacing = 6d
        };
        infoBody.Children.Add(_panHints);
        _panInfo = new StackPanel
        {
            Spacing = 4d
        };
        infoBody.Children.Add(_panInfo);
        panel.Children.Add(infoCard);

        _settingsCard = InstancePageHelper.CreateCard(
            "Instance.Saves.Info.Settings.Title",
            out var settingsBody);
        settingsBody.Children.Add(
            new MyHint
            {
                Text = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
                Theme = MyHint.Themes.Blue,
                Margin = new Thickness(0d, 0d, 0d, 8d)
            });

        _comboAllowCommands = new MyComboBox
        {
            Width = 220d
        };
        _comboAllowCommands.Items.Add(Lang.Text("Instance.Saves.Info.AllowCommands.Allowed"));
        _comboAllowCommands.Items.Add(Lang.Text("Instance.Saves.Info.AllowCommands.NotAllowed"));
        _comboAllowCommands.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
        _rowAllowCommands = InstancePageHelper.Row(
            Lang.Text("Instance.Saves.Info.AllowCommands"),
            _comboAllowCommands);
        settingsBody.Children.Add(_rowAllowCommands);

        _comboDifficulty = new MyComboBox
        {
            Width = 220d
        };
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Peaceful"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Easy"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Normal"));
        _comboDifficulty.Items.Add(Lang.Text("Instance.Saves.Info.Difficulty.Hard"));
        _comboDifficulty.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
        _rowDifficulty = InstancePageHelper.Row(
            Lang.Text("Instance.Saves.Info.GameDifficultyLabel"),
            _comboDifficulty);
        settingsBody.Children.Add(_rowDifficulty);

        _checkLockDifficulty = new MyCheckBox
        {
            Text = Lang.Text("Instance.Saves.Info.LockDifficulty")
        };
        ToolTip.SetTip(
            _checkLockDifficulty,
            Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"));
        _checkLockDifficulty.Change += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
        settingsBody.Children.Add(_checkLockDifficulty);
        panel.Children.Add(_settingsCard);
    }

    public void Reload()
    {
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _syncing = true;
        try
        {
            await ViewModel.LoadAsync(_saveFolder);
            Render();
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Render()
    {
        _syncing = true;
        try
        {
            _titleText.Text = string.IsNullOrEmpty(ViewModel.LevelName)
                ? Lang.Text("Instance.Saves.Info.GetFailed")
                : ViewModel.LevelName;
            _panInfo.Children.Clear();
            _panHints.Children.Clear();

            if (ViewModel.Save is null)
            {
                _settingsCard.IsVisible = false;
                return;
            }

            foreach (var row in ViewModel.Rows)
                if (row.IsSeed)
                    AddSeedRow(row);
                else
                    AddInfoRow(row.Head, row.Content);

            RenderHints();
            _settingsCard.IsVisible = ViewModel.ShowSettings;
            _rowAllowCommands.IsVisible = ViewModel.HasAllowCommandsSetting;
            _rowDifficulty.IsVisible = ViewModel.HasDifficultySetting;
            _comboAllowCommands.SelectedIndex = ViewModel.AllowCommandsIndex;
            _comboAllowCommands.IsEnabled = ViewModel.HasAllowCommandsSetting;
            _comboDifficulty.SelectedIndex = ViewModel.DifficultyIndex;
            _comboDifficulty.IsEnabled = ViewModel.HasDifficultySetting;
            _checkLockDifficulty.Checked = ViewModel.IsDifficultyLocked;
            _checkLockDifficulty.IsEnabled = ViewModel.HasDifficultySetting;
            _checkLockDifficulty.IsVisible = ViewModel.ShowLockDifficulty;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RenderHints()
    {
        if (ViewModel.ShowVersionHint13)
            _panHints.Children.Add(NewVersionHint(ViewModel.VersionHint13Text));
        if (ViewModel.ShowVersionHint18)
            _panHints.Children.Add(NewVersionHint(ViewModel.VersionHint18Text));
        if (ViewModel.ShowVersionHint19)
            _panHints.Children.Add(NewVersionHint(ViewModel.VersionHint19Text));
    }

    private static MyHint NewVersionHint(string text)
    {
        return new MyHint
        {
            Text = text,
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 2d)
        };
    }

    private void AddInfoRow(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Height = 26d
        };
        grid.Children.Add(
            new TextBlock
            {
                Text = label,
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 0d, 25d, 0d)
            });
        var valueBlock = new TextBlock
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(valueBlock);
        _panInfo.Children.Add(grid);
    }

    private void AddSeedRow(SaveInfoRow row)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Height = 28d
        };
        grid.Children.Add(
            new TextBlock
            {
                Text = row.Head,
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 0d, 25d, 0d)
            });
        var seedButton = new MyButton
        {
            Text = row.Content,
            MinWidth = 140d,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = row.CanCopySeed
        };
        ToolTip.SetTip(seedButton, Lang.Text("Common.Action.Copy"));
        seedButton.Click += (_, _) => _ = CopySeedAsync(row.Seed);
        Grid.SetColumn(seedButton, 1);
        grid.Children.Add(seedButton);
        var chunkbase = new MyIconButton
        {
            SvgIcon = "lucide/external-link",
            LogoScale = 0.9d,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = row.CanCopySeed
        };
        ToolTip.SetTip(chunkbase, Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"));
        chunkbase.Click += (_, _) => ViewModel.OpenChunkbase(row.Seed);
        Grid.SetColumn(chunkbase, 2);
        grid.Children.Add(chunkbase);
        _panInfo.Children.Add(grid);
    }

    private async Task CopySeedAsync(long? seed)
    {
        var text = PageInstanceSavesInfoViewModel.GetSeedCopyText(seed);
        if (text is null)
            return;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(text);
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Saves.Info.Error.ClipboardFailed", ex);
        }
    }

    private async Task ApplyChangesAsync()
    {
        if (ViewModel.Save is null || _syncing || ViewModel.IsApplying)
            return;
        _syncing = true;
        try
        {
            await ViewModel.ApplyAllowCommandsAsync(_comboAllowCommands.SelectedIndex);
            await ViewModel.ApplyDifficultyAsync(
                _comboDifficulty.SelectedIndex,
                _checkLockDifficulty.Checked == true);
            Render();
        }
        finally
        {
            _syncing = false;
        }
    }
}
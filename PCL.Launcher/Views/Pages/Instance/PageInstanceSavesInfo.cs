using Avalonia;
using Avalonia.Controls;
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
public partial class PageInstanceSavesInfo : MyPageRight, IRefreshable
{
    private string? _saveFolder;
    private bool _syncing;

    public PageInstanceSavesInfo()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;
        Grid.SetIsSharedSizeScope(PanMain, true);

        ComboAllowCommands.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
        ComboDifficulty.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
        CheckLockDifficulty.Change += (_, _) =>
        {
            if (!_syncing) _ = ApplyChangesAsync();
        };
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
        Reload();
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
            TitleText.Text = string.IsNullOrEmpty(ViewModel.LevelName)
                ? Lang.Text("Instance.Saves.Info.GetFailed")
                : ViewModel.LevelName;
            PanInfo.Children.Clear();

            if (ViewModel.Save is null)
                return;

            foreach (var row in ViewModel.Rows)
                if (row.IsSeed)
                    AddSeedRow(row);
                else
                    AddInfoRow(row.Head, row.Content);

            ComboAllowCommands.SelectedIndex = ViewModel.AllowCommandsIndex;
            ComboDifficulty.SelectedIndex = ViewModel.DifficultyIndex;
            CheckLockDifficulty.Checked = ViewModel.IsDifficultyLocked;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void AddInfoRow(string label, string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "SavesInfoLabel"
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(24d)
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        grid.Children.Add(
            new TextBlock
            {
                Text = label,
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 3d, 0d, 3d)
            });
        var valueBlock = new TextBlock
        {
            Text = value,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 3d, 0d, 3d)
        };
        Grid.SetColumn(valueBlock, 2);
        grid.Children.Add(valueBlock);
        PanInfo.Children.Add(grid);
    }

    private void AddSeedRow(SaveInfoRow row)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "SavesInfoLabel"
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(24d)
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        grid.Children.Add(
            new TextBlock
            {
                Text = row.Head,
                Opacity = 0.7d,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0d, 3d, 0d, 3d)
            });
        // WPF 使用 MyTextButton 而不是带白色底框的 MyButton。
        var seedButton = new MyTextButton
        {
            Text = row.Content,
            Margin = new Thickness(0d, 3d, 0d, 3d),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = row.CanCopySeed
        };
        ToolTip.SetTip(seedButton, Lang.Text("Common.Action.Copy"));
        seedButton.Click += (_, _) => _ = CopySeedAsync(row.Seed);
        Grid.SetColumn(seedButton, 2);
        grid.Children.Add(seedButton);
        var chunkbase = new MyIconButton
        {
            SvgIcon = "lucide/external-link",
            Width = 22d,
            Height = 22d,
            LogoScale = 0.9d,
            Margin = new Thickness(4d, 3d, 0d, 3d),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = row.CanCopySeed
        };
        ToolTip.SetTip(chunkbase, Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip"));
        chunkbase.Click += (_, _) => ViewModel.OpenChunkbase(row.Seed);
        Grid.SetColumn(chunkbase, 3);
        grid.Children.Add(chunkbase);
        PanInfo.Children.Add(grid);
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
            await ViewModel.ApplyAllowCommandsAsync(ComboAllowCommands.SelectedIndex);
            await ViewModel.ApplyDifficultyAsync(
                ComboDifficulty.SelectedIndex,
                CheckLockDifficulty.Checked == true);
            Render();
        }
        finally
        {
            _syncing = false;
        }
    }
}
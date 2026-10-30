using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSavesInfo : IRefreshable, IPageView
{
    private readonly PageInstanceSavesInfoViewModel _vm = new();
    private bool _refreshed;

    public PageInstanceSavesInfo()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageInfo_Loaded;
        Unloaded += PageInfo_Unloaded;
    }

    public PageInstanceSavesInfoViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    void IRefreshable.Refresh()
    {
        Refresh();
    }

    public void Refresh()
    {
        _ = RefreshInfoAsync();
    }

    private void PageInfo_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void PageInfo_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshed = false;
    }

    private void RefreshOnNavigate()
    {
        PanBack.ScrollToHome();
        if (_refreshed)
            return;
        _refreshed = true;
        Refresh();
    }

    private async Task RefreshInfoAsync()
    {
        try
        {
            await _vm.LoadAsync();
            if (Launcher.frmInstanceSavesLeft is not null)
                Launcher.frmInstanceSavesLeft.ViewModel.ShowDatapackTab = _vm.ShowDatapackTab;
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            BuildSettings();
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.Info.Error.LoadFailed"), Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Info.Error.LoadFailed"));
        }
    }

    private void SeedButton_Click(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveInfoRow row })
            return;
        CopySeed(row.Content);
    }

    private void ChunkbaseButton_Click(object sender, EventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SaveInfoRow row })
            return;
        PageInstanceSavesInfoViewModel.OpenChunkbase(row.Content, row.VersionName);
    }

    private static void CopySeed(string content)
    {
        try
        {
            Common.ClipboardSet(content);
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.Info.Error.ClipboardFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.Info.Error.ClipboardFailed"));
        }
    }

    private void BuildSettings()
    {
        if (_vm.HasAllowCommandsSetting)
            BuildAllowCommandsSetting(_vm.AllowCommands);
        if (_vm.HasDifficultySetting)
            BuildDifficultySetting(_vm.IsHardcore, _vm.IsDifficultyLocked, _vm.DifficultyValue);
    }

    private void BuildAllowCommandsSetting(bool allowCommands)
    {
        var folder = PageInstanceSavesLeft.currentSave;
        var combo = new MyComboBox
        {
            Width = 100d,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            SelectedValuePath = "Value",
            DisplayMemberPath = "Display",
            ItemsSource = _vm.AllowCommandsOptions
        };
        combo.SelectedValue = allowCommands ? 1 : 0;
        combo.SelectionChanged += async (_, _) =>
        {
            try
            {
                if (combo.SelectedValue is null)
                    return;
                await _vm.ApplyAllowCommandsAsync(folder, (int)combo.SelectedValue);
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.Info.Modify.CheatFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Modify.CheatFailed"));
            }
        };

        AddSettingRow(Lang.Text("Instance.Saves.Info.AllowCommands"), combo);
    }

    private void BuildDifficultySetting(bool isHardcore, bool isLocked, int difficultyValue)
    {
        var folder = PageInstanceSavesLeft.currentSave;
        var combo = new MyComboBox
        {
            Width = 100d,
            HorizontalAlignment = HorizontalAlignment.Left,
            ToolTip = Lang.Text("Instance.Saves.Info.Modify.BeforeSave"),
            SelectedValuePath = "Value",
            DisplayMemberPath = "Display",
            ItemsSource = _vm.DifficultyOptions
        };
        combo.SelectedValue = difficultyValue;

        var lockCheckBox = new MyCheckBox
        {
            Text = Lang.Text("Instance.Saves.Info.LockDifficulty"),
            ToolTip = Lang.Text("Instance.Saves.Info.LockDifficulty.ToolTip"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10d, 0d, 0d, 0d),
            Checked = isLocked,
            Visibility = isHardcore ? Visibility.Collapsed : Visibility.Visible
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        panel.Children.Add(combo);
        panel.Children.Add(lockCheckBox);

        async Task ApplyAsync()
        {
            try
            {
                if (combo.SelectedValue is null)
                    return;
                await _vm.ApplyDifficultyAsync(folder, (int)combo.SelectedValue, isHardcore,
                    lockCheckBox.Checked == true);
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"));
            }
        }

        combo.SelectionChanged += (_, _) => _ = ApplyAsync();
        lockCheckBox.Change += (_, _) => _ = ApplyAsync();

        AddSettingRow(Lang.Text("Instance.Saves.Info.GameDifficultyLabel"), panel);
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
}

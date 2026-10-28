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
            if (ModMain.frmInstanceSavesLeft is not null)
                ModMain.frmInstanceSavesLeft.ViewModel.ShowDatapackTab = _vm.ShowDatapackTab;
            PanList.Children.Clear();
            PanList.RowDefinitions.Clear();
            PanSettingsList.Children.Clear();
            PanSettingsList.RowDefinitions.Clear();
            BuildInfoTable();
            BuildSettings();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Error.LoadFailed"), ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Info.Error.LoadFailed"));
        }
    }

    private void BuildInfoTable()
    {
        foreach (var row in _vm.Rows)
            AddInfoRow(row);
    }

    private void AddInfoRow(SaveInfoRow row)
    {
        var headBlock = new TextBlock { Text = row.Head, Margin = new Thickness(0d, 3d, 0d, 3d) };
        var contentStack = new StackPanel { Orientation = Orientation.Horizontal };

        if (row.IsSeed && row.Content != Lang.Text("Instance.Saves.Info.GetFailed"))
        {
            var seedBtn = new MyTextButton { Text = row.Content, Margin = new Thickness(0d, 3d, 0d, 3d) };
            var content = row.Content;
            var versionName = row.VersionName;
            seedBtn.Click += (_, _) => CopySeed(content);
            contentStack.Children.Add(seedBtn);

            var chunkbaseBtn = new MyIconButton
            {
                SvgIcon = "lucide/external-link",
                Width = 22d,
                Height = 22d,
                ToolTip = Lang.Text("Instance.Saves.Info.Chunkbase.ToolTip")
            };
            chunkbaseBtn.Click += (_, _) => PageInstanceSavesInfoViewModel.OpenChunkbase(content, versionName);
            contentStack.Children.Add(chunkbaseBtn);
        }
        else
        {
            contentStack.Children.Add(new TextBlock { Text = row.Content, Margin = new Thickness(0d, 3d, 0d, 3d) });
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

    private static void CopySeed(string content)
    {
        try
        {
            ModBase.ClipboardSet(content);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Error.ClipboardFailed"), ModBase.LogLevel.Hint,
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
                ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Modify.CheatFailed"), ModBase.LogLevel.Hint,
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
                ModBase.Log(ex, Lang.Text("Instance.Saves.Info.Modify.DifficultyFailed"), ModBase.LogLevel.Hint,
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

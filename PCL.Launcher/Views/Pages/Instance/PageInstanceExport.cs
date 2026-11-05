using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例导出页：包名/版本、按实例内容动态显隐的导出内容选项、高级选项与底部导出按钮。
///     配置项树、动态子项与导出流程由 <see cref="PageInstanceExportViewModel" /> 维护。
/// </summary>
public class PageInstanceExport : MyPageRight, IRefreshable
{
    private readonly MyExtraTextButton _btnStartExport;
    private readonly Dictionary<ExportOption, MyCheckBox> _optionBoxes = [];
    private readonly Dictionary<ExportOption, StackPanel> _optionPanels = [];
    private readonly StackPanel _panMain;

    private MyIconTextButton _btnResetOverrides = null!;
    private MyCard _cardOptions = null!;
    private MyCheckBox _checkBundleFiles = null!;
    private MyCheckBox _checkModrinth = null!;
    private MyHint _hintBundle = null!;
    private MyHint _hintOptiFine = null!;
    private StackPanel _panOptions = null!;
    private bool _syncing;
    private MyTextBox _textName = null!;
    private MyTextBox _textVersion = null!;

    public PageInstanceExport()
    {
        var root = new Grid();
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 10d, 25d, 65d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        root.Children.Add(scroll);
        _btnStartExport = new MyExtraTextButton
        {
            Text = Lang.Text("Instance.Export.StartExport"),
            SvgIcon = "lucide/package",
            LogoScale = 1.1d,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _btnStartExport.Click += (_, _) => _ = StartExportAsync();
        root.Children.Add(_btnStartExport);
        Child = root;

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
        BuildUi();
    }

    public PageInstanceExportViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        RefreshAll();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        var changed = ViewModel.AttachInstance(InstancePageHelper.ResolveInstance(parameter));
        if (changed)
            ViewModel.RefreshAll();
        else
            ViewModel.RefreshSubOptions();
        BuildOptionTree();
        SyncUiFromViewModel();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        ViewModel.RefreshAll();
        BuildOptionTree();
        SyncUiFromViewModel();
    }

    public void RefreshAll()
    {
        ViewModel.RefreshAll();
        BuildOptionTree();
        SyncUiFromViewModel();
        PanScroll?.Offset = new Vector(0d, 0d);
    }

    private void SyncUiFromViewModel()
    {
        _syncing = true;
        try
        {
            if (_textName.Text != ViewModel.PackName)
                _textName.Text = ViewModel.PackName;
            if (_textVersion.Text != ViewModel.PackVersion)
                _textVersion.Text = ViewModel.PackVersion;
            _hintOptiFine.IsVisible = ViewModel.HasOptiFine;
            _hintBundle.IsVisible = ViewModel.ShowBundleHint;
            _checkBundleFiles.Checked = ViewModel.BundleFiles;
            _checkBundleFiles.IsEnabled = ViewModel.BundleEnabled;
            _checkModrinth.Checked = ViewModel.Modrinth;
            _checkModrinth.IsEnabled = ViewModel.ModrinthEnabled;
            _btnResetOverrides.IsVisible = ViewModel.ResetOverridesVisible;
            _panOptions.IsVisible = ViewModel.OptionsVisible;
            _cardOptions.Title = ViewModel.OptionsTitle;

            foreach (var (option, box) in _optionBoxes)
            {
                box.Checked = option.IsChecked;
                box.IsVisible = option.IsVisible;
                box.IsEnabled = option.IsEnabled;
            }

            foreach (var (option, panel) in _optionPanels)
                panel.IsVisible = option.IsVisible && option.IsChecked;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncOptionsAndAdvanced()
    {
        _syncing = true;
        try
        {
            _hintBundle.IsVisible = ViewModel.ShowBundleHint;
            _checkBundleFiles.Checked = ViewModel.BundleFiles;
            _checkBundleFiles.IsEnabled = ViewModel.BundleEnabled;
            _checkModrinth.Checked = ViewModel.Modrinth;
            _checkModrinth.IsEnabled = ViewModel.ModrinthEnabled;
            _btnResetOverrides.IsVisible = ViewModel.ResetOverridesVisible;
            _panOptions.IsVisible = ViewModel.OptionsVisible;
            _cardOptions.Title = ViewModel.OptionsTitle;

            foreach (var (option, box) in _optionBoxes)
            {
                box.Checked = option.IsChecked;
                box.IsVisible = option.IsVisible;
                box.IsEnabled = option.IsEnabled;
            }

            foreach (var (option, panel) in _optionPanels)
                panel.IsVisible = option.IsVisible && option.IsChecked;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ResetConfigOverrides()
    {
        ViewModel.ResetConfigOverrides();
        SyncOptionsAndAdvanced();
    }

    private void ImportConfig()
    {
        if (ViewModel.ImportConfig())
        {
            BuildOptionTree();
            SyncUiFromViewModel();
        }
    }

    private void ExportConfig()
    {
        ViewModel.ExportConfig();
    }

    private static string? GetSingleTxtFile(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not { Length: 1 })
            return null;
        var path = files[0].TryGetLocalPath();
        return !string.IsNullOrEmpty(path)
            && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetSingleTxtFile(e) is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var path = GetSingleTxtFile(e);
        if (path is not null && ViewModel.ReadConfigFile(path))
        {
            BuildOptionTree();
            SyncUiFromViewModel();
        }

        e.Handled = true;
    }

    private async Task StartExportAsync()
    {
        if (ViewModel.IsExporting)
            return;
        _btnStartExport.IsEnabled = false;
        try
        {
            await ViewModel.StartExportAsync();
        }
        finally
        {
            _btnStartExport.IsEnabled = true;
        }
    }

    #region 界面构建

    private void BuildUi()
    {
        _panMain.Children.Clear();
        _optionBoxes.Clear();
        _optionPanels.Clear();

        _hintOptiFine = new MyHint
        {
            Text = Lang.Text("Instance.Export.ModrinthWarning"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 15d, 0d, 0d),
            IsVisible = ViewModel.HasOptiFine
        };
        _panMain.Children.Add(_hintOptiFine);

        var headerCard = new MyCard
        {
            Margin = new Thickness(0d, 15d)
        };
        var headerGrid = new Grid
        {
            Margin = new Thickness(22d, 15d, 25d, 15d),
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,0.3*"),
            RowDefinitions = new RowDefinitions("28")
        };
        var nameLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Export.PackName"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        headerGrid.Children.Add(nameLabel);
        _textName = new MyTextBox
        {
            MaxLength = 100
        };
        _textName.TextChanged += (_, _) =>
        {
            if (!_syncing)
                ViewModel.PackName = _textName.Text ?? string.Empty;
        };
        Grid.SetColumn(_textName, 1);
        headerGrid.Children.Add(_textName);
        var versionLabel = new TextBlock
        {
            Text = Lang.Text("Instance.Export.PackVersion"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(30d, 0d, 20d, 0d)
        };
        Grid.SetColumn(versionLabel, 2);
        headerGrid.Children.Add(versionLabel);
        _textVersion = new MyTextBox
        {
            MaxLength = 100
        };
        _textVersion.TextChanged += (_, _) =>
        {
            if (!_syncing)
                ViewModel.PackVersion = _textVersion.Text ?? string.Empty;
        };
        Grid.SetColumn(_textVersion, 3);
        headerGrid.Children.Add(_textVersion);
        headerCard.BorderChild = headerGrid;
        _panMain.Children.Add(headerCard);

        _cardOptions = new MyCard
        {
            Margin = new Thickness(0d, 0d, 0d, 15d),
            MinHeight = 39d
        };
        _cardOptions.PointerPressed += (_, _) =>
        {
            if (ViewModel.HasOverrides)
                ResetConfigOverrides();
        };
        _btnResetOverrides = new MyIconTextButton
        {
            Text = Lang.Text("Common.Action.Reset"),
            SvgIcon = "lucide/rotate-ccw",
            LogoScale = 0.9d,
            Height = 24d,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            IsHitTestVisible = true,
            IsVisible = false
        };
        _btnResetOverrides.Click += (_, _) => ResetConfigOverrides();
        _cardOptions.Children.Add(_btnResetOverrides);
        _panOptions = new StackPanel
        {
            Margin = new Thickness(25d, 38d, 15d, 15d)
        };
        _cardOptions.Children.Add(_panOptions);
        _panMain.Children.Add(_cardOptions);

        var advancedCard = new MyCard
        {
            Title = Lang.Text("Instance.Export.Advanced.Title"),
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var advancedBody = new StackPanel
        {
            Margin = new Thickness(25d, 37d, 23d, 20d)
        };
        _hintBundle = new MyHint
        {
            Text = Lang.Text("Instance.Export.Advanced.BundleHint"),
            Theme = MyHint.Themes.Yellow,
            Margin = new Thickness(0d, 2d, 0d, 8d),
            IsVisible = false
        };
        advancedBody.Children.Add(_hintBundle);

        _checkBundleFiles = new MyCheckBox
        {
            Text = Lang.Text("Instance.Export.Advanced.BundleFiles")
        };
        ToolTip.SetTip(
            _checkBundleFiles,
            Lang.Text("Instance.Export.Advanced.BundleFiles.ToolTip"));
        _checkBundleFiles.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.BundleFiles = _checkBundleFiles.Checked == true;
            SyncOptionsAndAdvanced();
        };
        advancedBody.Children.Add(_checkBundleFiles);

        _checkModrinth = new MyCheckBox
        {
            Text = Lang.Text("Instance.Export.Advanced.Modrinth")
        };
        ToolTip.SetTip(_checkModrinth, Lang.Text("Instance.Export.Advanced.Modrinth.ToolTip"));
        _checkModrinth.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.Modrinth = _checkModrinth.Checked == true;
            SyncOptionsAndAdvanced();
        };
        advancedBody.Children.Add(_checkModrinth);

        advancedBody.Children.Add(
            new MyHint
            {
                Text = Lang.Text("Instance.Export.Advanced.ConfigHint"),
                Theme = MyHint.Themes.Blue,
                RelativeSetup = "HintExportConfig",
                CanClose = true,
                Margin = new Thickness(0d, 20d, 0d, 2d)
            });

        var configButtons = new Grid
        {
            Height = 35d,
            Margin = new Thickness(0d, 12d, 0d, 0d),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto")
        };
        var importButton = new MyButton
        {
            Text = Lang.Text("Instance.Export.Advanced.ImportConfig"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            ColorType = MyButton.ColorState.Highlight
        };
        importButton.Click += (_, _) => ImportConfig();
        configButtons.Children.Add(importButton);
        var exportButton = new MyButton
        {
            Text = Lang.Text("Instance.Export.Advanced.ExportConfig"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d)
        };
        Grid.SetColumn(exportButton, 1);
        exportButton.Click += (_, _) => ExportConfig();
        configButtons.Children.Add(exportButton);
        advancedBody.Children.Add(configButtons);

        advancedCard.Children.Add(advancedBody);
        advancedCard.SwapControl = advancedBody;
        advancedCard.IsSwapped = true;
        _panMain.Children.Add(advancedCard);
        BuildOptionTree();
    }

    private void BuildOptionTree()
    {
        _panOptions.Children.Clear();
        _optionBoxes.Clear();
        _optionPanels.Clear();
        foreach (var option in ViewModel.Options)
            AddOptionControl(_panOptions, option);
    }

    private MyCheckBox AddOptionControl(StackPanel parent, ExportOption option)
    {
        var box = new MyCheckBox
        {
            Text = option.Title,
            Height = 26d,
            Tag = option,
            Checked = option.IsChecked,
            IsVisible = option.IsVisible,
            IsEnabled = option.IsEnabled,
            Margin = option.IsIndented
                ? new Thickness(30d, 0d, 0d, 0d)
                : new Thickness(0d)
        };
        if (!string.IsNullOrEmpty(option.Description))
            ToolTip.SetTip(box, option.Description);
        box.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.SetOptionChecked(option, box.Checked == true);
            SyncOptionsAndAdvanced();
        };
        parent.Children.Add(box);
        _optionBoxes[option] = box;

        if (option.Children.Count > 0)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(30d, 0d, 0d, 0d),
                IsVisible = option.IsChecked
            };
            parent.Children.Add(panel);
            _optionPanels[option] = panel;
            foreach (var child in option.Children)
                AddOptionControl(panel, child);
        }

        return box;
    }

    #endregion
}
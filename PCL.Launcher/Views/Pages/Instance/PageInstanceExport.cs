using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
/// 实例导出页：包名/版本、按实例内容动态显隐的导出内容选项、高级选项与底部导出按钮。 配置项树、动态子项与导出流程由
/// <see cref="PageInstanceExportViewModel" /> 维护。
/// </summary>
public partial class PageInstanceExport : MyPageRight, IRefreshable
{
    private readonly Dictionary<ExportOption, MyCheckBox> _optionBoxes = [];
    private readonly Dictionary<ExportOption, StackPanel> _optionPanels = [];
    private bool _syncing;

    public PageInstanceExport()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;
        CardOptions.Bind(
            MyCard.TitleProperty,
            new Binding(nameof(PageInstanceExportViewModel.OptionsTitle))
            {
                Source = ViewModel
            });

        BtnExport.Click += (_, _) => _ = StartExportAsync();
        BtnOverrideCancel.Click += (_, _) => ResetConfigOverrides();
        CardOptions.PointerPressed += (_, _) =>
        {
            if (ViewModel.HasOverrides)
                ResetConfigOverrides();
        };
        TextExportName.TextChanged += (_, _) =>
        {
            if (!_syncing)
                ViewModel.PackName = TextExportName.Text ?? string.Empty;
        };
        TextExportVersion.TextChanged += (_, _) =>
        {
            if (!_syncing)
                ViewModel.PackVersion = TextExportVersion.Text ?? string.Empty;
        };
        TextExportName.GotFocus += (_, _) =>
        {
            if (!string.IsNullOrEmpty(TextExportName.Text))
                return;
            var hint = ViewModel.PackNameHint;
            if (string.IsNullOrEmpty(hint))
                return;
            TextExportName.Text = hint;
            TextExportName.CaretIndex = hint.Length;
        };
        CheckAdvancedInclude.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.BundleFiles = CheckAdvancedInclude.Checked == true;
            SyncOptionsAndAdvanced();
        };
        CheckAdvancedModrinth.Change += (_, _) =>
        {
            if (_syncing)
                return;
            ViewModel.Modrinth = CheckAdvancedModrinth.Checked == true;
            SyncOptionsAndAdvanced();
        };
        BtnAdvancedImport.Click += (_, _) => ImportConfig();
        BtnAdvancedExport.Click += (_, _) => ExportConfig();

        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);

        BuildOptionTree();
        SyncUiFromViewModel();
    }

    public PageInstanceExportViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        RefreshAll();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        var changed = ViewModel.AttachInstance(InstancePageHelper.ResolveInstance(parameter));
        if (changed)
            ViewModel.RefreshAll();
        else
            ViewModel.RefreshSubOptions();
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
            if (TextExportName.Text != ViewModel.PackName)
                TextExportName.Text = ViewModel.PackName;
            if (TextExportVersion.Text != ViewModel.PackVersion)
                TextExportVersion.Text = ViewModel.PackVersion;
            TextExportName.HintText = ViewModel.PackNameHint;
            TextExportVersion.HintText = ViewModel.PackVersionHint;
            HintOptiFine.IsVisible = ViewModel.HasOptiFine;
            HintBundle.IsVisible = ViewModel.ShowBundleHint;
            CheckAdvancedInclude.Checked = ViewModel.BundleFiles;
            CheckAdvancedInclude.IsEnabled = ViewModel.BundleEnabled;
            CheckAdvancedModrinth.Checked = ViewModel.Modrinth;
            CheckAdvancedModrinth.IsEnabled = ViewModel.ModrinthEnabled;
            BtnOverrideCancel.IsVisible = ViewModel.ResetOverridesVisible;
            PanOptions.IsVisible = ViewModel.OptionsVisible;

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
            HintBundle.IsVisible = ViewModel.ShowBundleHint;
            CheckAdvancedInclude.Checked = ViewModel.BundleFiles;
            CheckAdvancedInclude.IsEnabled = ViewModel.BundleEnabled;
            CheckAdvancedModrinth.Checked = ViewModel.Modrinth;
            CheckAdvancedModrinth.IsEnabled = ViewModel.ModrinthEnabled;
            BtnOverrideCancel.IsVisible = ViewModel.ResetOverridesVisible;
            PanOptions.IsVisible = ViewModel.OptionsVisible;

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

    private async void ImportConfig()
    {
        if (await ViewModel.ImportConfig())
        {
            BuildOptionTree();
            SyncUiFromViewModel();
        }
    }

    private async void ExportConfig()
    {
        await ViewModel.ExportConfig();
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
        BtnExport.IsEnabled = false;
        try
        {
            await ViewModel.StartExportAsync();
        }
        finally
        {
            BtnExport.IsEnabled = true;
        }
    }

    private void BuildOptionTree()
    {
        PanOptions.Children.Clear();
        _optionBoxes.Clear();
        _optionPanels.Clear();
        foreach (var option in ViewModel.Options)
            AddOptionControl(PanOptions, option);
    }

    private MyCheckBox AddOptionControl(StackPanel parent, ExportOption option)
    {
        var box = new MyCheckBox
        {
            Height = 26d,
            Tag = option,
            Checked = option.IsChecked,
            IsVisible = option.IsVisible,
            IsEnabled = option.IsEnabled,
            Margin = option.IsIndented
                ? new Thickness(30d, 0d, 0d, 0d)
                : new Thickness(0d)
        };
        if (string.IsNullOrEmpty(option.TitleKey))
            box.Text = option.Title;
        else
            box.SetLocalizedValue(MyCheckBox.TextProperty, option.TitleKey);
        if (!string.IsNullOrEmpty(option.DescriptionKey))
            box.SetLocalizedValue(ToolTip.TipProperty, option.DescriptionKey);
        else if (!string.IsNullOrEmpty(option.Description))
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
}
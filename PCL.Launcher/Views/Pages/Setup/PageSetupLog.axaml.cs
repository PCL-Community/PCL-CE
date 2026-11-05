using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>日志设置页：列出日志文件、导出当前/全部日志、打开日志目录与清理非活动日志。</summary>
public partial class PageSetupLog : MyPageRight
{
    private readonly PageSetupLogViewModel _viewModel = new();
    private bool _loaded;
    private bool _refreshed;

    public PageSetupLog()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = _viewModel;

        BtnOpenDir.Click += (_, _) => PageSetupLogViewModel.OpenLogDirectory();
        BtnExport.Click += (_, _) => _ = ExportCurrentAsync();
        BtnExportAll.Click += (_, _) => _ = ExportAllAsync();
        BtnClear.Click += (_, _) => _ = CleanAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Refresh();
        };
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageSetupLogViewModel ViewModel => _viewModel;

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        Refresh();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        var offset = PanBack.Offset.Y;
        LoadList();
        Dispatcher.UIThread.Post(() => PanBack.Offset = new Vector(PanBack.Offset.X, offset));
    }

    private void Refresh()
    {
        if (_refreshed) return;
        _refreshed = true;
        LoadList();
    }

    private void LoadList()
    {
        _ = LoadListAsync();
    }

    private async Task LoadListAsync()
    {
        try
        {
            await _viewModel.LoadListAsync();
            PanList.Children.Clear();
            foreach (var entry in _viewModel.Entries)
            {
                var item = new MyListItem
                {
                    Type = MyListItem.CheckType.Clickable,
                    Title = entry.Title,
                    Info = entry.FilePath
                };
                var file = entry.FilePath;
                item.Click += (_, _) => Basics.OpenPath(file);
                PanList.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "加载日志列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private async Task CleanAsync()
    {
        var ctl = BtnClear;
        ctl.IsEnabled = false;
        try
        {
            if (await _viewModel.CleanAsync())
                LoadList();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "清理日志文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            ctl.IsEnabled = true;
        }
    }

    private async Task ExportAllAsync()
    {
        try
        {
            await _viewModel.ExportAllAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }

    private async Task ExportCurrentAsync()
    {
        try
        {
            await _viewModel.ExportCurrentAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }
}

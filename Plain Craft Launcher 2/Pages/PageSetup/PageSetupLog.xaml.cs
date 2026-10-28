using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLog : IPageView
{
    private readonly PageSetupLogViewModel _vm = new();
    private bool _refreshed;

    public PageSetupLog()
    {
        InitializeComponent();
        Loaded += PageOtherLog_Loaded;
        Unloaded += PageOtherLog_Unloaded;
    }

    public PageSetupLogViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter) => Refresh();

    public void OnNavigatedFrom()
    {
    }

    private void PageOtherLog_Loaded(object sender, RoutedEventArgs e) => Refresh();

    private void PageOtherLog_Unloaded(object sender, RoutedEventArgs e) => _refreshed = false;

    private void Refresh()
    {
        PanBack.ScrollToHome();
        if (_refreshed)
            return;
        _refreshed = true;
        LoadList();
    }

    public void LoadList() => _ = LoadListAsync();

    private async Task LoadListAsync()
    {
        try
        {
            await _vm.LoadListAsync();
            PanList.Children.Clear();
            foreach (var entry in _vm.Entries)
            {
                var ele = new MyListItem
                {
                    Type = MyListItem.CheckType.Clickable,
                    Title = entry.Title,
                    Info = entry.FilePath,
                    Tag = entry.FilePath
                };
                ele.Click += (listItem, _) =>
                {
                    var file = (string)((MyListItem)listItem).Tag;
                    Basics.OpenPath(file);
                };
                PanList.Children.Add(ele);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "加载日志列表失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private void ButtonOpenDir_OnClick(object sender, MouseButtonEventArgs e)
    {
        PageSetupLogViewModel.OpenLogDirectory();
    }

    private async void ButtonClean_OnClick(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null)
            ctl.IsEnabled = false;
        try
        {
            if (await _vm.CleanAsync())
                LoadList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "清理日志文件失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            if (ctl is not null)
                ctl.IsEnabled = true;
        }
    }

    private async void ButtonExportAll_OnClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            await _vm.ExportAllAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Log.ExportFailed"), ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }

    private async void ButtonExport_OnClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            await _vm.ExportCurrentAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Setup.Log.ExportFailed"), ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }
}

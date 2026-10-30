using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSaves : IRefreshable, IPageView
{
    private readonly DispatcherTimer fileSystemRefreshTimer;
    private readonly DispatcherTimer searchTimer;
    private readonly PageInstanceSavesViewModel _vm = new();
    private FileSystemWatcher? fileSystemWatcher;
    private bool _initialized;
    private bool _refreshed;

    public PageInstanceSaves()
    {
        InitializeComponent();
        DataContext = _vm;
        fileSystemRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += Page_Unloaded;
        fileSystemRefreshTimer.Tick += FileSystemRefreshTimer_Tick;
        searchTimer.Tick += SearchTimer_Tick;
        SearchBox.TextChanged += SearchRun;
    }

    public PageInstanceSavesViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshPage();
    }

    public void OnNavigatedFrom()
    {
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    private void RefreshSelf()
    {
        _vm.CheckQuickPlay();
        Refresh();
    }

    public static void Refresh()
    {
        if (Launcher.frmInstanceSaves is not null)
            Launcher.frmInstanceSaves.Reload();
        Launcher.frmInstanceLeft.ItemWorld.Checked = true;
        HintService.Hint(Lang.Text("Instance.Saves.Status.Refreshing"), log: false);
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshPage();
    }

    private void RefreshPage()
    {
        PanBack.ScrollToHome();
        if (_refreshed)
            return;
        _refreshed = true;
        Reload();
    }

    public void Reload()
    {
        Animations.AniBeginControl();
        try
        {
            PanBack.ScrollToHome();
            _ = ReloadAsync();
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private async Task ReloadAsync()
    {
        try
        {
            await _vm.EnsureWorldFolderAsync();
            await _vm.LoadListAsync();
            PanNoWorld.UpdateLayout();
            PanContent.UpdateLayout();
            RebuildList();
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.LoadListFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.LoadListFailed"));
        }

        if (_initialized)
            return;
        _initialized = true;
        SetupFileSystemWatcher();
        BtnSort.Click += BtnSortClick;
        _vm.SetSortMethod(_vm.SortMethod);
    }

    private async void ApplySortAsync(SaveSortMethod method)
    {
        try
        {
            await _vm.ApplySortAsync(method);
            RebuildList();
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.RefreshUiFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.RefreshUiFailed"));
        }
    }

    private async Task RefreshUiAsync()
    {
        try
        {
            await _vm.SearchAsync();
            PanNoWorld.UpdateLayout();
            PanContent.UpdateLayout();
            RebuildList();
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.RefreshUiFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.RefreshUiFailed"));
        }
    }

    private void RebuildList()
    {
        try
        {
            Animations.AniBeginControl();
            PanList.Children.Clear();
            foreach (var entry in _vm.Saves)
                PanList.Children.Add(CreateSaveItem(entry));
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.RefreshUiFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.RefreshUiFailed"));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private MyListItem CreateSaveItem(SaveEntry entry)
    {
        var curFolder = entry.FolderPath;
        var worldItem = new MyListItem
        {
            Logo = entry.Logo,
            Title = entry.Title,
            Info = entry.Info,
            Type = MyListItem.CheckType.Clickable
        };
        worldItem.Command = _vm.OpenDetailsCommand;
        worldItem.CommandParameter = curFolder;

        var btnOpen = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            ToolTip = Lang.Text("Common.Action.Open")
        };
        btnOpen.Command = _vm.OpenSaveFolderCommand;
        btnOpen.CommandParameter = curFolder;

        var btnDelete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            ToolTip = Lang.Text("Common.Action.Delete")
        };
        btnDelete.Click += async (_, _) =>
        {
            worldItem.IsEnabled = false;
            worldItem.Info = Lang.Text("Instance.Saves.Deleting");
            try
            {
                if (await _vm.DeleteSaveAsync(curFolder))
                {
                    HintService.Hint(Lang.Text("Instance.Saves.DeletedToRecycleBin"));
                    RemoveItem(worldItem);
                    return;
                }

                Reload();
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.DeleteFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.DeleteFailed"));
                Reload();
            }
        };

        var btnCopy = new MyIconButton
        {
            SvgIcon = "lucide/copy",
            ToolTip = Lang.Text("Common.Action.Copy")
        };
        btnCopy.Command = _vm.CopySaveCommand;
        btnCopy.CommandParameter = curFolder;

        var btnInfo = new MyIconButton
        {
            SvgIcon = "lucide/info",
            ToolTip = Lang.Text("Instance.Saves.Details")
        };
        btnInfo.Command = _vm.OpenDetailsCommand;
        btnInfo.CommandParameter = curFolder;

        var btnLaunch = new MyIconButton
        {
            SvgIcon = "lucide/play",
            ToolTip = Lang.Text("Instance.Saves.QuickPlay")
        };
        btnLaunch.Command = _vm.LaunchSaveCommand;
        btnLaunch.CommandParameter = curFolder;

        worldItem.Buttons = _vm.QuickPlayEnabled
            ? [btnOpen, btnDelete, btnCopy, btnInfo, btnLaunch]
            : [btnOpen, btnDelete, btnCopy, btnInfo];
        return worldItem;
    }

    private void SetupFileSystemWatcher()
    {
        if (fileSystemWatcher is not null)
            fileSystemWatcher.Dispose();

        var worldPath = _vm.WorldPath;
        if (!Directory.Exists(worldPath))
            Directory.CreateDirectory(worldPath);

        fileSystemWatcher = new FileSystemWatcher
        {
            Path = worldPath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        fileSystemWatcher.Created += OnFileSystemChanged;
        fileSystemWatcher.Deleted += OnFileSystemChanged;
        fileSystemWatcher.Renamed += OnFileSystemChanged;
        fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        fileSystemRefreshTimer.Stop();
        fileSystemRefreshTimer.Start();
    }

    private void FileSystemRefreshTimer_Tick(object sender, EventArgs e)
    {
        fileSystemRefreshTimer.Stop();
        Common.RunInUi(() => Reload(), true);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (fileSystemWatcher is not null)
        {
            fileSystemWatcher.Created -= OnFileSystemChanged;
            fileSystemWatcher.Deleted -= OnFileSystemChanged;
            fileSystemWatcher.Renamed -= OnFileSystemChanged;
            fileSystemWatcher.Dispose();
            fileSystemWatcher = null;
        }

        fileSystemRefreshTimer.Stop();
        searchTimer.Stop();
        _refreshed = false;
    }

    private void RemoveItem(MyListItem item)
    {
        if (PanList.Children.IndexOf(item) == -1)
            return;
        PanList.Children.Remove(item);
        _ = RefreshUiAsync();
    }

    private void BtnPaste_Click(object sender, MouseButtonEventArgs e)
    {
        var files = Clipboard.GetFileDropList();
        var worldPath = _vm.WorldPath;
        var loaders = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<int, int>("Copy saves", _ =>
            {
                var copied = PageInstanceSavesViewModel.CopySaveFolders(files, worldPath);
                if (copied > 0)
                    HintService.Hint(Lang.Text("Instance.Saves.PastedCount", copied.ToString()), HintType.Success);
                Common.RunInUi(() => Reload());
            })
        };
        var loader = new Loading.LoaderCombo<int>(
            $"{PageInstanceLeft.McInstance.Name} - {Lang.Text("Instance.Saves.CopySave")}", loaders)
        {
            OnStateChanged = DownloadUi.LoaderStateChangedHintOnly
        };
        loader.Start(1);
        Loading.LoaderTaskbarAdd(loader);
        Launcher.frmMain.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
    }

    private void BtnSortClick(object sender, EventArgs e)
    {
        var body = new ContextMenu();
        foreach (var i in Enum.GetValues<SaveSortMethod>())
        {
            var item = new MyMenuItem { Header = _vm.GetSortName(i) };
            item.Click += (_, _) => ApplySortAsync(i);
            body.Items.Add(item);
        }

        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private void SearchRun(object sender, EventArgs e)
    {
        searchTimer.Stop();
        searchTimer.Start();
    }

    private void SearchTimer_Tick(object sender, EventArgs e)
    {
        searchTimer.Stop();
        _ = RefreshUiAsync();
    }
}

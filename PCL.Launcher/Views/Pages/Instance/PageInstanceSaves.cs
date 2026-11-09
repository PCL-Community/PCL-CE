using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Platform;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例存档列表页：扫描实例 <c>saves</c> 目录下的存档文件夹并显示名称、创建/修改时间与图标；
///     支持搜索、按文件名/创建时间/修改时间排序、空态三按钮（打开文件夹/下载新存档/粘贴剪贴板文件），
///     点击进入存档详情；删除优先移入系统回收站，不支持回收站时明确告知永久删除。
/// </summary>
public partial class PageInstanceSaves : MyPageRight, IRefreshable
{
    private readonly DispatcherTimer _fileSystemRefreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private bool _isActive;

    public PageInstanceSaves()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;

        _fileSystemRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _fileSystemRefreshTimer.Tick += (_, _) =>
        {
            _fileSystemRefreshTimer.Stop();
            _ = ReloadAsync();
        };
        _searchTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100d)
        };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            ViewModel.ApplySearchAndSort();
            BuildList();
        };

        SearchBox.TextChanged += (_, _) =>
        {
            ViewModel.SearchText = SearchBox.Text ?? string.Empty;
            _searchTimer.Stop();
            _searchTimer.Start();
        };
        BtnSort.Click += (_, _) => ShowSortMenu();
        BtnQuickOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
        BtnQuickDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnQuickPaste.Click += (_, _) => _ = PasteWithLoaderAsync();
        BtnEmptyOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
        BtnEmptyDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnEmptyPaste.Click += (_, _) => _ = PasteWithLoaderAsync();
        BtnErrorRetry.Click += (_, _) => _ = ReloadAsync();

        ViewModel.FileClipboardWriter = WriteFolderToClipboardAsync;
        ViewModel.Updated += BuildList;
    }

    public PageInstanceSavesViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        PanBack.ScrollToHome();
        ViewModel.CheckQuickPlay();
        _ = ReloadAsync();
        HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Status.Refreshing"), false));
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        ViewModel.StopWatcher();
        _fileSystemRefreshTimer?.Stop();
        PanBack.ScrollToHome();
        var instance = InstancePageHelper.ResolveInstance(parameter);
        ViewModel.SetInstance(instance);
        _ = ReloadAsync();
    }

    public override void OnNavigatedFrom()
    {
        _isActive = false;
        ViewModel.StopWatcher();
        _fileSystemRefreshTimer?.Stop();
    }

    protected override void RefreshLocalization()
    {
        ViewModel.RefreshLocalization();
        BuildList();
    }

    public async Task ReloadAsync()
    {
        ViewModel.HasError = false;
        try
        {
            await ViewModel.LoadAsync();
            if (!_isActive)
                return;
            BuildList();
            ViewModel.StartWatcher(() =>
                Dispatcher.UIThread.Post(() =>
                {
                    _fileSystemRefreshTimer.Stop();
                    _fileSystemRefreshTimer.Start();
                }));
        }
        catch (Exception ex)
        {
            ViewModel.HasError = true;
            ViewModel.Error = ex.Message;
            InstancePageHelper.ReportError("Instance.Saves.LoadListFailed", ex);
            BuildList();
        }
        finally
        {
            UpdateStatePanels();
        }
    }

    private void UpdateStatePanels()
    {
        PanError.IsVisible = ViewModel.HasError;
        if (ViewModel.HasError)
        {
            PanNoWorld.IsVisible = false;
            PanContent.IsVisible = false;
            return;
        }

        PanNoWorld.IsVisible = ViewModel.ShowNoWorld;
        PanContent.IsVisible = ViewModel.ShowContent;
    }

    private void BuildList()
    {
        if (PanList is null)
            return;

        Animations.AniBeginControl();
        try
        {
            PanList.Children.Clear();
            foreach (var entry in ViewModel.Saves)
                PanList.Children.Add(CreateSaveItem(entry));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private void ShowSortMenu()
    {
        var menu = new ContextMenu();
        foreach (var method in Enum.GetValues<SaveSortMethod>())
        {
            var item = new MyMenuItem
            {
                Header = PageInstanceSavesViewModel.GetSortName(method)
            };
            var target = method;
            item.Click += (_, _) =>
            {
                ViewModel.SetSortMethod(target);
                BuildList();
            };
            menu.Items.Add(item);
        }

        menu.Open(BtnSort);
    }

    private MyListItem CreateSaveItem(SaveEntry entry)
    {
        var folder = entry.FolderPath;
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Title = entry.Title,
            Info = entry.Info,
            Logo = entry.Logo,
            Tag = folder
        };
        entry.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SaveEntry.Info))
                item.Info = entry.Info;
        };

        var open = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(open, Lang.Text("Instance.Saves.OpenFolder"));
        open.Click += (_, _) => ViewModel.OpenSaveFolderCommand.Execute(folder);

        var detail = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(detail, Lang.Text("Instance.Saves.Details"));
        detail.Click += (_, _) => ViewModel.OpenDetailsCommand.Execute(folder);

        var copy = new MyIconButton
        {
            SvgIcon = "lucide/copy",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(copy, Lang.Text("Common.Action.Copy"));
        copy.Click += (_, _) => _ = ViewModel.CopySaveAsync(folder);

        var delete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(delete, Lang.Text("Common.Action.Delete"));
        delete.Click += async (_, _) =>
        {
            item.IsEnabled = false;
            entry.Info = Lang.Text("Instance.Saves.Deleting");
            if (await ViewModel.DeleteSaveAsync(folder))
                return;
            item.IsEnabled = true;
            await ReloadAsync();
        };

        var buttons = new List<MyIconButton>
        {
            open,
            delete,
            copy,
            detail
        };
        if (ViewModel.QuickPlayEnabled)
        {
            var launch = new MyIconButton
            {
                SvgIcon = "lucide/play",
                LogoScale = 0.9d
            };
            ToolTip.SetTip(launch, Lang.Text("Instance.Saves.QuickPlay"));
            launch.Click += (_, _) => ViewModel.LaunchSaveCommand.Execute(folder);
            buttons.Add(launch);
        }

        item.Buttons = buttons;
        item.Click += (_, _) => ViewModel.OpenDetailsCommand.Execute(folder);
        return item;
    }

    private async Task PasteWithLoaderAsync()
    {
        var paths = PlatformServices.Clipboard?.GetFileDropList();
        if (paths is null || paths.Length == 0)
        {
            HintService.Show(Hints.Warning(Lang.Text("Instance.Saves.Empty.Message")));
            return;
        }

        var destination = ViewModel.WorldPath;
        if (string.IsNullOrEmpty(destination))
            return;
        InstancePageHelper.EnsureDirectory(destination);
        foreach (var path in paths)
        {
            if (!Directory.Exists(path)) continue;
            var folderName = new DirectoryInfo(path).Name;
            if (Directory.Exists(Path.Combine(destination, folderName)))
                HintService.Show(
                    Hints.Info(Lang.Text("Instance.Saves.DuplicateFolder", folderName)));
        }

        var copied = 0;
        var task = new Loading.LoaderTask<int, int>(
            Lang.Text("Instance.Saves.CopySave"),
            _ => copied = Files
                .PastePathsAsync(paths, destination, false, true, true)
                .GetAwaiter()
                .GetResult());
        var loader = new Loading.LoaderCombo<int>(
            Lang.Text("Instance.Saves.CopySave"),
            [task])
        {
            OnStateChanged = state =>
            {
                switch (state.State)
                {
                    case LoadState.Finished when copied > 0:
                        HintService.Show(
                            Hints.Success(Lang.Text("Instance.Saves.PastedCount", copied)));
                        _ = ReloadAsync();
                        break;

                    case LoadState.Failed:
                        HintService.Show(Hints.Error(Lang.Text("Instance.Saves.CopyFailed")));
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                    case LoadState.Aborted:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        };
        loader.Start();
        Loading.LoaderTaskbarAdd(loader);
        await Task.CompletedTask;
    }

    private async Task WriteFolderToClipboardAsync(string folder)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
            return;
        var item = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(folder));
        if (item is null)
            return;
        await topLevel.Clipboard.SetFilesAsync([item]);
    }
}
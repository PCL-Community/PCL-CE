using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Core.App.Localization;
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
        BtnQuickPaste.Click += (_, _) => _ = ViewModel.PasteAsync();
        BtnEmptyOpen.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
        BtnEmptyDownload.Click += (_, _) => ViewModel.DownloadNewCommand.Execute(null);
        BtnEmptyPaste.Click += (_, _) => _ = ViewModel.PasteAsync();

        ViewModel.FileClipboardWriter = WriteFolderToClipboardAsync;
        ViewModel.Updated += BuildList;
    }

    public PageInstanceSavesViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        _ = ReloadAsync();
        HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Status.Refreshing"), false));
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        ViewModel.StopWatcher();
        _fileSystemRefreshTimer?.Stop();
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
            InstancePageHelper.ReportError("Instance.Saves.LoadListFailed", ex);
            BuildList();
        }
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
        delete.Click += (_, _) => _ = ViewModel.DeleteSaveAsync(folder);

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
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSaves : IRefreshable
{
    private readonly DispatcherTimer _fileSystemRefreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private FileSystemWatcher _fileSystemWatcher;
    private bool _isLoad;

    private object _quickPlayFeature = false;

    private List<string> _saveFolders = new();
    private string _worldPath;

    public PageInstanceSaves()
    {
        InitializeComponent();
        _fileSystemRefreshTimer = new DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(100d) };
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += Page_Unloaded;
        _fileSystemRefreshTimer.Tick += FileSystemRefreshTimer_Tick;
        _searchTimer.Tick += SearchTimer_Tick;
        SearchBox.TextChanged += SearchRun;
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    private void RefreshSelf()
    {
        Refresh();
        CheckQuickPlay();
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
        // 重复加载部分
        PanBack.ScrollToHome();
        _worldPath = PageInstanceLeft.mcInstance.PathIndie + @"saves\";
        if (!Directory.Exists(_worldPath))
            Directory.CreateDirectory(_worldPath);
        Reload();

        // 非重复加载部分
        if (_isLoad)
            return;
        _isLoad = true;
        CheckQuickPlay();

        // 初始化文件系统监视器和排序按钮
        SetupFileSystemWatcher();
        BtnSort.Click += BtnSortClick;
        SetSortMethod(_currentSortMethod);
    }

    private string GetFolderNameFromPath(string fullPath)
    {
        return string.IsNullOrEmpty(fullPath) ? "" :
            fullPath.EndsWith(@"\") ? new DirectoryInfo(fullPath).Parent?.Name : new DirectoryInfo(fullPath).Name;
    }

    private string GetFileNameFromPath(string fullPath)
    {
        return Path.GetFileName(fullPath);
    }

    private void SetupFileSystemWatcher()
    {
        if (_fileSystemWatcher is not null) _fileSystemWatcher.Dispose();

        // 确保目录存在
        if (!Directory.Exists(_worldPath))
            Directory.CreateDirectory(_worldPath);

        _fileSystemWatcher = new FileSystemWatcher();
        _fileSystemWatcher.Path = _worldPath;
        _fileSystemWatcher.IncludeSubdirectories = false;
        _fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite;

        _fileSystemWatcher.Created += OnFileSystemChanged;
        _fileSystemWatcher.Deleted += OnFileSystemChanged;
        _fileSystemWatcher.Renamed += OnFileSystemChanged;

        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        _fileSystemRefreshTimer.Stop();
        _fileSystemRefreshTimer.Start();
    }

    private void FileSystemRefreshTimer_Tick(object sender, EventArgs e)
    {
        _fileSystemRefreshTimer.Stop();
        Common.RunInUi(() => Reload(), true);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_fileSystemWatcher is not null)
        {
            _fileSystemWatcher.Created -= OnFileSystemChanged;
            _fileSystemWatcher.Deleted -= OnFileSystemChanged;
            _fileSystemWatcher.Renamed -= OnFileSystemChanged;
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher = null;
        }

        _fileSystemRefreshTimer.Stop();
        _searchTimer.Stop();
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public void Reload()
    {
        Animations.AniBeginControl();
        PanBack.ScrollToHome();
        LoadFileList();
        Animations.AniEndControl();
    }

    private void RefreshUI()
    {
        try
        {
            if (IsSearching)
            {
                var resultCount = _searchResult is null ? 0 : _searchResult.Count;
                PanListBack.Title = Lang.Text("Instance.Saves.SearchResultTitle", resultCount.ToString());
            }
            else
            {
                PanListBack.Title = Lang.Text("Instance.Saves.SaveListTitle",
                    _saveFolders.Count.ToString());
            }

            if (_saveFolders.Count == 0)
            {
                PanNoWorld.Visibility = Visibility.Visible;
                PanContent.Visibility = Visibility.Collapsed;
                PanNoWorld.UpdateLayout();
            }
            else
            {
                PanNoWorld.Visibility = Visibility.Collapsed;
                PanContent.Visibility = Visibility.Visible;
                PanContent.UpdateLayout();

                var showingSaves = (IsSearching ? _searchResult : _saveFolders).ToList();

                if (showingSaves.Any())
                {
                    var sortMethod = GetSortMethod(_currentSortMethod);
                    showingSaves.Sort((a, b) => sortMethod(a, b));
                }

                Animations.AniBeginControl();
                PanList.Children.Clear();

                foreach (var curFolder in showingSaves)
                {
                    // 检查文件夹是否仍然存在
                    if (!Directory.Exists(curFolder)) continue;

                    var saveLogo = Path.Combine(curFolder, "icon.png");
                    var tmpCurFolder = curFolder;
                    if (File.Exists(saveLogo))
                    {
                        var target =
                            $@"{PageInstanceLeft.mcInstance.PathInstance}PCL\ImgCache\{Common.GetStringMd5(saveLogo)}.png";
                        Common.CopyFile(saveLogo, target);
                        saveLogo = target;
                    }
                    else
                    {
                        saveLogo = Common.PathImage + "Icons/NoIcon.png";
                    }

                    var worldItem = new MyListItem
                    {
                        Logo = saveLogo,
                        Title = GetFolderNameFromPath(curFolder),
                        Info =
                            Lang.Text("Instance.Saves.CreationTime", Lang.Date(Directory.GetCreationTime(curFolder), "d"), Lang.Date(Directory.GetLastWriteTime(curFolder), "d")),
                        Type = MyListItem.CheckType.Clickable
                    };
                    worldItem.Click += (_, _) => Launcher.frmMain.PageChange(
                        new FormMain.PageStackData
                        {
                            page = FormMain.PageType.VersionSaves,
                            additional = (null, null, null, Comp.CompLoaderType.Any,
                                Comp.CompType.Any, tmpCurFolder)
                        });

                    var btnOpen = new MyIconButton
                    {
                        SvgIcon = "lucide/folder-open",
                        ToolTip = Lang.Text("Common.Action.Open")
                    };
                    btnOpen.Click += (_, _) => Common.OpenExplorer(tmpCurFolder);
                    var btnDelete = new MyIconButton
                    {
                        SvgIcon = "lucide/trash-2",
                        ToolTip = Lang.Text("Common.Action.Delete")
                    };
                    btnDelete.Click += (_, _) =>
                    {
                        worldItem.IsEnabled = false;
                        worldItem.Info = Lang.Text("Instance.Saves.Deleting");
                        Common.RunInNewThread(() =>
                        {
                            try
                            {
                                FileSystem.DeleteDirectory(tmpCurFolder, UIOption.OnlyErrorDialogs,
                                    RecycleOption.SendToRecycleBin);
                                HintService.Hint(Lang.Text("Instance.Saves.DeletedToRecycleBin"));
                                Common.RunInUiWait(() => RemoveItem(worldItem));
                            }
                            catch (Exception ex)
                            {
                                Common.Log(
                                    ex,
                                    Lang.Text("Instance.Saves.DeleteFailed"),
                                    Common.LogLevel.Hint,
                                    userSummary: Lang.Text("Instance.Saves.DeleteFailed"));
                                Common.RunInUiWait(() => Reload());
                            }
                        });
                    };
                    var btnCopy = new MyIconButton
                    {
                        SvgIcon = "lucide/copy",
                        ToolTip = Lang.Text("Common.Action.Copy")
                    };
                    btnCopy.Click += (_, _) =>
                    {
                        try
                        {
                            if (Directory.Exists(tmpCurFolder))
                            {
                                Clipboard.SetFileDropList(new StringCollection { tmpCurFolder });
                                HintService.Hint(Lang.Text("Instance.Saves.CopiedToClipboard"));
                                HintService.Hint(Lang.Text("Instance.Saves.CopyPasteWarning"));
                            }
                            else
                            {
                                HintService.Hint(Lang.Text("Instance.Saves.FolderNotFound"));
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.Log(
                                ex,
                                Lang.Text("Instance.Saves.CopyFailed"),
                                Common.LogLevel.Hint,
                                userSummary: Lang.Text("Instance.Saves.CopyFailed"));
                        }
                    };
                    var btnInfo = new MyIconButton
                    {
                        SvgIcon = "lucide/info",
                        ToolTip = Lang.Text("Instance.Saves.Details")
                    };
                    btnInfo.Click += (_, _) => Launcher.frmMain.PageChange(
                        new FormMain.PageStackData
                        {
                            page = FormMain.PageType.VersionSaves,
                            additional = (null, null, null, Comp.CompLoaderType.Any,
                                Comp.CompType.Any, tmpCurFolder)
                        });

                    var btnLaunch = new MyIconButton
                    {
                        SvgIcon = "lucide/play",
                        ToolTip = Lang.Text("Instance.Saves.QuickPlay")
                    };
                    btnLaunch.Click += (_, _) =>
                    {
                        var worldName = GetFileNameFromPath(tmpCurFolder);
                        var launchOptions = new GameLaunch.McLaunchOptions
                        {
                            WorldName = worldName,
                            instance = PageInstanceLeft.mcInstance
                        };
                        GameLaunch.McLaunchStart(launchOptions);
                        Launcher.frmMain.PageChange(new FormMain.PageStackData
                            { page = FormMain.PageType.Launch });
                    };
                    if ((bool)_quickPlayFeature)
                        worldItem.Buttons = new[] { btnOpen, btnDelete, btnCopy, btnInfo, btnLaunch };
                    else
                        worldItem.Buttons = new[] { btnOpen, btnDelete, btnCopy, btnInfo };

                    PanList.Children.Add(worldItem);
                }

                Animations.AniEndControl();
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Instance.Saves.RefreshUiFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.RefreshUiFailed"));
        }
    }

    private void CheckQuickPlay()
    {
        try
        {
            var cur = new GameLaunch.LaunchArgument(PageInstanceLeft.mcInstance);
            _quickPlayFeature = cur.HasArguments("--quickPlaySingleplayer");
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "检查存档快捷启动失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    private void LoadFileList()
    {
        try
        {
            Common.Log("[World] 刷新存档文件");
            _saveFolders.Clear();
            if (Directory.Exists(_worldPath))
                _saveFolders = Directory.EnumerateDirectories(_worldPath).ToList();
            else
                _saveFolders = new List<string>();

            if (Common.modeDebug)
                Common.Log("[World] 共发现 " + _saveFolders.Count + " 个存档文件夹", Common.LogLevel.Debug);
            PanList.Children.Clear();
            CheckQuickPlay();

            if (Common.modeDebug)
            {
                if ((bool)_quickPlayFeature)
                    Common.Log("[World] 该实例支持存档快捷启动", Common.LogLevel.Debug);
                else
                    Common.Log("[World] 该实例不支持存档快捷启动", Common.LogLevel.Debug);
            }

            RefreshUI(); // 确保UI刷新
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Instance.Saves.LoadListFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.LoadListFailed"));
        }
    }

    private void RemoveItem(MyListItem item)
    {
        if (PanList.Children.IndexOf(item) == -1)
            return;
        PanList.Children.Remove(item);
        RefreshUI();
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        Common.OpenExplorer(_worldPath);
    }

    private void BtnPaste_Click(object sender, MouseButtonEventArgs e)
    {
        var files = Clipboard.GetFileDropList();
        var loaders = new List<Loading.LoaderBase>();
        loaders.Add(new Loading.LoaderTask<int, int>("Copy saves", _ =>
        {
            var copied = 0;
            foreach (var i in files)
                try
                {
                    if (Directory.Exists(i))
                    {
                        if (Directory.Exists(_worldPath + GetFolderNameFromPath(i)))
                        {
                            HintService.Hint(Lang.Text("Instance.Saves.DuplicateFolder", GetFolderNameFromPath(i)));
                        }
                        else
                        {
                            Common.CopyDirectory(i, _worldPath + GetFolderNameFromPath(i));
                            copied += 1;
                        }
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Instance.Saves.SourceNotFolder"));
                    }
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        Lang.Text("Instance.Saves.PasteFolderFailed"),
                        Common.LogLevel.Hint,
                        userSummary: Lang.Text("Instance.Saves.PasteFolderFailed"));
                }

            if (copied > 0)
                HintService.Hint(Lang.Text("Instance.Saves.PastedCount", copied.ToString()), HintType.Success);
            Common.RunInUi(() => Reload());
        }));
        var loader = new Loading.LoaderCombo<int>(
                $"{PageInstanceLeft.mcInstance.Name} - {Lang.Text("Instance.Saves.CopySave")}",
                loaders)
            { OnStateChanged = DownloadUi.LoaderStateChangedHintOnly };
        loader.Start(1);
        Loading.LoaderTaskbarAdd(loader);
        Launcher.frmMain.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
    }

    private void BtnDownloadNew_Click(object sender, MouseButtonEventArgs e)
    {
        Launcher.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadWorld);
        PageComp.targetVersion = PageInstanceLeft.mcInstance; // 将当前实例设置为筛选器
    }

    #region 搜索和排序

    private SortMethod _currentSortMethod = SortMethod.FileName;
    private List<string> _searchResult;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchBox.Text);

    private enum SortMethod
    {
        FileName,
        CreateTime,
        ModifyTime
    }

    private string GetSortName(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return Lang.Text("Instance.Saves.SortFileName");
            }
            case SortMethod.CreateTime:
            {
                return Lang.Text("Instance.Saves.SortCreateTime");
            }
            case SortMethod.ModifyTime:
            {
                return Lang.Text("Instance.Saves.SortModifyTime");
            }

            default:
            {
                return Lang.Text("Instance.Saves.SortFileName");
            }
        }
    }

    private void SetSortMethod(SortMethod target)
    {
        _currentSortMethod = target;
        BtnSort.Text = Lang.Text("Instance.Saves.SortBy", GetSortName(target));
        RefreshUI();
    }

    private void BtnSortClick(object sender, EventArgs e)
    {
        var body = new ContextMenu();
        foreach (SortMethod i in Enum.GetValues(typeof(SortMethod)))
        {
            var item = new MyMenuItem();
            item.Header = GetSortName(i);
            item.Click += (_, _) => SetSortMethod(i);
            body.Items.Add(item);
        }

        body.PlacementTarget = (UIElement)sender;
        body.Placement = PlacementMode.Bottom;
        body.IsOpen = true;
    }

    private void SearchRun(object sender, EventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object sender, EventArgs e)
    {
        _searchTimer.Stop();
        PerformSearch();
    }

    private void PerformSearch()
    {
        try
        {
            if (IsSearching)
            {
                var queryList = new List<Common.SearchEntry<string>>();
                foreach (var saveFolder in _saveFolders)
                {
                    var folderName = GetFolderNameFromPath(saveFolder);
                    var searchSource = new List<Common.SearchSource>();
                    searchSource.Add(new Common.SearchSource(folderName));
                    queryList.Add(new Common.SearchEntry<string>
                        { item = saveFolder, searchSource = searchSource });
                }

                _searchResult = Common
                    .Search(queryList, SearchBox.Text, Common.MaxLocalSearchDepth, 0.35d)
                    .Select(r => r.item).ToList();
            }
            else
            {
                _searchResult = null;
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Instance.Saves.SearchError"));
        }
    }

    private Func<string, string, int> GetSortMethod(SortMethod method)
    {
        switch (method)
        {
            case SortMethod.FileName:
            {
                return (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
            case SortMethod.CreateTime:
            {
                return (a, b) => Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a));
            }
            case SortMethod.ModifyTime:
            {
                return (a, b) => Directory.GetLastWriteTime(b).CompareTo(Directory.GetLastWriteTime(a));
            }

            default:
            {
                return (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    #endregion
}

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
    private readonly DispatcherTimer fileSystemRefreshTimer;
    private readonly DispatcherTimer searchTimer;
    private FileSystemWatcher fileSystemWatcher;
    private bool IsLoad;

    private object QuickPlayFeature = false;

    private List<string> saveFolders = new();
    private string WorldPath;

    public PageInstanceSaves()
    {
        InitializeComponent();
        fileSystemRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += Page_Unloaded;
        fileSystemRefreshTimer.Tick += FileSystemRefreshTimer_Tick;
        searchTimer.Tick += SearchTimer_Tick;
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
        if (ModMain.FrmInstanceSaves is not null)
            ModMain.FrmInstanceSaves.Reload();
        ModMain.FrmInstanceLeft.ItemWorld.Checked = true;
        ModMain.Hint("正在刷新……", Log: false);
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        WorldPath = PageInstanceLeft.Instance.PathIndie + @"saves\";
        if (!Directory.Exists(WorldPath))
            Directory.CreateDirectory(WorldPath);
        Reload();

        // 非重复加载部分
        if (IsLoad)
            return;
        IsLoad = true;
        CheckQuickPlay();

        // 初始化文件系统监视器和排序按钮
        SetupFileSystemWatcher();
        BtnSort.Click += BtnSortClick;
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
        if (fileSystemWatcher is not null) fileSystemWatcher.Dispose();

        // 确保目录存在
        if (!Directory.Exists(WorldPath))
            Directory.CreateDirectory(WorldPath);

        fileSystemWatcher = new FileSystemWatcher();
        fileSystemWatcher.Path = WorldPath;
        fileSystemWatcher.IncludeSubdirectories = false;
        fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite;

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
        ModBase.RunInUi(() => Reload(), true);
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
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public void Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        PanBack.ScrollToHome();
        LoadFileList();
        ModAnimation.AniControlEnabled -= 1;
    }

    private void RefreshUI()
    {
        try
        {
            if (IsSearching)
            {
                var resultCount = _searchResult is null ? 0 : _searchResult.Count;
                PanListBack.Title = $"搜索结果 ({resultCount})";
            }
            else
            {
                PanListBack.Title = $"存档列表 ({saveFolders.Count})";
            }

            if (saveFolders.Count == 0)
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

                var showingSaves = (IsSearching ? _searchResult : saveFolders).ToList();

                if (showingSaves.Any())
                {
                    var sortMethod = GetSortMethod(_currentSortMethod);
                    showingSaves.Sort((a, b) => sortMethod(a, b));
                }

                ModAnimation.AniControlEnabled += 1;
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
                            $@"{PageInstanceLeft.Instance.PathInstance}PCL\ImgCache\{ModBase.GetStringMD5(saveLogo)}.png";
                        ModBase.CopyFile(saveLogo, target);
                        saveLogo = target;
                    }
                    else
                    {
                        saveLogo = ModBase.PathImage + "Icons/NoIcon.png";
                    }

                    var worldItem = new MyListItem
                    {
                        Logo = saveLogo,
                        Title = GetFolderNameFromPath(curFolder),
                        Info =
                            $"创建时间：{Lang.Date(Directory.GetCreationTime(curFolder), "d")}，最后修改时间：{Lang.Date(Directory.GetLastWriteTime(curFolder), "d")}",
                        Type = MyListItem.CheckType.Clickable
                    };
                    worldItem.Click += (_, _) => ModMain.FrmMain.PageChange(new FormMain.PageStackData
                        { Page = FormMain.PageType.VersionSaves, Additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, null, null, tmpCurFolder) });

                    var BtnOpen = new MyIconButton
                    {
                        Logo = ModBase.Logo.IconButtonOpen,
                        ToolTip = Lang.Text("Common.Action.Open")
                    };
                    BtnOpen.Click += (_, _) => ModBase.OpenExplorer(tmpCurFolder);
                    var BtnDelete = new MyIconButton
                    {
                        Logo = ModBase.Logo.IconButtonDelete,
                        ToolTip = Lang.Text("Common.Action.Delete")
                    };
                    BtnDelete.Click += (_, _) =>
                    {
                        worldItem.IsEnabled = false;
                        worldItem.Info = "删除中……";
                        ModBase.RunInNewThread(() =>
                        {
                            try
                            {
                                FileSystem.DeleteDirectory(tmpCurFolder, UIOption.OnlyErrorDialogs,
                                    RecycleOption.SendToRecycleBin);
                                ModMain.Hint("已将存档移至回收站！");
                                ModBase.RunInUiWait(() => RemoveItem(worldItem));
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "删除存档失败！", ModBase.LogLevel.Hint);
                                ModBase.RunInUiWait(() => Reload());
                            }
                        });
                    };
                    var BtnCopy = new MyIconButton
                    {
                        Logo = ModBase.Logo.IconButtonCopy,
                        ToolTip = Lang.Text("Common.Action.Copy")
                    };
                    BtnCopy.Click += (_, _) =>
                    {
                        try
                        {
                            if (Directory.Exists(tmpCurFolder))
                            {
                                Clipboard.SetFileDropList(new StringCollection { tmpCurFolder });
                                ModMain.Hint("已复制存档文件夹到剪贴板！");
                                ModMain.Hint("注意！在粘贴之前进行删除操作会导致存档丢失！");
                            }
                            else
                            {
                                ModMain.Hint("存档文件夹不存在！");
                            }
                        }
                        catch (Exception ex)
                        {
                            ModBase.Log(ex, "复制失败……", ModBase.LogLevel.Hint);
                        }
                    };
                    var BtnInfo = new MyIconButton
                    {
                        Logo = ModBase.Logo.IconButtonInfo,
                        ToolTip = "详情"
                    };
                    BtnInfo.Click += (_, _) => ModMain.FrmMain.PageChange(new FormMain.PageStackData
                        { Page = FormMain.PageType.VersionSaves, Additional = (null, null, null, ModComp.CompLoaderType.Any, ModComp.CompType.Any, null, null, tmpCurFolder) });

                    var BtnLaunch = new MyIconButton
                    {
                        Logo = ModBase.Logo.IconPlayGame,
                        ToolTip = "快捷启动"
                    };
                    BtnLaunch.Click += (_, _) =>
                    {
                        var WorldName = GetFileNameFromPath(tmpCurFolder);
                        var LaunchOptions = new ModLaunch.McLaunchOptions
                        {
                            WorldName = WorldName,
                            Instance = PageInstanceLeft.Instance
                        };
                        ModLaunch.McLaunchStart(LaunchOptions);
                        ModMain.FrmMain.PageChange(new FormMain.PageStackData { Page = FormMain.PageType.Launch });
                    };
                    if ((bool)QuickPlayFeature)
                        worldItem.Buttons = new[] { BtnOpen, BtnDelete, BtnCopy, BtnInfo, BtnLaunch };
                    else
                        worldItem.Buttons = new[] { BtnOpen, BtnDelete, BtnCopy, BtnInfo };

                    PanList.Children.Add(worldItem);
                }

                ModAnimation.AniControlEnabled -= 1;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新存档UI失败", ModBase.LogLevel.Hint);
        }
    }

    private void CheckQuickPlay()
    {
        try
        {
            var cur = new ModLaunch.LaunchArgument(PageInstanceLeft.Instance);
            QuickPlayFeature = cur.HasArguments("--quickPlaySingleplayer");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检查存档快捷启动失败", ModBase.LogLevel.Hint);
        }
    }

    private void LoadFileList()
    {
        try
        {
            ModBase.Log("[World] 刷新存档文件");
            saveFolders.Clear();
            if (Directory.Exists(WorldPath))
                saveFolders = Directory.EnumerateDirectories(WorldPath).ToList();
            else
                saveFolders = new List<string>();

            if (ModBase.ModeDebug)
                ModBase.Log("[World] 共发现 " + saveFolders.Count + " 个存档文件夹", ModBase.LogLevel.Debug);
            PanList.Children.Clear();
            CheckQuickPlay();

            if (ModBase.ModeDebug)
            {
                if ((bool)QuickPlayFeature)
                    ModBase.Log("[World] 该实例支持存档快捷启动", ModBase.LogLevel.Debug);
                else
                    ModBase.Log("[World] 该实例不支持存档快捷启动", ModBase.LogLevel.Debug);
            }

            RefreshUI(); // 确保UI刷新
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "载入存档列表失败", ModBase.LogLevel.Hint);
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
        ModBase.OpenExplorer(WorldPath);
    }

    private void BtnPaste_Click(object sender, MouseButtonEventArgs e)
    {
        var files = Clipboard.GetFileDropList();
        var loaders = new List<ModLoader.LoaderBase>();
        loaders.Add(new ModLoader.LoaderTask<int, int>("Copy saves", _ =>
        {
            var Copied = 0;
            foreach (var i in files)
                try
                {
                    if (Directory.Exists(i))
                    {
                        if (Directory.Exists(WorldPath + GetFolderNameFromPath(i)))
                        {
                            ModMain.Hint("发现同名文件夹，无法粘贴：" + GetFolderNameFromPath(i));
                        }
                        else
                        {
                            ModBase.CopyDirectory(i, WorldPath + GetFolderNameFromPath(i));
                            Copied += 1;
                        }
                    }
                    else
                    {
                        ModMain.Hint("源文件夹不存在或源目标不是文件夹");
                    }
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "粘贴存档文件夹失败", ModBase.LogLevel.Hint);
                }

            if (Copied > 0)
                ModMain.Hint("已粘贴 " + Copied + " 个文件夹", ModMain.HintType.Finish);
            ModBase.RunInUi(() => Reload());
        }));
        var loader = new ModLoader.LoaderCombo<int>($"{PageInstanceLeft.Instance.Name} - 复制存档", loaders)
            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        loader.Start(1);
        ModLoader.LoaderTaskbarAdd(loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
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
                return "文件名";
            }
            case SortMethod.CreateTime:
            {
                return "创建时间";
            }
            case SortMethod.ModifyTime:
            {
                return "修改时间";
            }

            default:
            {
                return "文件名";
            }
        }
    }

    private void SetSortMethod(SortMethod target)
    {
        _currentSortMethod = target;
        BtnSort.Text = $"排序：{GetSortName(target)}";
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
        searchTimer.Stop();
        searchTimer.Start();
    }

    private void SearchTimer_Tick(object sender, EventArgs e)
    {
        searchTimer.Stop();
        PerformSearch();
    }

    private void PerformSearch()
    {
        try
        {
            if (IsSearching)
            {
                var queryList = new List<ModBase.SearchEntry<string>>();
                foreach (var saveFolder in saveFolders)
                {
                    var folderName = GetFolderNameFromPath(saveFolder);
                    var searchSource = new List<ModBase.SearchSource>();
                    searchSource.Add(new ModBase.SearchSource(folderName, 1d));
                    queryList.Add(new ModBase.SearchEntry<string> { Item = saveFolder, SearchSource = searchSource });
                }

                _searchResult = ModBase.Search(queryList, SearchBox.Text, 6, 0.35d).Select(r => r.Item).ToList();
            }
            else
            {
                _searchResult = null;
            }

            RefreshUI();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "搜索过程中发生异常");
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

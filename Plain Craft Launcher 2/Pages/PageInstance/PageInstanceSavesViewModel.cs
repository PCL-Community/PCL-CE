using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>存档列表页的视图模型。</summary>
public partial class PageInstanceSavesViewModel : ViewModelBase
{
    [ObservableProperty] private string _listTitle = "";

    [ObservableProperty] private string _searchText = "";

    [ObservableProperty] private bool _showContent = true;

    [ObservableProperty] private bool _showNoWorld;

    [ObservableProperty] private string _sortText = "";

    private readonly List<string> _saveFolders = [];

    private object _quickPlayFeature = false;

    private SaveSortMethod _sortMethod = SaveSortMethod.FileName;

    public PageInstanceSavesViewModel()
    {
        ListTitle = Lang.Text("Instance.Saves.ListTitle");
        SortText = Lang.Text("Instance.Saves.SortBy");
    }

    public ObservableCollection<SaveEntry> Saves { get; } = [];

    public string WorldPath => PageInstanceLeft.McInstance.PathIndie + @"saves\";

    public SaveSortMethod SortMethod => _sortMethod;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public bool QuickPlayEnabled => _quickPlayFeature is true;

    public async Task EnsureWorldFolderAsync()
    {
        var path = WorldPath;
        await RunAsync(ct => Task.Run(() =>
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }, ct));
    }

    public void CheckQuickPlay()
    {
        try
        {
            var cur = new GameLaunch.LaunchArgument(PageInstanceLeft.McInstance);
            _quickPlayFeature = cur.HasArguments("--quickPlaySingleplayer");
        }
        catch (Exception ex)
        {
            Common.Log(ex, "检查存档快捷启动失败", Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    public async Task LoadListAsync()
    {
        CheckQuickPlay();
        await ApplyListAsync(true);
    }

    public async Task SearchAsync()
    {
        await ApplyListAsync(false);
    }

    public string GetSortName(SaveSortMethod method)
    {
        return method switch
        {
            SaveSortMethod.CreateTime => Lang.Text("Instance.Saves.SortCreateTime"),
            SaveSortMethod.ModifyTime => Lang.Text("Instance.Saves.SortModifyTime"),
            _ => Lang.Text("Instance.Saves.SortFileName")
        };
    }

    public void SetSortMethod(SaveSortMethod target)
    {
        _sortMethod = target;
        SortText = Lang.Text("Instance.Saves.SortBy", GetSortName(target));
    }

    public async Task ApplySortAsync(SaveSortMethod target)
    {
        SetSortMethod(target);
        await SearchAsync();
    }

    public async Task<bool> DeleteSaveAsync(string folder)
    {
        var deleted = await RunAsync(ct => Task.Run(() =>
        {
            try
            {
                FileSystem.DeleteDirectory(folder, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                return true;
            }
            catch (Exception ex)
            {
                Common.Log(ex, Lang.Text("Instance.Saves.DeleteFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.DeleteFailed"));
                return false;
            }
        }, ct));
        return deleted is true;
    }

    public static int CopySaveFolders(StringCollection files, string worldPath)
    {
        var copied = 0;
        foreach (var i in files)
            try
            {
                if (Directory.Exists(i))
                {
                    if (Directory.Exists(worldPath + GetFolderNameFromPath(i)))
                    {
                        HintService.Hint(Lang.Text("Instance.Saves.DuplicateFolder", GetFolderNameFromPath(i)));
                    }
                    else
                    {
                        Common.CopyDirectory(i, worldPath + GetFolderNameFromPath(i));
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
                Common.Log(ex, Lang.Text("Instance.Saves.PasteFolderFailed"), Common.LogLevel.Hint,
                    userSummary: Lang.Text("Instance.Saves.PasteFolderFailed"));
            }

        return copied;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Common.OpenExplorer(WorldPath);
    }

    [RelayCommand]
    private void OpenSaveFolder(string? folder)
    {
        if (folder is null)
            return;
        Common.OpenExplorer(folder);
    }

    [RelayCommand]
    private void OpenDetails(string? folder)
    {
        if (folder is null)
            return;
        Launcher.frmMain.PageChange(new FormMain.PageStackData
        {
            page = FormMain.PageType.VersionSaves,
            additional = (null, null, null, Comp.CompLoaderType.Any, Comp.CompType.Any, folder)
        });
    }

    [RelayCommand]
    private void CopySave(string? folder)
    {
        if (folder is null)
            return;
        try
        {
            if (Directory.Exists(folder))
            {
                Clipboard.SetFileDropList(new StringCollection { folder });
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
            Common.Log(ex, Lang.Text("Instance.Saves.CopyFailed"), Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Saves.CopyFailed"));
        }
    }

    [RelayCommand]
    private void LaunchSave(string? folder)
    {
        if (folder is null)
            return;
        var launchOptions = new GameLaunch.McLaunchOptions
        {
            WorldName = Path.GetFileName(folder),
            instance = PageInstanceLeft.McInstance
        };
        GameLaunch.McLaunchStart(launchOptions);
        Launcher.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
    }

    [RelayCommand]
    private void DownloadNew()
    {
        Launcher.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadWorld);
        PageComp.targetVersion = PageInstanceLeft.McInstance;
    }

    private async Task ApplyListAsync(bool rescan)
    {
        var snapshot = await RunAsync(ct => Task.Run(() => BuildEntries(rescan, SearchText), ct));
        if (snapshot is null)
            return;

        Saves.Clear();
        foreach (var entry in snapshot.Entries)
            Saves.Add(entry);

        ListTitle = IsSearching
            ? Lang.Text("Instance.Saves.SearchResultTitle", snapshot.SearchCount.ToString())
            : Lang.Text("Instance.Saves.SaveListTitle", snapshot.FolderCount.ToString());
        ShowNoWorld = snapshot.FolderCount == 0;
        ShowContent = snapshot.FolderCount != 0;
    }

    private SaveListSnapshot BuildEntries(bool rescan, string query)
    {
        var path = WorldPath;
        var searching = !string.IsNullOrWhiteSpace(query);

        if (rescan)
        {
            Common.Log("[World] 刷新存档文件");
            _saveFolders.Clear();
            if (Directory.Exists(path))
                _saveFolders.AddRange(Directory.EnumerateDirectories(path));
            if (Common.modeDebug)
                Common.Log("[World] 共发现 " + _saveFolders.Count + " 个存档文件夹", Common.LogLevel.Debug);
            if (Common.modeDebug)
                Common.Log(_quickPlayFeature is true ? "[World] 该实例支持存档快捷启动" : "[World] 该实例不支持存档快捷启动",
                    Common.LogLevel.Debug);
        }

        List<string> showingSaves;
        if (searching)
        {
            var queryList = new List<Common.SearchEntry<string>>();
            foreach (var saveFolder in _saveFolders)
            {
                var folderName = GetFolderNameFromPath(saveFolder);
                var searchSource = new List<Common.SearchSource> { new(folderName, 1d) };
                queryList.Add(new Common.SearchEntry<string> { item = saveFolder, searchSource = searchSource });
            }

            showingSaves = Common.Search(queryList, query, Common.MaxLocalSearchDepth, 0.35d)
                .Select(r => r.item).ToList();
        }
        else
        {
            showingSaves = [.. _saveFolders];
        }

        if (showingSaves.Count > 1)
        {
            var compare = GetComparer(_sortMethod);
            showingSaves.Sort((a, b) => compare(a, b));
        }

        var result = new List<SaveEntry>();
        foreach (var curFolder in showingSaves)
        {
            if (!Directory.Exists(curFolder))
                continue;
            result.Add(BuildEntry(curFolder));
        }

        return new SaveListSnapshot(result, showingSaves.Count, _saveFolders.Count);
    }

    private static SaveEntry BuildEntry(string curFolder)
    {
        var saveLogo = Path.Combine(curFolder, "icon.png");
        if (File.Exists(saveLogo))
        {
            var target =
                $@"{PageInstanceLeft.McInstance.PathInstance}PCL\ImgCache\{Common.GetStringMd5(saveLogo)}.png";
            Common.CopyFile(saveLogo, target);
            saveLogo = target;
        }
        else
        {
            saveLogo = Common.PathImage + "Icons/NoIcon.png";
        }

        var info = Lang.Text("Instance.Saves.CreationTime",
            Lang.Date(Directory.GetCreationTime(curFolder), "d"),
            Lang.Date(Directory.GetLastWriteTime(curFolder), "d"));
        return new SaveEntry(curFolder, GetFolderNameFromPath(curFolder), info, saveLogo);
    }

    private static Func<string, string, int> GetComparer(SaveSortMethod method)
    {
        return method switch
        {
            SaveSortMethod.CreateTime => (a, b) =>
                Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a)),
            SaveSortMethod.ModifyTime => (a, b) =>
                Directory.GetLastWriteTime(b).CompareTo(Directory.GetLastWriteTime(a)),
            _ => (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b),
                StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string GetFolderNameFromPath(string fullPath)
    {
        return string.IsNullOrEmpty(fullPath)
            ? ""
            : fullPath.EndsWith(@"\")
                ? new DirectoryInfo(fullPath).Parent?.Name
                : new DirectoryInfo(fullPath).Name;
    }

    private sealed class SaveListSnapshot(List<SaveEntry> entries, int searchCount, int folderCount)
    {
        public List<SaveEntry> Entries { get; } = entries;

        public int SearchCount { get; } = searchCount;

        public int FolderCount { get; } = folderCount;
    }
}

/// <summary>存档列表中的一条存档。</summary>
public class SaveEntry(string folderPath, string title, string info, string logo)
{
    public string FolderPath { get; } = folderPath;

    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Logo { get; } = logo;
}

/// <summary>存档列表的排序方式。</summary>
public enum SaveSortMethod
{
    FileName,
    CreateTime,
    ModifyTime
}

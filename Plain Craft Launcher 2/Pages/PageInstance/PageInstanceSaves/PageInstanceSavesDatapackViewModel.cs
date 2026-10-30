using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Network;
using PCL.Network.Loaders;
using FileSystem = Microsoft.VisualBasic.FileSystem;

namespace PCL;

/// <summary>数据包管理页的视图模型。</summary>
public partial class PageInstanceSavesDatapackViewModel : ViewModelBase
{
    public enum DatapackFilterType
    {
        All = 0,
        Enabled = 1,
        Disabled = 2,
        CanUpdate = 3,
        Unavailable = 4
    }

    public enum DatapackSortMethod
    {
        FileName,
        CompName,
        CreateTime,
        DatapackFileSize
    }

    public sealed record DatapackToggleOutcome(
        List<(ModLocalComp.LocalCompFile Old, ModLocalComp.LocalCompFile New)> Replaced,
        bool IsSuccessful,
        bool Aborted);

    public sealed record DatapackDeleteOutcome(
        List<ModLocalComp.LocalCompFile> Deleted,
        bool IsSuccessful,
        bool Canceled,
        Exception? Fatal);

    [ObservableProperty] private DatapackFilterType _filter = DatapackFilterType.All;

    [ObservableProperty] private DatapackSortMethod _sortMethod = DatapackSortMethod.CompName;

    [ObservableProperty] private string _sortText = "";

    [ObservableProperty] private bool _showDatapacks = true;

    [ObservableProperty] private bool _hasVisibleEntries;

    [ObservableProperty] private string _filterAllText = "";

    [ObservableProperty] private string _filterCanUpdateText = "";

    [ObservableProperty] private string _filterEnabledText = "";

    [ObservableProperty] private string _filterDisabledText = "";

    [ObservableProperty] private string _filterErrorText = "";

    [ObservableProperty] private bool _showFilterCanUpdate;

    [ObservableProperty] private bool _showFilterEnabled;

    [ObservableProperty] private bool _showFilterDisabled;

    [ObservableProperty] private bool _showFilterError;

    [ObservableProperty] private string _selectedText = "";

    [ObservableProperty] private int _selectedCount;

    [ObservableProperty] private bool _canUpdate = true;

    [ObservableProperty] private bool _canEnable = true;

    [ObservableProperty] private bool _canDisable = true;

    [ObservableProperty] private bool _canFavoriteAndShare = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching))]
    private string _searchText = "";

    private readonly Dictionary<string, (DateTime CreationTime, long Length)> _fileInfoCache = new();
    private readonly List<ModLocalComp.LocalCompFile> _visibleEntries = [];
    private List<ModLocalComp.LocalCompFile>? _searchResult;

    public PageInstanceSavesDatapackViewModel()
    {
        SortText = Lang.Text("Instance.Resource.Sort.Text", GetSortName(DatapackSortMethod.CompName));
    }

    public static List<string> UpdatingVersions { get; } = new();

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    public HashSet<string> SelectedDatapacks { get; } = new();

    public IReadOnlyList<ModLocalComp.LocalCompFile> VisibleEntries => _visibleEntries;

    public static string GetDatapackFolderPath()
    {
        return Path.Combine(PageInstanceSavesLeft.currentSave, "datapacks");
    }

    public void SetSearchResult(List<ModLocalComp.LocalCompFile>? result)
    {
        _searchResult = result;
    }

    public void Apply()
    {
        var output = ModLocalComp.compResourceListLoader.output;
        if (output is null)
        {
            _visibleEntries.Clear();
            HasVisibleEntries = false;
            SelectedCount = SelectedDatapacks.Count;
            return;
        }

        var source = IsSearching && _searchResult is not null ? _searchResult : output;
        _visibleEntries.Clear();
        foreach (var entry in source)
            if (CanPassFilter(entry))
                _visibleEntries.Add(entry);

        if (_visibleEntries.Count > 0)
        {
            var compare = GetSortMethod(SortMethod);
            _visibleEntries.Sort((a, b) => compare(a, b));
        }

        SelectedDatapacks.RemoveWhere(rawPath =>
            _visibleEntries.All(entry => (entry.RawPath ?? "") != (rawPath ?? "")));
        HasVisibleEntries = _visibleEntries.Count > 0;
        RefreshBarState();
    }

    public void RefreshBarState()
    {
        var output = ModLocalComp.compResourceListLoader.output;
        if (output is null)
        {
            SelectedCount = SelectedDatapacks.Count;
            return;
        }

        var itemSource = (IsSearching && _searchResult is not null ? _searchResult : output).ToArray();
        var anyCount = itemSource.Length;
        var enabledCount = itemSource.Count(item => item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine);
        var disabledCount = itemSource.Count(item => item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled);
        var updateCount = itemSource.Count(item => item.CanUpdate);
        var unavailableCount =
            itemSource.Count(item => item.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable);

        FilterAllText = IsSearching
            ? Lang.Text("Instance.Resource.Filter.SearchResult")
            : Lang.Text("Instance.Resource.Filter.AllWithCount", anyCount);
        FilterCanUpdateText = Lang.Text("Instance.Resource.Filter.UpdatableWithCount", updateCount);
        ShowFilterCanUpdate = Filter == DatapackFilterType.CanUpdate || updateCount > 0;
        FilterEnabledText = Lang.Text("Instance.Resource.Filter.EnabledWithCount", enabledCount);
        ShowFilterEnabled = Filter == DatapackFilterType.Enabled || (enabledCount > 0 && enabledCount < anyCount);
        FilterDisabledText = Lang.Text("Instance.Resource.Filter.DisabledWithCount", disabledCount);
        ShowFilterDisabled = Filter == DatapackFilterType.Disabled || disabledCount > 0;
        FilterErrorText = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavailableCount);
        ShowFilterError = Filter == DatapackFilterType.Unavailable || unavailableCount > 0;

        SelectedCount = SelectedDatapacks.Count;
        if (SelectedCount <= 0)
            return;

        SelectedText = Lang.Text("Instance.Resource.SelectedCount", SelectedCount);
        var hasUpdate = false;
        var hasEnabled = false;
        var hasDisabled = false;
        var canFavoriteAndShare = true;
        foreach (var datapackEntity in output)
        {
            if (!SelectedDatapacks.Contains(datapackEntity.RawPath))
                continue;
            if (datapackEntity.CanUpdate) hasUpdate = true;
            if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine)
                hasEnabled = true;
            else if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled)
                hasDisabled = true;
            if (datapackEntity.Comp is null || string.IsNullOrEmpty(datapackEntity.Comp.Id))
                canFavoriteAndShare = false;
        }

        CanDisable = hasEnabled;
        CanEnable = hasDisabled;
        CanUpdate = hasUpdate;
        CanFavoriteAndShare = canFavoriteAndShare;
    }

    public bool CanPassFilter(ModLocalComp.LocalCompFile checkingDatapack)
    {
        switch (Filter)
        {
            case DatapackFilterType.All:
            {
                return true;
            }
            case DatapackFilterType.Enabled:
            {
                return checkingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine;
            }
            case DatapackFilterType.Disabled:
            {
                return checkingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled;
            }
            case DatapackFilterType.CanUpdate:
            {
                return checkingDatapack.CanUpdate;
            }
            case DatapackFilterType.Unavailable:
            {
                return checkingDatapack.State == ModLocalComp.LocalCompFile.LocalFileStatus.Unavailable;
            }

            default:
            {
                return false;
            }
        }
    }

    public void SetSortMethod(DatapackSortMethod method)
    {
        SortMethod = method;
        SortText = Lang.Text("Instance.Resource.Sort.Text", GetSortName(method));
    }

    public string GetSortName(DatapackSortMethod method)
    {
        switch (method)
        {
            case DatapackSortMethod.FileName:
            {
                return Lang.Text("Instance.Resource.Sort.FileName");
            }
            case DatapackSortMethod.CompName:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
            case DatapackSortMethod.CreateTime:
            {
                return Lang.Text("Instance.Resource.Sort.AddTime");
            }
            case DatapackSortMethod.DatapackFileSize:
            {
                return Lang.Text("Instance.Resource.Sort.FileSize");
            }

            default:
            {
                return Lang.Text("Instance.Resource.Sort.ResourceName");
            }
        }
    }

    public Func<ModLocalComp.LocalCompFile, ModLocalComp.LocalCompFile, int> GetSortMethod(DatapackSortMethod method)
    {
        switch (method)
        {
            case DatapackSortMethod.FileName:
            {
                return (a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);
            }
            case DatapackSortMethod.CompName:
            {
                return (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
            case DatapackSortMethod.CreateTime:
            {
                return (a, b) =>
                {
                    var aDate = GetDatapackFileInfo(a.path).CreationTime;
                    var bDate = GetDatapackFileInfo(b.path).CreationTime;
                    if (aDate == DateTime.MinValue && bDate == DateTime.MinValue)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                    if (aDate == DateTime.MinValue) return 1;

                    if (bDate == DateTime.MinValue) return -1;
                    return bDate.CompareTo(aDate);
                };
            }
            case DatapackSortMethod.DatapackFileSize:
            {
                return (a, b) =>
                {
                    var aSize = GetDatapackFileInfo(a.path).Length;
                    var bSize = GetDatapackFileInfo(b.path).Length;
                    if (aSize == 0L && bSize == 0L)
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

                    if (aSize == 0L) return 1;

                    if (bSize == 0L) return -1;
                    return bSize.CompareTo(aSize);
                };
            }

            default:
            {
                return (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public (DateTime CreationTime, long Length) GetDatapackFileInfo(string path)
    {
        if (_fileInfoCache.TryGetValue(path, out var cacheItem)) return cacheItem;

        try
        {
            var fileInfo = new FileInfo(path);
            var newItem = (fileInfo.CreationTime, fileInfo.Length);
            if (!_fileInfoCache.ContainsKey(path)) _fileInfoCache.Add(path, newItem);
            return newItem;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取数据包信息失败: " + path);
            return (DateTime.MinValue, 0L);
        }
    }

    public void ClearFileInfoCache()
    {
        _fileInfoCache.Clear();
    }

    public async Task EnsureFileInfoAsync(IEnumerable<string> paths)
    {
        var pathList = paths.Where(path => path is not null).Distinct().ToList();
        await RunAsync(ct => Task.Run(() =>
        {
            foreach (var path in pathList)
                GetDatapackFileInfo(path);
        }, ct));
    }

    public void ClearSelection()
    {
        SelectedDatapacks.Clear();
    }

    public void SetSelected(string rawPath, bool value)
    {
        if (value)
            SelectedDatapacks.Add(rawPath);
        else
            SelectedDatapacks.Remove(rawPath);
    }

    public static async Task InstallFilesAsync(IReadOnlyList<string> filePathList)
    {
        if (filePathList.Count == 0)
            return;

        try
        {
            var extension = filePathList[0].AfterLast(".").ToLower();
            if (extension != "zip")
            {
                HintService.Hint(
                    Lang.Text("Instance.Resource.Install.UnsupportedFormat", extension,
                        Lang.Text("Download.Comp.Type.DataPack"), "zip"), HintType.Error);
                return;
            }

            if (filePathList[0].Contains(@":\$RECYCLE.BIN\"))
            {
                HintService.Hint(Lang.Text("Instance.Resource.Install.RestoreFromRecycleBin"), HintType.Error);
                return;
            }

            ModBase.Log($"[System] 文件为 {extension} 格式，尝试作为数据包安装");

            if (!(ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                  ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack))
                if (1 != await ModMain.MyMsgBoxAsync(
                        Lang.Text("Instance.Saves.Datapack.Install.Message"),
                        Lang.Text("Instance.Saves.Datapack.Install.Title"), Lang.Text("Common.Action.Confirm"),
                        Lang.Text("Common.Action.Cancel")))
                    return;

            var datapackFolder = GetDatapackFolderPath();
            var copyList = new List<(string Source, string Destination)>();
            foreach (var filePath in filePathList)
            {
                var newFileName = ModBase.GetFileNameFromPath(filePath);
                var destFile = datapackFolder + newFileName;

                if (File.Exists(destFile))
                    if (1 != await ModMain.MyMsgBoxAsync(
                            Lang.Text("Instance.Resource.Install.OverwriteConfirm.Message", newFileName),
                            Lang.Text("Instance.Resource.Install.OverwriteConfirm.Title"),
                            Lang.Text("Common.Action.Overwrite"), Lang.Text("Common.Action.Cancel")))
                        continue;

                copyList.Add((filePath, destFile));
            }

            await Task.Run(() =>
            {
                Directory.CreateDirectory(datapackFolder);
                foreach (var (source, destination) in copyList)
                    ModBase.CopyFile(source, destination);
            });

            if (filePathList.Count == 1)
                HintService.Hint(
                    Lang.Text("Instance.Resource.Install.SuccessSingle",
                        ModBase.GetFileNameFromPath(filePathList[0])), HintType.Success);
            else
                HintService.Hint(
                    Lang.Text("Instance.Resource.Install.SuccessMultiple", filePathList.Count,
                        Lang.Text("Download.Comp.Type.DataPack")), HintType.Success);

            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.VersionSavesDatapack)
                ModMain.frmInstanceSavesDatapack?.ReloadDatapackFileList(true);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "复制数据包文件失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
        }
    }

    public async Task<DatapackToggleOutcome> ToggleDatapacksAsync(
        IEnumerable<ModLocalComp.LocalCompFile> datapackList, bool isEnable)
    {
        var replaced = new List<(ModLocalComp.LocalCompFile Old, ModLocalComp.LocalCompFile New)>();
        var isSuccessful = true;
        try
        {
            foreach (var datapackEntity in datapackList.ToList())
            {
                string? newPath;
                if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine && !isEnable)
                    newPath = datapackEntity.path + ".disabled";
                else if (datapackEntity.State == ModLocalComp.LocalCompFile.LocalFileStatus.Disabled && isEnable)
                    newPath = datapackEntity.RawPath;
                else
                    continue;

                try
                {
                    if (File.Exists(newPath))
                    {
                        await ModMain.MyMsgBoxAsync(
                            Lang.Text("Instance.Saves.Datapack.Replace.FileNameConflict",
                                ModBase.GetFileNameFromPath(newPath)));
                        continue;
                    }

                    FileSystem.Rename(datapackEntity.path, newPath);
                }
                catch (FileNotFoundException ex)
                {
                    ModBase.Log(
                        ex,
                        $"未找到需要重命名的数据包（{datapackEntity.path ?? "null"}）",
                        ModBase.LogLevel.Feedback,
                        userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
                    return new DatapackToggleOutcome(replaced, isSuccessful, true);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"重命名数据包失败（{datapackEntity.path ?? "null"}）");
                    isSuccessful = false;
                }

                var newDatapackEntity = new ModLocalComp.LocalCompFile(newPath);
                newDatapackEntity.FromJson(datapackEntity.ToJson());
                var output = ModLocalComp.compResourceListLoader.output;
                if (output.Contains(datapackEntity))
                {
                    var indexOfLoader = output.IndexOf(datapackEntity);
                    output.RemoveAt(indexOfLoader);
                    output.Insert(indexOfLoader, newDatapackEntity);
                }

                if (_searchResult is not null && _searchResult.Contains(datapackEntity))
                {
                    var indexOfResult = _searchResult.IndexOf(datapackEntity);
                    _searchResult.Remove(datapackEntity);
                    _searchResult.Insert(indexOfResult, newDatapackEntity);
                }

                replaced.Add((datapackEntity, newDatapackEntity));
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "切换数据包启用状态失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
            return new DatapackToggleOutcome(replaced, false, true);
        }

        return new DatapackToggleOutcome(replaced, isSuccessful, false);
    }

    public async Task<DatapackDeleteOutcome> DeleteFilesAsync(
        IEnumerable<ModLocalComp.LocalCompFile> datapackList, bool isShiftPressed)
    {
        var deleted = new List<ModLocalComp.LocalCompFile>();
        var isSuccessful = true;
        var targets = new List<ModLocalComp.LocalCompFile>();
        Exception? failure = null;
        await RunAsync(ct => Task.Run(() =>
        {
            try
            {
                targets = datapackList.SelectMany(target =>
                        target.State == ModLocalComp.LocalCompFile.LocalFileStatus.Fine
                            ? new[] { target.path, target.path + ".disabled" }
                            : new[] { target.path, target.RawPath })
                    .Distinct()
                    .Where(File.Exists)
                    .Select(path => new ModLocalComp.LocalCompFile(path))
                    .ToList();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }, ct));

        if (failure is not null)
            return new DatapackDeleteOutcome(deleted, false, false, failure);

        foreach (var datapackEntity in targets)
        {
            try
            {
                if (isShiftPressed)
                    File.Delete(datapackEntity.path);
                else
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(datapackEntity.path,
                        UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch (OperationCanceledException ex)
            {
                ModBase.Log(ex, "删除数据包被主动取消");
                return new DatapackDeleteOutcome(deleted, isSuccessful, true, null);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"删除数据包失败（{datapackEntity.path}）",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Instance.Saves.Error.OperationFailed"));
                isSuccessful = false;
            }

            SelectedDatapacks.Remove(datapackEntity.RawPath);
            ModLocalComp.compResourceListLoader.output.Remove(datapackEntity);
            _searchResult?.Remove(datapackEntity);
            deleted.Add(datapackEntity);
        }

        return new DatapackDeleteOutcome(deleted, isSuccessful, false, null);
    }

    public async Task<bool> UpdateAsync(IEnumerable<ModLocalComp.LocalCompFile> datapackList)
    {
        try
        {
            var list = datapackList.ToList();
            var fileList = new List<DownloadFile>();
            var fileCopyList = new Dictionary<string, string>();
            var updateEntryList = new List<ModLocalComp.LocalCompFile>();
            var tempRoot = Path.Combine(ModBase.pathTemp, "DownloadedComp");
            var datapackRoot = GetDatapackFolderPath();
            var skippedUnsafeFileCount = 0;
            foreach (var Entry in list)
            {
                var file = Entry.UpdateFile;
                if (!file.Available)
                    continue;
                if (!TryGetSafeDatapackUpdateFileName(file, out var safeFileName) ||
                    !TryBuildDatapackUpdatePath(tempRoot, safeFileName, out var tempAddress) ||
                    !TryBuildDatapackUpdatePath(datapackRoot, safeFileName, out var realAddress))
                {
                    skippedUnsafeFileCount++;
                    ModBase.Log($"[DatapackUpdate] 已跳过不安全的数据包更新文件名：{file.FileName}", ModBase.LogLevel.Debug);
                    continue;
                }

                fileList.Add(file.ToNetFile(tempAddress, ModComp.DownloadReason.Update,
                    file.RawGameVersions.FirstOrDefault()));
                fileCopyList[tempAddress] = realAddress;
                updateEntryList.Add(Entry);
            }

            if (skippedUnsafeFileCount > 0)
                HintService.Hint(
                    Lang.Text("Instance.Saves.Datapack.Update.UnsafeFilesSkipped", skippedUnsafeFileCount),
                    HintType.Error);
            if (!fileList.Any())
                return false;

            var installLoaders = new List<ModLoader.LoaderBase>();
            var finishedFileNames = new List<string>();
            installLoaders.Add(
                new LoaderDownload(Lang.Text("Instance.Saves.Datapack.Update.Task.DownloadFiles"), fileList)
                    { ProgressWeight = updateEntryList.Count * 1.5d });

            installLoaders.Add(new ModLoader.LoaderTask<int, int>(
                Lang.Text("Instance.Saves.Datapack.Update.Task.ReplaceFiles"), _ =>
                {
                    try
                    {
                        foreach (var Entry in updateEntryList)
                            if (File.Exists(Entry.path))
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.path, UIOption.AllDialogs,
                                    RecycleOption.SendToRecycleBin);
                            else
                                ModBase.Log($"[DatapackUpdate] 未找到更新前的数据包文件，跳过对它的删除：{Entry.path}",
                                    ModBase.LogLevel.Debug);

                        foreach (var Entry in fileCopyList)
                        {
                            if (File.Exists(Entry.Value))
                            {
                                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(Entry.Value, UIOption.AllDialogs,
                                    RecycleOption.SendToRecycleBin);
                                ModBase.Log($"[Datapack] 更新后的数据包文件已存在，将会把它放入回收站：{Entry.Value}",
                                    ModBase.LogLevel.Debug);
                            }

                            if (Directory.Exists(ModBase.GetPathFromFullPath(Entry.Value)))
                            {
                                File.Move(Entry.Key, Entry.Value);
                                finishedFileNames.Add(ModBase.GetFileNameFromPath(Entry.Value));
                            }
                            else
                            {
                                ModBase.Log($"[Datapack] 更新后的目标文件夹已被删除：{Entry.Value}", ModBase.LogLevel.Debug);
                            }
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        ModBase.Log(ex, "替换旧版数据包文件时被主动取消");
                    }
                }));

            var loader = new ModLoader.LoaderCombo<IEnumerable<ModLocalComp.LocalCompFile>>(
                Lang.Text("Instance.Saves.Datapack.Update.Task.Title",
                    ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.currentSave)), installLoaders);
            var pathDatapacks = GetDatapackFolderPath();

            loader.OnStateChanged = _ =>
            {
                switch (loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        switch (finishedFileNames.Count)
                        {
                            case 0:
                            {
                                ModBase.Log("[DatapackUpdate] 没有数据包被成功更新");
                                break;
                            }
                            case 1:
                            {
                                HintService.Hint(
                                    Lang.Text("Instance.Resource.Update.SuccessSingle", finishedFileNames.Single()),
                                    HintType.Success);
                                break;
                            }

                            default:
                            {
                                HintService.Hint(
                                    Lang.Text("Instance.Resource.Update.SuccessMultiple", finishedFileNames.Count),
                                    HintType.Success);
                                break;
                            }
                        }

                        break;
                    }
                    case ModBase.LoadState.Failed:
                    {
                        HintService.Hint(Lang.Text("Instance.Resource.Update.Failed", loader.Error.Message),
                            HintType.Error);
                        break;
                    }
                    case ModBase.LoadState.Aborted:
                    {
                        HintService.Hint(Lang.Text("Instance.Resource.Update.Aborted"));
                        break;
                    }

                    default:
                    {
                        return;
                    }
                }

                ModBase.Log($"[DatapackUpdate] 已从正在进行数据包更新的文件夹列表移除：{pathDatapacks}");
                UpdatingVersions.Remove(pathDatapacks);

                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        foreach (var TempFile in fileCopyList.Keys)
                            if (File.Exists(TempFile))
                                File.Delete(TempFile);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "清理数据包更新缓存失败");
                    }
                }, "Clean Datapack Update Cache", ThreadPriority.BelowNormal);
            };

            ModBase.Log($"[DatapackUpdate] 开始更新 {list.Count()} 个数据包：{pathDatapacks}");
            UpdatingVersions.Add(pathDatapacks);
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化数据包更新失败");
            return false;
        }
    }

    public string BuildExportContent(bool isCsv)
    {
        var exportContent = new List<string>();
        if (!isCsv)
        {
            foreach (var datapackEntity in ModLocalComp.compResourceListLoader.output)
                exportContent.Add(datapackEntity.FileName);
            return string.Join("\r\n", exportContent);
        }

        exportContent.Add("文件名,数据包名称,数据包版本,此版本更新时间,工程 ID,文件大小（字节）,文件路径");
        foreach (var datapackEntity in ModLocalComp.compResourceListLoader.output)
            exportContent.Add(
                $"{datapackEntity.FileName},{datapackEntity.Comp?.TranslatedName},{datapackEntity.Version},{datapackEntity.compFile?.ReleaseDate},{datapackEntity.Comp?.Id},{GetDatapackFileInfo(datapackEntity.path).Length},{datapackEntity.path}");
        return string.Join("\r\n", exportContent);
    }

    public async Task WriteExportFileAsync(string savePath, string content)
    {
        await RunAsync(ct => Task.Run(() => File.WriteAllText(savePath, content, Encoding.UTF8), ct));
    }

    private static bool TryGetSafeDatapackUpdateFileName(ModComp.CompFile file, out string fileName)
    {
        fileName = file.FileName?.Trim() ?? "";
        if (string.IsNullOrEmpty(fileName))
            return false;

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return false;

        if (fileName.IndexOfAny(new[] { '\\', '/', ':' }) >= 0)
            return false;

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return false;

        return fileName == Path.GetFileName(fileName) && fileName != "." && fileName != "..";
    }

    private static bool TryBuildDatapackUpdatePath(string rootPath, string fileName, out string fullPath)
    {
        var fullRootPath = Path.GetFullPath(rootPath);
        if (!fullRootPath.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
            !fullRootPath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            fullRootPath += Path.DirectorySeparatorChar;

        fullPath = Path.GetFullPath(Path.Combine(fullRootPath, fileName));
        return fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase);
    }
}

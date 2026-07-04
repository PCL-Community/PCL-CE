// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceSavesRight : MyPageRight
{
    private readonly DispatcherTimer _fileSystemRefreshTimer;
    private readonly DispatcherTimer _searchTimer;
    private FileSystemWatcher? _fileSystemWatcher;
    private bool _isLoad;
    private bool _quickPlayFeature;
    private List<string> _saveFolders = [];
    private string _worldPath = string.Empty;
    private SortMethod _currentSortMethod = SortMethod.FileName;
    private List<string>? _searchResult;
    private LaunchInstanceInfo? _instance;

    public PageInstanceSavesRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _fileSystemRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100d) };
        _fileSystemRefreshTimer.Tick += FileSystemRefreshTimer_Tick;
        _searchTimer.Tick += SearchTimer_Tick;
        if (this.FindControl<MySearchBox>("SearchBox") is { } searchBox)
            searchBox.TextChanged += SearchRun;
        if (this.FindControl<MyIconTextButton>("BtnSort") is { } btnSort)
            btnSort.Click += BtnSortClick;
    }

    public event EventHandler<string>? OpenFolderRequested;

    public event EventHandler<string>? SaveDetailsRequested;

    public event EventHandler<string>? QuickPlayRequested;

    public event EventHandler<string>? StatusMessage;

    public bool IsSearching => !string.IsNullOrWhiteSpace(this.FindControl<MySearchBox>("SearchBox")?.Text);

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        _worldPath = Path.Combine(GetMinecraftRootFromInstance(instance), "saves");
        if (!Directory.Exists(_worldPath))
            Directory.CreateDirectory(_worldPath);

        if (PanScroll is not null)
            PanScroll.ScrollToHome();
        Reload();

        if (_isLoad)
            return;

        _isLoad = true;
        CheckQuickPlay();
        SetupFileSystemWatcher();
        SetSortMethod(_currentSortMethod);
    }

    public override void Dispose()
    {
        base.Dispose();
        DisposeFileSystemWatcher();
        _fileSystemRefreshTimer.Stop();
        _searchTimer.Stop();
        GC.SuppressFinalize(this);
    }

    public void Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        try
        {
            if (PanScroll is not null)
                PanScroll.ScrollToHome();
            LoadFileList();
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static string GetFolderNameFromPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return string.Empty;

        string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmed);
    }

    private static string GetFileNameFromPath(string fullPath) =>
        Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private void SetupFileSystemWatcher()
    {
        DisposeFileSystemWatcher();
        if (!Directory.Exists(_worldPath))
            Directory.CreateDirectory(_worldPath);

        _fileSystemWatcher = new FileSystemWatcher
        {
            Path = _worldPath,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        _fileSystemWatcher.Created += OnFileSystemChanged;
        _fileSystemWatcher.Deleted += OnFileSystemChanged;
        _fileSystemWatcher.Renamed += OnFileSystemChanged;
        _fileSystemWatcher.EnableRaisingEvents = true;
    }

    private void DisposeFileSystemWatcher()
    {
        if (_fileSystemWatcher is null)
            return;

        _fileSystemWatcher.Created -= OnFileSystemChanged;
        _fileSystemWatcher.Deleted -= OnFileSystemChanged;
        _fileSystemWatcher.Renamed -= OnFileSystemChanged;
        _fileSystemWatcher.Dispose();
        _fileSystemWatcher = null;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _fileSystemRefreshTimer.Stop();
            _fileSystemRefreshTimer.Start();
        });
    }

    private void FileSystemRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _fileSystemRefreshTimer.Stop();
        Reload();
    }

    private void RefreshUI()
    {
        if (this.FindControl<MyCard>("PanListBack") is { } listBack)
        {
            listBack.Title = IsSearching
                ? Text("Instance.Saves.SearchResultTitle", _searchResult?.Count.ToString(CultureInfo.CurrentCulture) ?? "0")
                : Text("Instance.Saves.SaveListTitle", _saveFolders.Count.ToString(CultureInfo.CurrentCulture));
        }

        bool isEmpty = _saveFolders.Count == 0;
        if (this.FindControl<Control>("PanNoWorld") is { } noWorld)
            noWorld.IsVisible = isEmpty;
        if (this.FindControl<Control>("PanContent") is { } content)
            content.IsVisible = !isEmpty;

        if (isEmpty || this.FindControl<StackPanel>("PanList") is not { } list)
            return;

        List<string> showingSaves = (IsSearching ? _searchResult ?? [] : _saveFolders).ToList();
        if (showingSaves.Count > 0)
        {
            Comparison<string> sortMethod = GetSortMethod(_currentSortMethod);
            showingSaves.Sort(sortMethod);
        }

        ModAnimation.AniControlEnabled += 1;
        try
        {
            list.Children.Clear();
            foreach (string curFolder in showingSaves)
            {
                if (!Directory.Exists(curFolder))
                    continue;

                list.Children.Add(CreateSaveItem(curFolder));
            }
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private MyListItem CreateSaveItem(string curFolder)
    {
        string saveLogo = Path.Combine(curFolder, "icon.png");
        if (!File.Exists(saveLogo))
            saveLogo = "pack://application:,,,/images/Icons/NoIcon.png";

        MyListItem worldItem = new()
        {
            Logo = saveLogo,
            Title = GetFolderNameFromPath(curFolder),
            Info = Text(
                "Instance.Saves.CreationTime",
                Directory.GetCreationTime(curFolder).ToString("d", CultureInfo.CurrentCulture),
                Directory.GetLastWriteTime(curFolder).ToString("d", CultureInfo.CurrentCulture)),
            Type = MyListItem.CheckType.Clickable
        };
        worldItem.Click += (_, _) => SaveDetailsRequested?.Invoke(this, curFolder);

        MyIconButton btnOpen = new()
        {
            SvgIcon = "lucide/folder-open",
            ToolTip = Text("Common.Action.Open")
        };
        btnOpen.Click += (_, _) => OpenFolderRequested?.Invoke(this, curFolder);

        MyIconButton btnDelete = new()
        {
            SvgIcon = "lucide/trash-2",
            ToolTip = Text("Common.Action.Delete")
        };
        btnDelete.Click += (_, _) => _ = DeleteSaveAsync(worldItem, curFolder);

        MyIconButton btnCopy = new()
        {
            SvgIcon = "lucide/copy",
            ToolTip = Text("Common.Action.Copy")
        };
        btnCopy.Click += (_, _) => _ = CopySaveFolderAsync(curFolder);

        MyIconButton btnInfo = new()
        {
            SvgIcon = "lucide/info",
            ToolTip = Text("Instance.Saves.Details")
        };
        btnInfo.Click += (_, _) => SaveDetailsRequested?.Invoke(this, curFolder);

        if (_quickPlayFeature)
        {
            MyIconButton btnLaunch = new()
            {
                SvgIcon = "lucide/play",
                ToolTip = Text("Instance.Saves.QuickPlay")
            };
            btnLaunch.Click += (_, _) => QuickPlayRequested?.Invoke(this, GetFileNameFromPath(curFolder));
            worldItem.Buttons = [btnOpen, btnDelete, btnCopy, btnInfo, btnLaunch];
        }
        else
        {
            worldItem.Buttons = [btnOpen, btnDelete, btnCopy, btnInfo];
        }

        return worldItem;
    }

    private void CheckQuickPlay()
    {
        if (_instance is null)
        {
            _quickPlayFeature = false;
            return;
        }

        string json = _instance.VersionJsonPath;
        _quickPlayFeature = File.Exists(json) &&
                            string.Compare(_instance.Name, "1.20", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void LoadFileList()
    {
        try
        {
            _saveFolders = Directory.Exists(_worldPath)
                ? Directory.EnumerateDirectories(_worldPath).ToList()
                : [];
            if (this.FindControl<StackPanel>("PanList") is { } list)
                list.Children.Clear();
            CheckQuickPlay();
            RefreshUI();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.LoadListFailed"));
        }
    }

    private void RemoveItem(MyListItem item)
    {
        if (this.FindControl<StackPanel>("PanList") is not { } list || !list.Children.Contains(item))
            return;

        list.Children.Remove(item);
        RefreshUI();
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        if (!Directory.Exists(_worldPath))
            Directory.CreateDirectory(_worldPath);
        OpenFolderRequested?.Invoke(this, _worldPath);
    }

    private void BtnPaste_Click(object? sender, EventArgs e)
    {
        _ = PasteSavesAsync();
    }

    private async Task DeleteSaveAsync(MyListItem worldItem, string folder)
    {
        worldItem.IsEnabled = false;
        worldItem.Info = Text("Instance.Saves.Deleting");
        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }).ConfigureAwait(true);
            _saveFolders.Remove(folder);
            RemoveItem(worldItem);
            StatusMessage?.Invoke(this, Text("Instance.Saves.DeletedToRecycleBin"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.DeleteFailed"));
            Reload();
        }
    }

    private async Task CopySaveFolderAsync(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                StatusMessage?.Invoke(this, Text("Instance.Saves.FolderNotFound"));
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            IClipboard? clipboard = topLevel?.Clipboard;
            IStorageProvider? storage = topLevel?.StorageProvider;
            if (clipboard is null || storage is null)
                throw new InvalidOperationException("Clipboard is not available.");

            IStorageFolder? storageFolder = await storage.TryGetFolderFromPathAsync(folder).ConfigureAwait(true);
            if (storageFolder is null)
                throw new InvalidOperationException("Folder is not available.");

            await clipboard.SetFilesAsync([storageFolder]).ConfigureAwait(true);
            StatusMessage?.Invoke(this, Text("Instance.Saves.CopiedToClipboard"));
            StatusMessage?.Invoke(this, Text("Instance.Saves.CopyPasteWarning"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.CopyFailed"));
        }
    }

    private async Task PasteSavesAsync()
    {
        try
        {
            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                throw new InvalidOperationException("Clipboard is not available.");

            IStorageItem[]? files = await clipboard.TryGetFilesAsync().ConfigureAwait(true);
            int copied = 0;
            foreach (IStorageItem item in files ?? [])
            {
                string? source = item.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
                {
                    StatusMessage?.Invoke(this, Text("Instance.Saves.SourceNotFolder"));
                    continue;
                }

                string folderName = GetFolderNameFromPath(source);
                string target = Path.Combine(_worldPath, folderName);
                if (Directory.Exists(target))
                {
                    StatusMessage?.Invoke(this, Text("Instance.Saves.DuplicateFolder", folderName));
                    continue;
                }

                await Task.Run(() => CopyDirectory(source, target)).ConfigureAwait(true);
                copied += 1;
            }

            if (copied > 0)
                StatusMessage?.Invoke(this, Text("Instance.Saves.PastedCount", copied.ToString(CultureInfo.CurrentCulture)));
            Reload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.PasteFolderFailed"));
        }
    }

    private void BtnSortClick(object? sender, EventArgs e)
    {
        if (sender is not Control button)
            return;

        ContextMenu body = new();
        foreach (SortMethod method in Enum.GetValues<SortMethod>())
        {
            SortMethod captured = method;
            MyMenuItem item = new()
            {
                Header = GetSortName(method)
            };
            item.Click += (_, _) => SetSortMethod(captured);
            body.Items.Add(item);
        }

        body.Open(button);
    }

    private void SearchRun(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        PerformSearch();
    }

    private void PerformSearch()
    {
        try
        {
            string query = this.FindControl<MySearchBox>("SearchBox")?.Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                _searchResult = _saveFolders
                    .Where(saveFolder => GetFolderNameFromPath(saveFolder).Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                _searchResult = null;
            }

            RefreshUI();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Saves.SearchError"));
        }
    }

    private string GetSortName(SortMethod method) =>
        method switch
        {
            SortMethod.FileName => Text("Instance.Saves.SortFileName"),
            SortMethod.CreateTime => Text("Instance.Saves.SortCreateTime"),
            SortMethod.ModifyTime => Text("Instance.Saves.SortModifyTime"),
            _ => Text("Instance.Saves.SortFileName")
        };

    private void SetSortMethod(SortMethod target)
    {
        _currentSortMethod = target;
        if (this.FindControl<MyIconTextButton>("BtnSort") is { } btnSort)
            btnSort.Text = Text("Instance.Saves.SortBy", GetSortName(target));
        RefreshUI();
    }

    private static Comparison<string> GetSortMethod(SortMethod method) =>
        method switch
        {
            SortMethod.FileName => (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b), StringComparison.OrdinalIgnoreCase),
            SortMethod.CreateTime => (a, b) => Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a)),
            SortMethod.ModifyTime => (a, b) => Directory.GetLastWriteTime(b).CompareTo(Directory.GetLastWriteTime(a)),
            _ => (a, b) => string.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b), StringComparison.OrdinalIgnoreCase)
        };

    private string Text(string key, params string[] args)
    {
        string value = TryGetResource(key, null, out object? resource) && resource is string text
            ? text
            : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            string targetFile = Path.Combine(target, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: false);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo? versionsDirectory = versionDirectory.Parent;
        if (versionsDirectory?.Parent is not null &&
            string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
        {
            return versionsDirectory.Parent.FullName;
        }

        return instance.InstanceDirectory;
    }

    private enum SortMethod
    {
        FileName,
        CreateTime,
        ModifyTime
    }
}

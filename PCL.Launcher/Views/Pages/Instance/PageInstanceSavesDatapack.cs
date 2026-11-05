using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;
using PCL.Views.Shell;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档数据包页：列出存档 <c>datapacks</c> 目录下的数据包文件与目录。
///     支持搜索、按启用状态/可读性筛选、排序、全选与批量启用/禁用/删除、从文件安装与下载新数据包；
///     删除优先移入系统回收站，回收站不可用时明确提示永久删除。
/// </summary>
public class PageInstanceSavesDatapack : MyPageRight, IRefreshable
{
    private readonly List<DatapackEntry> _entries = [];
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DatapackEntry> _visibleEntries = [];
    private MyRadioButton _btnFilterAll = null!;
    private MyRadioButton _btnFilterDisabled = null!;
    private MyRadioButton _btnFilterEnabled = null!;
    private MyRadioButton _btnFilterError = null!;
    private MyRadioButton _btnFilterUpdatable = null!;
    private MyIconTextButton _btnSelectDelete = null!;
    private MyIconTextButton _btnSelectDisable = null!;
    private MyIconTextButton _btnSelectEnable = null!;
    private MyIconTextButton _btnSort = null!;
    private bool _building;
    private int _filter;
    private TextBlock _labSelect = null!;
    private MyLoading _load = null!;
    private MyCard _panEmpty = null!;
    private StackPanel _panFilter = null!;
    private StackPanel _panList = null!;
    private MyCard _panLoad = null!;

    private StackPanel _panMain = null!;
    private MyCard _panSelect = null!;
    private StackPanel _panSort = null!;
    private string? _saveFolder;
    private string _search = string.Empty;
    private MySearchBox _searchBox = null!;
    private int _sort = 1;

    public PageInstanceSavesDatapack()
    {
        BuildUi();
    }

    private string? DatapacksPath =>
        string.IsNullOrEmpty(_saveFolder) ? null : InstancePageHelper.DatapacksPath(_saveFolder);

    public void Refresh()
    {
        Reload();
    }

    private void BuildUi()
    {
        var root = new Grid();
        var panel = new StackPanel
        {
            Margin = new Thickness(25d, 25d, 25d, 25d)
        };
        var scroll = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = panel
        };
        _panMain = panel;
        PanScroll = scroll;
        root.Children.Add(scroll);

        _searchBox = new MySearchBox
        {
            HintText = Lang.Text("Instance.Resource.Search.Hint"),
            Text = _search
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _search = _searchBox.Text ?? string.Empty;
            BuildList();
        };
        _panMain.Children.Add(_searchBox);

        var manageCard = new MyCard
        {
            Title = Lang.Text("Instance.Saves.Left.Datapack"),
            Margin = new Thickness(0d, 15d, 0d, 15d)
        };
        var manageBody = new WrapPanel
        {
            Margin = new Thickness(15d, 8d, 0d, 15d)
        };
        var refresh = NewButton("Common.Action.Refresh", Reload);
        manageBody.Children.Add(refresh);
        var openFolder = NewButton("Instance.Saves.OpenFolder", OpenFolder);
        manageBody.Children.Add(openFolder);
        var install = NewButton(
            "Instance.Resource.InstallFromFiles",
            () => _ = InstallFromFilesAsync());
        manageBody.Children.Add(install);
        var download = NewButton("Instance.Resource.DownloadNew", DownloadNew);
        download.IsVisible = HiddenFeatureState.ForceShow || !Config.Preference.Hide.PageDownload;
        manageBody.Children.Add(download);
        var selectAll = NewButton("Instance.Resource.SelectAll", ToggleSelectAll);
        manageBody.Children.Add(selectAll);
        var export = NewButton("Instance.Resource.ExportInfo", () => _ = ExportInfoAsync());
        ToolTip.SetTip(export, Lang.Text("Instance.Resource.ExportInfo.ToolTip"));
        manageBody.Children.Add(export);
        manageCard.BorderChild = manageBody;
        _panMain.Children.Add(manageCard);

        var listCard = new MyCard
        {
            MinHeight = 55d,
            Title = " "
        };
        var listContainer = new StackPanel
        {
            Margin = new Thickness(20d, 38d, 18d, 22d),
            Spacing = 2d
        };
        _panFilter = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _btnFilterAll = NewFilterButton("Instance.Resource.Filter.All", 0);
        _btnFilterEnabled = NewFilterButton("Instance.Resource.Filter.Enabled", 1);
        _btnFilterDisabled = NewFilterButton("Instance.Resource.Filter.Disabled", 2);
        _btnFilterUpdatable = NewFilterButton("Instance.Resource.Filter.Updatable", 3);
        _btnFilterError = NewFilterButton("Instance.Resource.Filter.Error", 4);
        _panFilter.Children.Add(_btnFilterAll);
        _panFilter.Children.Add(_btnFilterEnabled);
        _panFilter.Children.Add(_btnFilterDisabled);
        _panFilter.Children.Add(_btnFilterUpdatable);
        _panFilter.Children.Add(_btnFilterError);
        listContainer.Children.Add(_panFilter);

        _panSort = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Height = 28d,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _btnSort = new MyIconTextButton
        {
            Text = Lang.Text(
                "Instance.Resource.Sort.Text",
                Lang.Text("Instance.Resource.Sort.FileName")),
            SvgIcon = "lucide/arrow-up-down"
        };
        _btnSort.Click += (_, _) =>
        {
            var menu = new ContextMenu();
            var names = new[]
            {
                "Instance.Resource.Sort.FileName",
                "Instance.Resource.Sort.ResourceName",
                "Instance.Resource.Sort.AddTime",
                "Instance.Resource.Sort.FileSize"
            };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var item = new MyMenuItem
                {
                    Header = Lang.Text(names[i])
                };
                item.Click += (_, _) =>
                {
                    _sort = index;
                    BuildList();
                };
                menu.Items.Add(item);
            }

            menu.Open(_btnSort);
        };
        _panSort.Children.Add(_btnSort);
        listContainer.Children.Add(_panSort);

        _panList = new StackPanel();
        listContainer.Children.Add(_panList);
        listCard.BorderChild = listContainer;
        _panMain.Children.Add(listCard);

        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false,
            BorderChild = CreateEmptyBody()
        };
        root.Children.Add(_panEmpty);

        _labSelect = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(9d)
        };
        _btnSelectEnable = NewSelectButton(
            "Instance.Resource.Enable",
            "lucide/circle-check",
            () => SetSelectedEnabled(true));
        _btnSelectDisable = NewSelectButton(
            "Instance.Resource.Disable",
            "lucide/circle-minus",
            () => SetSelectedEnabled(false));
        _btnSelectDelete = NewSelectButton(
            "Common.Action.Delete",
            "lucide/trash-2",
            DeleteSelected);
        var cancel = NewSelectButton(
            "Instance.Resource.CancelSelection",
            "lucide/x",
            () =>
            {
                _selected.Clear();
                BuildList();
            });
        var selectButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(5d),
            Spacing = 2d
        };
        selectButtons.Children.Add(_btnSelectEnable);
        selectButtons.Children.Add(_btnSelectDisable);
        selectButtons.Children.Add(_btnSelectDelete);
        selectButtons.Children.Add(cancel);
        var selectBody = new StackPanel
        {
            Margin = new Thickness(6d, 4d, 6d, 6d),
            Spacing = 2d
        };
        selectBody.Children.Add(_labSelect);
        selectBody.Children.Add(selectButtons);
        _panSelect = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(25d, 25d, 25d, 0d),
            UseAnimation = false,
            IsVisible = false,
            BorderChild = selectBody
        };
        root.Children.Add(_panSelect);

        _load = new MyLoading
        {
            Text = Lang.Text("Instance.Resource.Datapack.Loading"),
            Margin = new Thickness(20d, 20d, 20d, 17d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _panLoad = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d, 0d),
            UseAnimation = false,
            IsVisible = false,
            BorderChild = _load
        };
        root.Children.Add(_panLoad);

        Child = root;
        BuildList();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
    }

    private MyRadioButton NewFilterButton(string textKey, int index)
    {
        var button = new MyRadioButton
        {
            Text = Lang.Text(textKey),
            Tag = index.ToString(),
            ColorType = MyRadioButton.ColorState.Highlight,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2d, 0d),
            Checked = index == 0
        };
        button.Check += (sender, _) =>
        {
            if (sender.Tag is string tag && int.TryParse(tag, out var value))
                _filter = value;
            BuildList();
        };
        return button;
    }

    private static MyButton NewButton(string textKey, Action onClick)
    {
        var button = new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 110d,
            Margin = new Thickness(0d, 7d, 15d, 0d),
            Padding = new Thickness(13d, 7d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static MyIconTextButton NewSelectButton(string textKey, string icon, Action onClick)
    {
        var button = new MyIconTextButton
        {
            Text = Lang.Text(textKey),
            SvgIcon = icon,
            LogoScale = 1d
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private Control CreateEmptyBody()
    {
        var grid = new Grid
        {
            Margin = new Thickness(20d, 17d),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,*")
        };
        var title = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Datapack.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        Grid.SetColumnSpan(title, 5);
        grid.Children.Add(title);
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 5);
        grid.Children.Add(divider);
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Datapack.Empty.Description"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        Grid.SetRow(message, 2);
        Grid.SetColumnSpan(message, 5);
        grid.Children.Add(message);
        var openFolder = new MyButton
        {
            Text = Lang.Text("Instance.Saves.OpenFolder"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButton.ColorState.Highlight
        };
        openFolder.Click += (_, _) => OpenFolder();
        Grid.SetRow(openFolder, 3);
        Grid.SetColumn(openFolder, 1);
        grid.Children.Add(openFolder);

        var install = new MyButton
        {
            Text = Lang.Text("Instance.Resource.InstallFromFiles"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        install.Click += (_, _) => _ = InstallFromFilesAsync();
        Grid.SetRow(install, 3);
        Grid.SetColumn(install, 2);
        grid.Children.Add(install);

        var download = new MyButton
        {
            Text = Lang.Text("Instance.Resource.Datapack.DownloadNew"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        download.Click += (_, _) => DownloadNew();
        Grid.SetRow(download, 3);
        Grid.SetColumn(download, 3);
        grid.Children.Add(download);
        return grid;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _saveFolder = parameter as string;
        if (string.IsNullOrEmpty(_saveFolder))
            _saveFolder = PageInstanceSavesLeft.currentSave;
        Reload();
    }

    public void Reload()
    {
        _entries.Clear();
        _selected.Clear();
        var path = DatapacksPath;
        if (path is null)
        {
            UpdateEmptyState();
            return;
        }

        _panLoad.IsVisible = true;
        _panEmpty.IsVisible = false;
        _panMain.IsVisible = false;
        _panSelect.IsVisible = false;
        _load.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _ = LoadAsync(path);
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            InstancePageHelper.EnsureDirectory(path);
            var entries = await Task.Run(() => Directory
                .EnumerateFileSystemEntries(path)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .Select(CreateEntry)
                .ToList());
            foreach (var entry in entries)
                _entries.Add(entry);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Error.OperationFailed", ex);
        }
        finally
        {
            _panLoad.IsVisible = false;
            if (_load.State is MyLoadingStateSimulator simulator)
                simulator.LoadingState = LoadingState.Stop;
        }

        BuildList();
        UpdateEmptyState();
    }

    private static DatapackEntry CreateEntry(string path)
    {
        var name = Path.GetFileName(path);
        var isDirectory = Directory.Exists(path);
        LocalComp.LocalCompFile? file = null;
        if (!isDirectory)
            try
            {
                file = new LocalComp.LocalCompFile(path);
                file.Load();
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, $"读取数据包元数据失败：{path}", LauncherLogLevel.Developer);
            }

        return new DatapackEntry
        {
            Path = path,
            Name = name,
            IsDirectory = isDirectory,
            File = file
        };
    }

    private void BuildList()
    {
        if (_building)
            return;
        _building = true;
        try
        {
            var keyword = (_searchBox.Text ?? string.Empty).Trim();
            var available = _entries
                .Select(entry => entry.RawKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _selected.RemoveWhere(key => !available.Contains(key));

            var filtered = _entries
                .Where(MatchFilter)
                .Where(entry =>
                    keyword.Length == 0
                    || entry.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            filtered = (_sort switch
            {
                1 => filtered.OrderBy(
                    entry => entry.IsDirectory
                        ? string.Empty
                        : entry.File?.Name ?? entry.DisplayName,
                    StringComparer.OrdinalIgnoreCase),
                2 => filtered.OrderByDescending(SafeWriteTime),
                3 => filtered.OrderByDescending(SafeSize),
                _ => filtered.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            }).ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.OrderBy(entry => entry.IsDirectory ? 0 : 1);

            _visibleEntries.Clear();
            var visible = filtered.ToList();
            _visibleEntries.AddRange(visible);
            _panList.Children.Clear();
            foreach (var entry in visible)
                _panList.Children.Add(CreateItem(entry));

            RefreshFilterTexts();
            RefreshSortText();
            RefreshSelectBar();
        }
        finally
        {
            _building = false;
        }
    }

    private bool MatchFilter(DatapackEntry entry)
    {
        return _filter switch
        {
            1 => entry is { Disabled: false, Unavailable: false },
            2 => entry.Disabled,
            3 => entry.CanUpdate,
            4 => entry.Unavailable,
            _ => true
        };
    }

    private void RefreshFilterTexts()
    {
        var updatable = _entries.Count(entry => entry.CanUpdate);
        var unavailable = _entries.Count(entry => entry.Unavailable);
        _btnFilterAll.Text = Lang.Text("Instance.Resource.Filter.AllWithCount", _entries.Count);
        _btnFilterEnabled.Text = Lang.Text(
            "Instance.Resource.Filter.EnabledWithCount",
            _entries.Count(entry => entry is { Disabled: false, Unavailable: false }));
        _btnFilterDisabled.Text = Lang.Text(
            "Instance.Resource.Filter.DisabledWithCount",
            _entries.Count(entry => entry.Disabled));
        _btnFilterUpdatable.Text = Lang.Text(
            "Instance.Resource.Filter.UpdatableWithCount",
            updatable);
        _btnFilterError.Text = Lang.Text("Instance.Resource.Filter.ErrorWithCount", unavailable);
        var enabled = _entries.Count(entry => entry is { Disabled: false, Unavailable: false });
        _btnFilterUpdatable.IsVisible = updatable > 0;
        _btnFilterError.IsVisible = unavailable > 0;
        _btnFilterEnabled.IsVisible = enabled > 0 && enabled < _entries.Count;
        _btnFilterDisabled.IsVisible = _entries.Any(entry => entry.Disabled);
    }

    private void RefreshSortText()
    {
        var key = _sort switch
        {
            1 => "Instance.Resource.Sort.ResourceName",
            2 => "Instance.Resource.Sort.AddTime",
            3 => "Instance.Resource.Sort.FileSize",
            _ => "Instance.Resource.Sort.FileName"
        };
        _btnSort.Text = Lang.Text("Instance.Resource.Sort.Text", Lang.Text(key));
    }

    private void RefreshSelectBar()
    {
        var count = _selected.Count;
        _panSelect.IsVisible = count > 0;
        if (count == 0)
            return;
        _labSelect.Text = Lang.Text("Instance.Resource.SelectedCount", count);
        var selected = _entries.Where(entry => _selected.Contains(entry.RawKey)).ToList();
        _btnSelectEnable.IsEnabled = selected.Any(entry => entry.Disabled);
        _btnSelectDisable.IsEnabled = selected.Any(entry => !entry.Disabled);
        _btnSelectDelete.IsEnabled = selected.Count > 0;
    }

    private void UpdateEmptyState()
    {
        var empty = _entries.Count == 0;
        _panEmpty.IsVisible = empty;
        _panMain.IsVisible = !empty;
        if (empty)
            _panSelect.IsVisible = false;
    }

    private MyListItem CreateItem(DatapackEntry entry)
    {
        var selected = _selected.Contains(entry.RawKey);
        var metadata = (entry.IsDirectory
                ? Lang.Text("Instance.Resource.Item.FolderTag")
                : string.Empty)
            + (string.IsNullOrWhiteSpace(entry.Version) ? string.Empty : $" {entry.Version}")
            + (string.IsNullOrWhiteSpace(entry.Description)
                ? string.Empty
                : $" · {entry.Description}");
        var item = new MyListItem
        {
            Title = entry.File?.Name ?? entry.DisplayName,
            Info = string.IsNullOrWhiteSpace(metadata)
                ? entry.Disabled
                    ? Lang.Text("Instance.Resource.Filter.Disabled")
                    : Lang.Text("Instance.Resource.Filter.Enabled")
                : metadata,
            SvgIcon = entry.IsDirectory ? "lucide/folder" : "lucide/package",
            Type = MyListItem.CheckType.CheckBox,
            Checked = selected,
            tag = entry
        };
        if (entry.Tags.Count > 0)
            item.PlainTags = entry.Tags.Take(4);
        item.Check += (_, _) =>
        {
            if (item.Checked)
                _selected.Add(entry.RawKey);
            else
                _selected.Remove(entry.RawKey);
            RefreshSelectBar();
        };

        var toggle = new MyIconButton
        {
            SvgIcon = entry.Disabled ? "lucide/check" : "lucide/x",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(
            toggle,
            Lang.Text(entry.Disabled ? "Instance.Resource.Enable" : "Instance.Resource.Disable"));
        toggle.Click += (_, _) => Toggle(entry);

        var open = new MyIconButton
        {
            SvgIcon = "lucide/folder-open",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(open, Lang.Text("Instance.Saves.OpenFolder"));
        open.Click += (_, _) =>
            InstancePageHelper.OpenFolder(
                entry.IsDirectory ? entry.Path : Path.GetDirectoryName(entry.Path) ?? entry.Path);

        var delete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            LogoScale = 0.9d
        };
        ToolTip.SetTip(delete, Lang.Text("Common.Action.Delete"));
        delete.Click += (_, _) => _ = DeleteAsync([entry]);

        var buttons = new List<MyIconButton>();
        if (entry.CanUpdate)
        {
            var update = new MyIconButton
            {
                SvgIcon = "lucide/upload",
                LogoScale = 0.9d
            };
            ToolTip.SetTip(update, Lang.Text("Instance.Resource.Update"));
            update.Click += (_, _) => _ = UpdateEntryAsync(entry);
            buttons.Add(update);
        }

        buttons.Add(open);
        buttons.Add(delete);
        item.Buttons = buttons;
        return item;
    }

    private async Task UpdateEntryAsync(DatapackEntry entry)
    {
        if (entry.File is not { UpdateFile: not null, CanUpdate: true } file)
            return;
        try
        {
            var folder = Path.GetDirectoryName(entry.Path) ?? DatapacksPath;
            if (string.IsNullOrEmpty(folder))
                return;
            var update = file.UpdateFile;
            if (!update.Available || update.DownloadUrls.Count == 0)
                return;

            var targetPath = Path.Combine(folder, update.FileName);
            var checker = string.IsNullOrEmpty(update.Hash)
                ? new FileChecker(1024L)
                : new FileChecker(hash: update.Hash);
            var loader = new Loading.LoaderCombo<int>(
                Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                [
                    new LoaderDownload(
                        Lang.Text("Instance.Resource.Update.Task.DownloadFiles"),
                        [new DownloadFile(update.DownloadUrls, targetPath, checker)])
                ])
            {
                OnStateChanged = changed =>
                {
                    switch (changed.State)
                    {
                        case LoadState.Finished:
                        {
                            foreach (var variant in new[]
                                     {
                                         entry.Path,
                                         $"{entry.Path}.disabled",
                                         $"{entry.Path}.old"
                                     })
                                if (File.Exists(variant)
                                    && !string.Equals(
                                        variant,
                                        targetPath,
                                        StringComparison.OrdinalIgnoreCase))
                                    File.Delete(variant);
                            HintService.Show(
                                Hints.Success(
                                    Lang.Text(
                                        "Instance.Resource.Update.SuccessSingle",
                                        entry.DisplayName)));
                            Reload();
                            break;
                        }
                        case LoadState.Failed:
                            HintService.Show(
                                Hints.Error(
                                    Lang.Text(
                                        "Instance.Resource.Update.Failed",
                                        changed.Error?.Message ?? string.Empty)));
                            break;
                    }
                }
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            LoadingUi.Current?.RefreshDownloadButton();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Update.Failed", ex);
        }
    }

    private static DateTime SafeWriteTime(DatapackEntry entry)
    {
        try
        {
            return entry.IsDirectory
                ? Directory.GetLastWriteTime(entry.Path)
                : File.GetLastWriteTime(entry.Path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeSize(DatapackEntry entry)
    {
        try
        {
            return entry.IsDirectory ? 0L : new FileInfo(entry.Path).Length;
        }
        catch
        {
            return 0L;
        }
    }

    private static string CsvEscape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',')
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }

    private void OpenFolder()
    {
        var path = DatapacksPath;
        if (path is null)
            return;
        InstancePageHelper.EnsureDirectory(path);
        InstancePageHelper.OpenFolder(path);
    }

    private sealed class DatapackEntry
    {
        public required string Path { get; init; }

        public required string Name { get; init; }

        public bool IsDirectory { get; init; }

        public LocalComp.LocalCompFile? File { get; init; }

        public bool Disabled => Path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

        public string DisplayName => Disabled ? Name[..^".disabled".Length] : Name;

        public string RawKey => Path;

        public string Version => File?.Version ?? string.Empty;

        public string? Description => File?.Description;

        public IReadOnlyList<string> Tags => File?.Tags ?? [];

        public bool CanUpdate => File is { CanUpdate: true, UpdateFile: not null };

        public bool Unavailable =>
            File?.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
    }

    #region 多选操作

    private void ToggleSelectAll()
    {
        if (_visibleEntries.Count == 0)
            return;
        var allSelected = _visibleEntries.All(entry => _selected.Contains(entry.RawKey));
        foreach (var entry in _visibleEntries)
            if (allSelected)
                _selected.Remove(entry.RawKey);
            else
                _selected.Add(entry.RawKey);

        BuildList();
    }

    private void SetSelectedEnabled(bool enabled)
    {
        var targets = _entries
            .Where(entry => _selected.Contains(entry.RawKey))
            .Where(entry => enabled ? entry.Disabled : !entry.Disabled)
            .ToList();
        foreach (var entry in targets)
            Toggle(entry, false);
        _selected.Clear();
        Reload();
    }

    private void DeleteSelected()
    {
        var targets = _entries.Where(entry => _selected.Contains(entry.RawKey)).ToList();
        if (targets.Count > 0)
            _ = DeleteAsync(targets);
    }

    private void Toggle(DatapackEntry entry, bool reload = true)
    {
        try
        {
            var target = entry.Disabled
                ? entry.Path[..^".disabled".Length]
                : $"{entry.Path}.disabled";
            if (entry.IsDirectory)
                Directory.Move(entry.Path, target);
            else
                File.Move(entry.Path, target);
            if (reload)
                Reload();
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(Lang.Text("Instance.Saves.Datapack.ToggleWarning")));
            LauncherCommon.Log(ex, "切换数据包状态失败", LauncherLogLevel.Feedback);
        }
    }

    private async Task DeleteAsync(List<DatapackEntry> targets)
    {
        try
        {
            var names = string.Join(", ", targets.Select(entry => entry.DisplayName));
            var canRecycle = targets.All(entry => RecycleBin.IsSupported(entry.Path));
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Datapack.Delete.ConfirmTitle"),
                    Content = Lang.Text(
                        canRecycle
                            ? "Instance.Saves.Datapack.Delete.ConfirmMessage"
                            : "Instance.Saves.Datapack.Delete.ConfirmPermanent",
                        names),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            var moved = 0;
            var permanent = 0;
            foreach (var entry in targets)
            {
                var outcome = canRecycle
                    ? RecycleBin.Move(entry.Path, out _)
                    : RecycleBin.Outcome.Unsupported;
                switch (outcome)
                {
                    case RecycleBin.Outcome.Moved:
                        moved++;
                        break;
                    case RecycleBin.Outcome.Unsupported:
                        if (entry.IsDirectory)
                            Directory.Delete(entry.Path, true);
                        else
                            File.Delete(entry.Path);
                        permanent++;
                        break;
                    default:
                        throw new IOException(Lang.Text("Instance.Saves.DeleteFailed"));
                }
            }

            if (moved > 0)
                HintService.Show(
                    Hints.Success(
                        targets.Count == 1
                            ? Lang.Text(
                                "Instance.Saves.Datapack.Delete.RecycleSingle",
                                targets[0].DisplayName)
                            : Lang.Text("Instance.Saves.Datapack.Delete.RecycleMultiple", moved)));
            if (permanent > 0)
                HintService.Show(
                    Hints.Success(
                        targets.Count == 1
                            ? Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentSingle",
                                targets[0].DisplayName)
                            : Lang.Text(
                                "Instance.Saves.Datapack.Delete.PermanentMultiple",
                                permanent)));
            _selected.Clear();
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Delete.Failed", ex);
        }
    }

    #endregion

    #region 安装与导出

    private async Task InstallFromFilesAsync()
    {
        try
        {
            var path = DatapacksPath;
            if (path is null)
                return;
            var files = SystemDialogs.SelectFiles(
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Filter"),
                Lang.Text("Instance.Saves.Datapack.Install.FileDialog.Title"));
            if (files.Length == 0)
                return;

            var confirm = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Saves.Datapack.Install.Title"),
                    Content = Lang.Text("Instance.Saves.Datapack.Install.Message"),
                    Buttons = DialogButtons.OkCancel
                });
            if (!confirm.IsConfirmed)
                return;

            InstancePageHelper.EnsureDirectory(path);
            foreach (var file in files)
            {
                var target = Path.Combine(path, Path.GetFileName(file));
                if (File.Exists(target)
                    && !string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
                {
                    HintService.Show(
                        Hints.Warning(
                            Lang.Text(
                                "Instance.Saves.Datapack.Replace.FileNameConflict",
                                Path.GetFileName(file))));
                    continue;
                }

                File.Copy(file, target, true);
            }

            HintService.Show(
                Hints.Success(
                    Lang.Text(
                        "Instance.Resource.Install.SuccessMultiple",
                        files.Length,
                        Lang.Text("Instance.Saves.Left.Datapack"))));
            Reload();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    private void DownloadNew()
    {
        PageRouter.Navigate(PageType.Download, PageSubType.DownloadDataPack);
    }

    private async Task ExportInfoAsync()
    {
        try
        {
            var mode = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Resource.Export.Mode.Title"),
                    Content = Lang.Text("Instance.Saves.Datapack.Export.Mode.Message"),
                    Buttons = DialogButtons.Create(
                        "Instance.Resource.Export.Mode.Txt",
                        "Instance.Resource.Export.Mode.Csv",
                        "Common.Action.Cancel")
                });
            if (!mode.IsConfirmed && mode.Button != 2 && mode.Button != 3)
                return;

            var isCsv = mode.Button == 2;
            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Instance.Resource.Export.SelectSaveLocation"),
                isCsv ? "datapacks.csv" : "datapacks.txt",
                Lang.Text("Instance.Resource.Export.FilesFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            var builder = new StringBuilder();
            if (isCsv)
                builder.AppendLine("FileName,Enabled,Folder,Size,Version,Description");
            foreach (var entry in _entries.OrderBy(
                         entry => entry.DisplayName,
                         StringComparer.OrdinalIgnoreCase))
                if (isCsv)
                    builder.AppendLine(
                        string.Join(
                            ',',
                            new[]
                            {
                                entry.DisplayName,
                                (!entry.Disabled).ToString(),
                                entry.IsDirectory.ToString(),
                                SafeSize(entry).ToString(),
                                entry.Version,
                                entry.Description ?? string.Empty
                            }.Select(CsvEscape)));
                else
                    builder.AppendLine(entry.DisplayName);

            await File.WriteAllTextAsync(savePath, builder.ToString(), Encoding.UTF8);
            InstancePageHelper.OpenFolder(savePath);
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Resource.Error.OperationFailed", ex);
        }
    }

    #endregion
}
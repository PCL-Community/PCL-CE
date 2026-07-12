// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceResourceRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstancePageSubType _page;
    private InstanceResourceKind _kind = InstanceResourceKind.Mod;
    private ResourceFilter _filter;
    private ResourceSort _sort = ResourceSort.FileName;
    private string _folder = string.Empty;
    private List<ResourceEntry> _entries = [];
    private bool _isLoading;

    public PageInstanceResourceRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        WireControls();
    }

    public event EventHandler<string>? OpenFolderRequested;

    public event EventHandler<InstancePageSubType>? DownloadRequested;

    public event EventHandler<string>? StatusMessage;

    public void SetContext(LaunchInstanceInfo instance, InstancePageSubType page)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _page = page;
        _kind = InstancePageRegistry.GetResourceKind(page);
        if (_kind == InstanceResourceKind.None)
            _kind = InstanceResourceKind.Mod;

        string relativePath = InstancePageRegistry.GetFolderRelativePath(page);
        if (string.IsNullOrWhiteSpace(relativePath))
            relativePath = "mods";

        // WPF: isolated instance → version folder; else shared .minecraft root.
        _ = SetContextAsync(instance, relativePath);
    }

    private async Task SetContextAsync(LaunchInstanceInfo instance, string relativePath)
    {
        if (_isLoading)
            return;
        _isLoading = true;
        try
        {
            bool isolated = true;
            try
            {
                InstanceMetadata metadata = await InstanceMetadataStore.LoadAsync(instance.InstanceDirectory)
                    .ConfigureAwait(true);
                isolated = metadata.InstanceIsolation;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                isolated = true;
            }

            string gameDir = isolated
                ? instance.InstanceDirectory
                : GetMinecraftRootFromInstance(instance);
            _folder = Path.Combine(gameDir, relativePath);
            Directory.CreateDirectory(_folder);
            ApplyKindChrome();
            Reload();
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void SetDataPackFolder(string saveFolder)
    {
        _instance = null;
        _page = InstancePageSubType.Saves;
        _kind = InstanceResourceKind.DataPack;
        _folder = Path.Combine(saveFolder, "datapacks");
        Directory.CreateDirectory(_folder);
        ApplyKindChrome();
        Reload();
    }

    public void Reload()
    {
        if (string.IsNullOrWhiteSpace(_folder))
            return;

        try
        {
            Directory.CreateDirectory(_folder);
            _entries = Directory.EnumerateFileSystemEntries(_folder)
                .Where(IsAcceptedPath)
                .Select(path => new ResourceEntry(path, Directory.Exists(path), IsDisabledPath(path), GetLength(path), File.GetCreationTime(path), File.GetLastWriteTime(path)))
                .ToList();
            RefreshUI();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Resource.LoadFailed"));
        }
    }

    private void WireControls()
    {
        WireButton("BtnManageOpen", OpenCurrentFolder);
        WireButton("BtnHintOpen", OpenCurrentFolder);
        WireButton("BtnManageDownload", RequestDownload);
        WireButton("BtnHintDownload", RequestDownload);
        WireButton("BtnManageInstall", () => _ = InstallFromFilesAsync());
        WireButton("BtnHintInstall", () => _ = InstallFromFilesAsync());

        if (this.FindControl<MySearchBox>("SearchBox") is { } searchBox)
            searchBox.TextChanged += (_, _) => RefreshUI();
        if (this.FindControl<MyIconTextButton>("BtnSort") is { } sort)
            sort.Click += (_, _) => CycleSort();

        foreach (MyRadioButton radioButton in new[]
                 {
                     this.FindControl<MyRadioButton>("BtnFilterAll"),
                     this.FindControl<MyRadioButton>("BtnFilterEnabled"),
                     this.FindControl<MyRadioButton>("BtnFilterDisabled")
                 }.OfType<MyRadioButton>())
        {
            radioButton.Check += (sender, _) =>
            {
                if (sender.Tag is string text && int.TryParse(text, out int value))
                    _filter = (ResourceFilter)value;
                RefreshUI();
            };
        }
    }

    private void WireButton(string name, Action action)
    {
        if (this.FindControl<MyButton>(name) is { } button)
            button.Click += (_, _) => action();
    }

    private void ApplyKindChrome()
    {
        if (this.FindControl<MyCard>("PanListBack") is { } listBack)
            listBack.Title = Text("Instance.Resource.ListTitle", KindDisplayName(_kind));
        if (this.FindControl<TextBlock>("TxtEmptyTitle") is { } title)
            title.Text = Text("Instance.Resource.Empty.Title", KindDisplayName(_kind));
        if (this.FindControl<TextBlock>("TxtEmptyDescription") is { } description)
            description.Text = Text("Instance.Resource.Empty.Description", KindDisplayName(_kind));

        bool supportsDisable = _kind == InstanceResourceKind.Mod;
        if (this.FindControl<MyRadioButton>("BtnFilterEnabled") is { } enabled)
            enabled.IsVisible = supportsDisable;
        if (this.FindControl<MyRadioButton>("BtnFilterDisabled") is { } disabled)
            disabled.IsVisible = supportsDisable;
        if (!supportsDisable)
        {
            _filter = ResourceFilter.All;
            this.FindControl<MyRadioButton>("BtnFilterAll")?.SetChecked(true, false, false);
        }

        bool canDownload = _kind is not InstanceResourceKind.Schematic;
        if (this.FindControl<MyButton>("BtnManageDownload") is { } download)
            download.IsVisible = canDownload;
        if (this.FindControl<MyButton>("BtnHintDownload") is { } hintDownload)
            hintDownload.IsVisible = canDownload;
    }

    private void RefreshUI()
    {
        List<ResourceEntry> showing = GetFilteredEntries().ToList();
        SortEntries(showing);

        if (this.FindControl<MyCard>("PanListBack") is { } listBack)
        {
            string kind = KindDisplayName(_kind);
            string count = showing.Count.ToString(CultureInfo.CurrentCulture);
            listBack.Title = IsSearching
                ? Text("Instance.Resource.SearchResultTitle", kind, count)
                : Text("Instance.Resource.ListTitleWithCount", kind, count);
        }

        bool isEmpty = _entries.Count == 0;
        if (this.FindControl<Control>("PanEmpty") is { } empty)
            empty.IsVisible = isEmpty;
        if (this.FindControl<Control>("PanMain") is { } main)
            main.IsVisible = !isEmpty;

        if (this.FindControl<StackPanel>("PanList") is not { } list)
            return;

        list.Children.Clear();
        if (isEmpty)
            return;

        foreach (ResourceEntry entry in showing)
            list.Children.Add(CreateEntryItem(entry));
    }

    private MyLocalModItem CreateEntryItem(ResourceEntry entry)
    {
        MyLocalModItem item = new()
        {
            Title = GetDisplayName(entry),
            Description = GetEntryInfo(entry),
            Logo = GetEntryLogo(entry),
            State = entry.IsDisabled ? ResourceItemState.Disabled : ResourceItemState.Fine
        };
        item.Click += (_, _) => OpenEntryLocation(entry);

        List<MyIconButton> buttons =
        [
            new()
            {
                SvgIcon = "lucide/folder-open",
                ToolTip = Text("Common.Action.Open")
            }
        ];
        buttons[0].Click += (_, _) => OpenEntryLocation(entry);

        if (_kind == InstanceResourceKind.Mod && !entry.IsDirectory)
        {
            MyIconButton toggle = new()
            {
                SvgIcon = entry.IsDisabled ? "lucide/circle-check" : "lucide/circle-minus",
                ToolTip = entry.IsDisabled ? Text("Instance.Resource.Enable") : Text("Instance.Resource.Disable")
            };
            toggle.Click += (_, _) => ToggleModAsync(entry);
            buttons.Add(toggle);
        }

        MyIconButton delete = new()
        {
            SvgIcon = "lucide/trash-2",
            Theme = MyIconButton.Themes.Red,
            ToolTip = Text("Common.Action.Delete")
        };
        delete.Click += (_, _) => DeleteEntryAsync(entry);
        buttons.Add(delete);

        item.Buttons = buttons;
        return item;
    }

    private IEnumerable<ResourceEntry> GetFilteredEntries()
    {
        string keyword = this.FindControl<MySearchBox>("SearchBox")?.Text?.Trim() ?? string.Empty;
        foreach (ResourceEntry entry in _entries)
        {
            if (!string.IsNullOrWhiteSpace(keyword) &&
                !GetDisplayName(entry).Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            if (_kind == InstanceResourceKind.Mod)
            {
                if (_filter == ResourceFilter.Enabled && entry.IsDisabled)
                    continue;
                if (_filter == ResourceFilter.Disabled && !entry.IsDisabled)
                    continue;
            }

            yield return entry;
        }
    }

    private void SortEntries(List<ResourceEntry> entries)
    {
        Comparison<ResourceEntry> comparison = _sort switch
        {
            ResourceSort.AddTime => (a, b) => b.CreationTime.CompareTo(a.CreationTime),
            ResourceSort.ModifyTime => (a, b) => b.ModifyTime.CompareTo(a.ModifyTime),
            ResourceSort.FileSize => (a, b) => b.Length.CompareTo(a.Length),
            _ => (a, b) => string.Compare(GetDisplayName(a), GetDisplayName(b), StringComparison.OrdinalIgnoreCase)
        };
        entries.Sort(comparison);
        if (this.FindControl<MyIconTextButton>("BtnSort") is { } sort)
            sort.Text = Text("Instance.Resource.Sort.Text", SortDisplayName(_sort));
    }

    private void CycleSort()
    {
        _sort = _sort switch
        {
            ResourceSort.FileName => ResourceSort.ModifyTime,
            ResourceSort.ModifyTime => ResourceSort.AddTime,
            ResourceSort.AddTime => ResourceSort.FileSize,
            _ => ResourceSort.FileName
        };
        RefreshUI();
    }

    private void OpenCurrentFolder()
    {
        if (string.IsNullOrWhiteSpace(_folder))
            return;

        Directory.CreateDirectory(_folder);
        OpenFolderRequested?.Invoke(this, _folder);
    }

    private void RequestDownload() => DownloadRequested?.Invoke(this, _page);

    private async Task InstallFromFilesAsync()
    {
        try
        {
            IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storage is null)
                throw new InvalidOperationException("Storage provider is unavailable.");

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Text("Instance.Resource.InstallFromFiles"),
                AllowMultiple = true
            }).ConfigureAwait(true);

            int copied = 0;
            foreach (IStorageFile file in files)
            {
                string? source = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source) || !IsAcceptedPath(source))
                    continue;

                string target = Path.Combine(_folder, Path.GetFileName(source));
                if (File.Exists(target))
                    continue;

                File.Copy(source, target);
                copied++;
            }

            if (copied > 0)
            {
                StatusMessage?.Invoke(this, Text("Instance.Resource.Install.Success", copied.ToString(CultureInfo.CurrentCulture)));
                Reload();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Resource.Install.Failed"));
        }
    }

    private async void ToggleModAsync(ResourceEntry entry)
    {
        try
        {
            await Task.Run(() =>
            {
                string target = entry.IsDisabled
                    ? entry.FullPath[..^".disabled".Length]
                    : entry.FullPath + ".disabled";
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException("Target exists.");
                File.Move(entry.FullPath, target);
            }).ConfigureAwait(true);
            StatusMessage?.Invoke(this, entry.IsDisabled ? Text("Instance.Resource.Enabled") : Text("Instance.Resource.Disabled"));
            Reload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Resource.ToggleFailed"));
        }
    }

    private async void DeleteEntryAsync(ResourceEntry entry)
    {
        try
        {
            await Task.Run(() =>
            {
                if (entry.IsDirectory)
                    Directory.Delete(entry.FullPath, recursive: true);
                else if (File.Exists(entry.FullPath))
                    File.Delete(entry.FullPath);
            }).ConfigureAwait(true);
            StatusMessage?.Invoke(this, Text("Instance.Resource.Deleted"));
            Reload();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, Text("Instance.Resource.DeleteFailed"));
        }
    }

    private void OpenEntryLocation(ResourceEntry entry)
    {
        string path = entry.IsDirectory
            ? entry.FullPath
            : Path.GetDirectoryName(entry.FullPath) ?? _folder;
        OpenFolderRequested?.Invoke(this, path);
    }

    private bool IsSearching => !string.IsNullOrWhiteSpace(this.FindControl<MySearchBox>("SearchBox")?.Text);

    private bool IsAcceptedPath(string path)
    {
        if (Directory.Exists(path))
            return _kind is InstanceResourceKind.ResourcePack or InstanceResourceKind.ShaderPack or InstanceResourceKind.DataPack;

        string fileName = Path.GetFileName(path);
        string extension = Path.GetExtension(path);
        return _kind switch
        {
            InstanceResourceKind.Mod => fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase),
            InstanceResourceKind.ResourcePack or InstanceResourceKind.ShaderPack or InstanceResourceKind.DataPack =>
                extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                // Iris / OptiFine may also drop loose folders; folders already accepted above.
                extension.Equals(".jar", StringComparison.OrdinalIgnoreCase),
            InstanceResourceKind.Schematic => extension.Equals(".schematic", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".schem", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".litematic", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".nbt", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".bp", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static long GetLength(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0L;
        }
    }

    private static bool IsDisabledPath(string path) =>
        path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

    private static string GetDisplayName(ResourceEntry entry)
    {
        string name = Path.GetFileName(entry.FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return entry.IsDisabled && name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? name[..^".disabled".Length]
            : name;
    }

    private string GetEntryInfo(ResourceEntry entry)
    {
        string state = _kind == InstanceResourceKind.Mod
            ? entry.IsDisabled ? Text("Instance.Resource.State.Disabled") : Text("Instance.Resource.State.Enabled")
            : entry.IsDirectory ? Text("Instance.Resource.State.Folder") : Text("Instance.Resource.State.File");
        return Text(
            "Instance.Resource.Item.Info",
            state,
            FormatSize(entry.Length),
            entry.ModifyTime.ToString("d", CultureInfo.CurrentCulture));
    }

    private string GetEntryLogo(ResourceEntry entry) =>
        _kind switch
        {
            InstanceResourceKind.Mod => entry.IsDisabled ? InstanceDisplayHelper.BlockAssetRoot + "RedstoneBlock.png" : InstanceDisplayHelper.BlockAssetRoot + "CommandBlock.png",
            InstanceResourceKind.ResourcePack => InstanceDisplayHelper.BlockAssetRoot + "Grass.png",
            InstanceResourceKind.ShaderPack => InstanceDisplayHelper.BlockAssetRoot + "GoldBlock.png",
            InstanceResourceKind.Schematic => InstanceDisplayHelper.BlockAssetRoot + "StructureBlock.png",
            InstanceResourceKind.DataPack => InstanceDisplayHelper.BlockAssetRoot + "CommandBlock.png",
            _ => InstanceDisplayHelper.DefaultLogo
        };

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {units[unit]}"
            : string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", value, units[unit]);
    }

    private static string KindDisplayName(InstanceResourceKind kind) =>
        kind switch
        {
            InstanceResourceKind.ResourcePack => "资源包",
            InstanceResourceKind.ShaderPack => "光影",
            InstanceResourceKind.Schematic => "投影",
            InstanceResourceKind.DataPack => "数据包",
            _ => "Mod"
        };

    private string SortDisplayName(ResourceSort sort) =>
        sort switch
        {
            ResourceSort.AddTime => Text("Instance.Resource.Sort.AddTime"),
            ResourceSort.ModifyTime => Text("Instance.Resource.Sort.ModifyTime"),
            ResourceSort.FileSize => Text("Instance.Resource.Sort.FileSize"),
            _ => Text("Instance.Resource.Sort.FileName")
        };

    private string Text(string key, params string[] args)
    {
        string? value = null;
        // Prefer app/theme resource dictionaries (PclTheme + localization).
        if (Avalonia.Application.Current?.TryGetResource(key, ActualThemeVariant, out object? appRes) == true &&
            appRes is string appText)
        {
            value = appText;
        }
        else if (TryGetResource(key, ActualThemeVariant, out object? localRes) && localRes is string localText)
        {
            value = localText;
        }

        value ??= BuiltInResourceText(key) ?? key;
        return args.Length == 0
            ? value
            : string.Format(CultureInfo.CurrentCulture, value, args);
    }

    private static string? BuiltInResourceText(string key) =>
        key switch
        {
            "Instance.Resource.ListTitle" => "{0} 列表",
            "Instance.Resource.ListTitleWithCount" => "{0} 列表 ({1})",
            "Instance.Resource.SearchResultTitle" => "{0} 搜索结果 ({1})",
            "Instance.Resource.Sort.Text" => "排序：{0}",
            "Instance.Resource.Sort.FileName" => "文件名",
            "Instance.Resource.Sort.ModifyTime" => "修改时间",
            "Instance.Resource.Sort.AddTime" => "添加时间",
            "Instance.Resource.Sort.FileSize" => "文件大小",
            "Instance.Resource.Empty.Title" => "还没有 {0}",
            "Instance.Resource.Empty.Description" => "这个版本还没有 {0}。你可以下载新的内容，或从本地文件安装。",
            "Instance.Resource.Item.Info" => "{0} · {1} · 修改于 {2}",
            "Instance.Resource.State.Enabled" => "已启用",
            "Instance.Resource.State.Disabled" => "已禁用",
            "Instance.Resource.State.File" => "文件",
            "Instance.Resource.State.Folder" => "文件夹",
            "Common.Action.Open" => "打开",
            "Common.Action.Delete" => "删除",
            "Instance.Resource.Enable" => "启用",
            "Instance.Resource.Disable" => "禁用",
            _ => null
        };

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

    private enum ResourceFilter
    {
        All = 0,
        Enabled = 1,
        Disabled = 2
    }

    private enum ResourceSort
    {
        FileName,
        ModifyTime,
        AddTime,
        FileSize
    }

    private sealed record ResourceEntry(
        string FullPath,
        bool IsDirectory,
        bool IsDisabled,
        long Length,
        DateTime CreationTime,
        DateTime ModifyTime);
}

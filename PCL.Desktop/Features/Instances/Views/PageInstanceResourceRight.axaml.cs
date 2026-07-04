// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceResourceRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstancePageSubType _page;
    private ResourceKind _kind = ResourceKind.Mod;
    private ResourceFilter _filter;
    private ResourceSort _sort = ResourceSort.FileName;
    private string _folder = string.Empty;
    private List<ResourceEntry> _entries = [];

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
        _instance = instance;
        _page = page;
        _kind = ResourceKindFromPage(page);
        _folder = Path.Combine(GetMinecraftRootFromInstance(instance), GetFolderRelativePath(_kind));
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

        bool supportsDisable = _kind == ResourceKind.Mod;
        if (this.FindControl<MyRadioButton>("BtnFilterEnabled") is { } enabled)
            enabled.IsVisible = supportsDisable;
        if (this.FindControl<MyRadioButton>("BtnFilterDisabled") is { } disabled)
            disabled.IsVisible = supportsDisable;
        if (!supportsDisable)
        {
            _filter = ResourceFilter.All;
            this.FindControl<MyRadioButton>("BtnFilterAll")?.SetChecked(true, false, false);
        }

        bool canDownload = _kind != ResourceKind.Schematic;
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
            string titleKey = IsSearching ? "Instance.Resource.SearchResultTitle" : "Instance.Resource.ListTitleWithCount";
            listBack.Title = Text(titleKey, KindDisplayName(_kind), showing.Count.ToString(CultureInfo.CurrentCulture));
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

    private MyListItem CreateEntryItem(ResourceEntry entry)
    {
        MyListItem item = new()
        {
            Title = GetDisplayName(entry),
            Info = GetEntryInfo(entry),
            Logo = GetEntryLogo(entry),
            Type = MyListItem.CheckType.Clickable,
            MinPaddingRight = 120d
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

        if (_kind == ResourceKind.Mod && !entry.IsDirectory)
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

            if (_kind == ResourceKind.Mod)
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
            return _kind is ResourceKind.ResourcePack or ResourceKind.ShaderPack;

        string fileName = Path.GetFileName(path);
        string extension = Path.GetExtension(path);
        return _kind switch
        {
            ResourceKind.Mod => fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                fileName.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase),
            ResourceKind.ResourcePack or ResourceKind.ShaderPack => extension.Equals(".zip", StringComparison.OrdinalIgnoreCase),
            ResourceKind.Schematic => extension.Equals(".schematic", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".schem", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".litematic", StringComparison.OrdinalIgnoreCase) ||
                                      extension.Equals(".nbt", StringComparison.OrdinalIgnoreCase),
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
        string state = _kind == ResourceKind.Mod
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
            ResourceKind.Mod => entry.IsDisabled ? InstanceDisplayHelper.BlockAssetRoot + "RedstoneBlock.png" : InstanceDisplayHelper.BlockAssetRoot + "CommandBlock.png",
            ResourceKind.ResourcePack => InstanceDisplayHelper.BlockAssetRoot + "Grass.png",
            ResourceKind.ShaderPack => InstanceDisplayHelper.BlockAssetRoot + "GoldBlock.png",
            ResourceKind.Schematic => InstanceDisplayHelper.BlockAssetRoot + "StructureBlock.png",
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

    private static ResourceKind ResourceKindFromPage(InstancePageSubType page) =>
        page switch
        {
            InstancePageSubType.ResourcePacks => ResourceKind.ResourcePack,
            InstancePageSubType.Shaders => ResourceKind.ShaderPack,
            InstancePageSubType.Schematics => ResourceKind.Schematic,
            _ => ResourceKind.Mod
        };

    private static string GetFolderRelativePath(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.ResourcePack => "resourcepacks",
            ResourceKind.ShaderPack => "shaderpacks",
            ResourceKind.Schematic => "schematics",
            _ => "mods"
        };

    private static string KindDisplayName(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.ResourcePack => "资源包",
            ResourceKind.ShaderPack => "光影",
            ResourceKind.Schematic => "投影",
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
        string value = TryGetResource(key, null, out object? resource) && resource is string text
            ? text
            : key;
        return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
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

    private enum ResourceKind
    {
        Mod,
        ResourcePack,
        ShaderPack,
        Schematic
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

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceManageRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstanceMetadata _metadata = new();
    private readonly SemaphoreSlim _metadataWriteLock = new(1, 1);
    private bool _isApplyingMetadata;

    public PageInstanceManageRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        WireWpfCopiedControls();
    }

    public event EventHandler<LaunchInstanceInfo>? OpenFolderRequested;

    public event EventHandler<string>? OpenPathRequested;

    public event EventHandler<LaunchInstanceInfo>? RenameRequested;

    public event EventHandler<LaunchInstanceInfo>? DeleteRequested;

    public event EventHandler<LaunchInstanceInfo>? EditDescriptionRequested;

    public event EventHandler<LaunchInstanceInfo>? ToggleStarRequested;

    public event EventHandler<LaunchInstanceInfo>? ExportLaunchScriptRequested;

    public event EventHandler<LaunchInstanceInfo>? TestLaunchRequested;

    public event EventHandler<LaunchInstanceInfo>? RepairFilesRequested;

    public event EventHandler<LaunchInstanceInfo>? ResetSettingsRequested;

    public event EventHandler<LaunchInstanceInfo>? PatchCoreRequested;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        _metadata = InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult();
        PopulateDisplayItem(instance);
        PopulateInfo(instance);
        ApplyMetadataToControls();
    }

    private void WireWpfCopiedControls()
    {
        if (this.FindControl<MyComboBoxItem>("ItemDisplayLogoCustom") is { } customLogo)
            customLogo.Tag = InstanceDisplayHelper.CustomLogoRelativePath;

        if (this.FindControl<MyComboBox>("ComboDisplayLogo") is { } logoCombo)
            logoCombo.SelectionChanged += ComboDisplayLogo_SelectionChanged;

        if (this.FindControl<MyComboBox>("ComboDisplayType") is { } typeCombo)
            typeCombo.SelectionChanged += ComboDisplayType_SelectionChanged;

        WireButton("BtnFolderVersion", () =>
        {
            if (_instance is not null)
                OpenFolderRequested?.Invoke(this, _instance);
        });
        WireButton("BtnFolderSaves", () => OpenMinecraftSubFolder("saves"));
        WireButton("BtnFolderMods", () => OpenMinecraftSubFolder("mods"));
        WireButton("BtnDisplayRename", () =>
        {
            if (_instance is not null)
                RenameRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManageDelete", () =>
        {
            if (_instance is not null)
                DeleteRequested?.Invoke(this, _instance);
        });

        WireButton("BtnDisplayDesc", () =>
        {
            if (_instance is not null)
                EditDescriptionRequested?.Invoke(this, _instance);
        });
        WireButton("BtnDisplayStar", () =>
        {
            if (_instance is not null)
                ToggleStarRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManageScript", () =>
        {
            if (_instance is not null)
                ExportLaunchScriptRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManageTest", () =>
        {
            if (_instance is not null)
                TestLaunchRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManageCheck", () =>
        {
            if (_instance is not null)
                RepairFilesRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManageRestore", () =>
        {
            if (_instance is not null)
                ResetSettingsRequested?.Invoke(this, _instance);
        });
        WireButton("BtnManagePatch", () =>
        {
            if (_instance is not null)
                PatchCoreRequested?.Invoke(this, _instance);
        });
    }

    private void WireButton(string name, Action action)
    {
        if (this.FindControl<MyButton>(name) is { } button)
            button.Click += (_, _) => action();
    }

    private void PopulateDisplayItem(LaunchInstanceInfo instance)
    {
        if (this.FindControl<Grid>("PanDisplayItem") is not { } panel)
            return;

        panel.Children.Clear();
        MyListItem item = new()
        {
            Title = instance.Name,
            Info = string.IsNullOrWhiteSpace(_metadata.Description) ? instance.InstanceDirectory : _metadata.Description,
            Logo = InstanceDisplayHelper.ResolveLogo(instance, _metadata),
            Height = 42d,
            IsHitTestVisible = false
        };
        panel.Children.Add(item);
    }

    private void PopulateInfo(LaunchInstanceInfo instance)
    {
        if (this.FindControl<StackPanel>("PanInfo") is not { } panel)
            return;

        InstanceJsonInfo jsonInfo = ReadInstanceJsonInfo(instance);
        WrapPanel wrap = new()
        {
            Margin = new Thickness(0, -5, -20, 7)
        };

        AddInfoItem(
            wrap,
            ResourceText("Instance.Overall.Info.LaunchCount.Title", "启动次数"),
            _metadata.LaunchCount <= 0
                ? ResourceText("Instance.Overall.Info.LaunchCount.Never", "从未启动")
                : ResourceText("Instance.Overall.Info.LaunchCount.Count", "已启动 {0} 次", _metadata.LaunchCount),
            _metadata.LaunchCount <= 0 ? "RedstoneLampOff.png" : "RedstoneLampOn.png");
        if (!string.IsNullOrWhiteSpace(_metadata.ModpackVersion))
        {
            AddInfoItem(
                wrap,
                ResourceText("Instance.Overall.Info.ModpackVersion", "整合包版本"),
                _metadata.ModpackVersion,
                "CommandBlock.png");
        }

        AddInfoItem(wrap, "Minecraft", jsonInfo.MinecraftVersion, "Grass.png");
        foreach (InstanceInfoItem item in jsonInfo.LoaderItems)
            AddInfoItem(wrap, item.Title, item.Info, item.ImageName);
        if (!string.IsNullOrWhiteSpace(jsonInfo.InheritsFrom))
            AddInfoItem(wrap, "继承版本", jsonInfo.InheritsFrom, "CommandBlock.png");
        AddInfoItem(wrap, "版本文件", instance.VersionJsonPath, "CommandBlock.png");
        AddInfoItem(wrap, "版本目录", instance.InstanceDirectory, "CobbleStone.png");

        panel.Children.Clear();
        panel.Children.Add(wrap);

        if (this.FindControl<MyButton>("BtnFolderMods") is { } modsButton)
            modsButton.IsVisible = jsonInfo.IsModable;
    }

    private void ApplyMetadataToControls()
    {
        _isApplyingMetadata = true;
        try
        {
            if (this.FindControl<MyButton>("BtnDisplayStar") is { } star)
                star.Text = _metadata.IsStarred ? "取消收藏" : "收藏";

            SetComboIndex("ComboDisplayType", Math.Clamp(_metadata.CardType, 0, 5));
            SelectLogoItem(_metadata.LogoPath);
        }
        finally
        {
            _isApplyingMetadata = false;
        }
    }

    private static void AddInfoItem(WrapPanel panel, string title, string info, string imageName)
    {
        panel.Children.Add(new MyListItem
        {
            Title = title,
            Info = info,
            Logo = InstanceDisplayHelper.BlockAssetRoot + imageName,
            Height = 42d,
            Width = 245d,
            Margin = new Thickness(0, 5, 20, 0)
        });
    }

    private async void ComboDisplayLogo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingMetadata || _instance is null || sender is not MyComboBox comboBox)
            return;

        if (ReferenceEquals(comboBox.SelectedItem, this.FindControl<MyComboBoxItem>("ItemDisplayLogoCustom")))
        {
            await SelectCustomLogoAsync().ConfigureAwait(true);
            return;
        }

        string logoPath = (comboBox.SelectedItem as MyComboBoxItem)?.Tag?.ToString() ?? string.Empty;
        TryDeleteCustomLogo(_instance);
        await UpdateMetadataAsync(metadata => metadata with { LogoPath = logoPath }).ConfigureAwait(true);
    }

    private async void ComboDisplayType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingMetadata || sender is not MyComboBox comboBox)
            return;

        int selectedIndex = Math.Max(0, comboBox.SelectedIndex);
        await UpdateMetadataAsync(metadata => metadata with { CardType = selectedIndex }).ConfigureAwait(true);
    }

    private async Task SelectCustomLogoAsync()
    {
        if (_instance is null)
            return;

        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            await RunOnUiThreadAsync(ApplyMetadataToControls).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择版本图标",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.ico"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "image/x-icon"]
                }
            ]
        }).ConfigureAwait(true);

        if (files.Count == 0)
        {
            await RunOnUiThreadAsync(ApplyMetadataToControls).ConfigureAwait(false);
            return;
        }

        string logoPath = InstanceDisplayHelper.GetCustomLogoPath(_instance);
        Directory.CreateDirectory(Path.GetDirectoryName(logoPath)
            ?? throw new InvalidOperationException("无法确定自定义图标目录。"));

        await using (Stream source = await files[0].OpenReadAsync().ConfigureAwait(false))
        await using (FileStream destination = new(
                         logoPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 8 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }

        await UpdateMetadataAsync(metadata => metadata with
        {
            LogoPath = InstanceDisplayHelper.CustomLogoRelativePath
        }).ConfigureAwait(true);
    }

    private async Task UpdateMetadataAsync(Func<InstanceMetadata, InstanceMetadata> update)
    {
        LaunchInstanceInfo? instance = _instance;
        if (instance is null)
            return;

        await _metadataWriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            InstanceMetadata metadata = update(_metadata);
            _metadata = metadata;
            await InstanceMetadataStore.SaveAsync(instance.InstanceDirectory, metadata).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (_instance is null ||
                    !string.Equals(_instance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _metadata = metadata;
                PopulateDisplayItem(instance);
                ApplyMetadataToControls();
            }).ConfigureAwait(false);
        }
        finally
        {
            _metadataWriteLock.Release();
        }
    }

    private void SelectLogoItem(string logoPath)
    {
        if (this.FindControl<MyComboBox>("ComboDisplayLogo") is not { } comboBox)
            return;

        if (InstanceDisplayHelper.IsCustomLogoPath(logoPath))
        {
            comboBox.SelectedItem = this.FindControl<MyComboBoxItem>("ItemDisplayLogoCustom");
            return;
        }

        if (string.IsNullOrWhiteSpace(logoPath))
        {
            comboBox.SelectedIndex = 0;
            return;
        }

        string normalizedLogo = NormalizeLogoTag(logoPath);
        foreach (object? item in comboBox.Items)
        {
            if (item is not MyComboBoxItem comboBoxItem)
                continue;

            string? tag = comboBoxItem.Tag?.ToString();
            if (string.Equals(NormalizeLogoTag(tag), normalizedLogo, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        comboBox.SelectedIndex = 0;
    }

    private void SetComboIndex(string name, int index)
    {
        if (this.FindControl<MyComboBox>(name) is { } comboBox && comboBox.ItemCount > 0)
            comboBox.SelectedIndex = Math.Clamp(index, 0, comboBox.ItemCount - 1);
    }

    private static string NormalizeLogoTag(string? value) =>
        InstanceDisplayHelper.NormalizeLogoTag(value);

    private static void TryDeleteCustomLogo(LaunchInstanceInfo instance)
    {
        try
        {
            string customLogo = InstanceDisplayHelper.GetCustomLogoPath(instance);
            if (File.Exists(customLogo))
                File.Delete(customLogo);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private InstanceJsonInfo ReadInstanceJsonInfo(LaunchInstanceInfo instance)
    {
        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string? id = root.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;
            string? inheritsFrom = root.TryGetProperty("inheritsFrom", out JsonElement inheritsElement) ? inheritsElement.GetString() : null;
            List<string> libraries = ReadLibraryNames(root).ToList();
            IReadOnlyList<InstanceInfoItem> loaderItems = DetectLoaderInfo(libraries, ResourceText("Instance.Overall.Info.Installed", "已安装"));
            return new InstanceJsonInfo(
                string.IsNullOrWhiteSpace(inheritsFrom)
                    ? (string.IsNullOrWhiteSpace(id) ? instance.Name : id)
                    : inheritsFrom,
                inheritsFrom,
                loaderItems,
                loaderItems.Any(static item => item.IsModable));
        }
        catch (Exception)
        {
            return new InstanceJsonInfo(instance.Name, null, [], IsModable: false);
        }
    }

    private static List<InstanceInfoItem> DetectLoaderInfo(IReadOnlyList<string> libraries, string installedText)
    {
        List<InstanceInfoItem> items = [];
        AddLoader(items, libraries, "Forge", "Anvil.png", isModable: true, "net.minecraftforge:forge:", "minecraftforge");
        AddLoader(items, libraries, "NeoForge", "NeoForge.png", isModable: true, "net.neoforged:neoforge:", "net.neoforge:forge:", "neoforge");
        AddLoader(items, libraries, "Cleanroom", "Cleanroom.png", isModable: true, "com.cleanroommc:cleanroom:", "cleanroom");
        AddLoader(items, libraries, "Fabric", "Fabric.png", isModable: true, "net.fabricmc:fabric-loader:");
        AddLoader(items, libraries, "Quilt", "Quilt.png", isModable: true, "org.quiltmc:quilt-loader:");
        AddLoader(items, libraries, "OptiFine", "GrassPath.png", isModable: true, "optifine");
        AddLoader(items, libraries, "LiteLoader", "Egg.png", true, installedText, "liteloader");
        AddLoader(items, libraries, "Legacy Fabric", "Fabric.png", isModable: true, "net.legacyfabric:", "legacyfabric");
        AddLoader(items, libraries, "LabyMod", "LabyMod.png", isModable: true, "labymod");
        return items;
    }

    private static void AddLoader(
        List<InstanceInfoItem> items,
        IReadOnlyList<string> libraries,
        string title,
        string imageName,
        bool isModable,
        params string[] needles)
    {
        AddLoader(items, libraries, title, imageName, isModable, explicitInfo: null, needles);
    }

    private static void AddLoader(
        List<InstanceInfoItem> items,
        IReadOnlyList<string> libraries,
        string title,
        string imageName,
        bool isModable,
        string? explicitInfo,
        params string[] needles)
    {
        string? library = libraries.FirstOrDefault(library =>
            needles.Any(needle => library.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        if (string.IsNullOrWhiteSpace(library))
            return;

        items.Add(new InstanceInfoItem(title, explicitInfo ?? SimplifyLibraryVersion(library), imageName, isModable));
    }

    private static string SimplifyLibraryVersion(string library)
    {
        int versionIndex = library.LastIndexOf(':');
        if (versionIndex < 0 || versionIndex == library.Length - 1)
            return "已安装";

        string version = library[(versionIndex + 1)..];
        int minecraftPrefixIndex = version.IndexOf('-');
        return minecraftPrefixIndex > 0 && minecraftPrefixIndex < version.Length - 1
            ? version[(minecraftPrefixIndex + 1)..]
            : version;
    }

    private static IEnumerable<string> ReadLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out JsonElement libraries) ||
            libraries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement library in libraries.EnumerateArray())
        {
            if (library.TryGetProperty("name", out JsonElement nameElement) &&
                nameElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(nameElement.GetString()))
            {
                yield return nameElement.GetString()!;
            }
        }
    }

    private void OpenMinecraftSubFolder(string name)
    {
        if (_instance is null)
            return;

        OpenPathRequested?.Invoke(this, Path.Combine(GetMinecraftRootFromInstance(_instance), name));
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

    private static Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return Dispatcher.UIThread.InvokeAsync(action).GetTask();
    }

    private string ResourceText(string key, string fallback, params object[] args)
    {
        string text = fallback;
        if (this.TryFindResource(key, ActualThemeVariant, out object? value) && value is string resourceText)
            text = resourceText;

        return args.Length == 0
            ? text
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, text, args);
    }

    private readonly record struct InstanceInfoItem(string Title, string Info, string ImageName, bool IsModable);

    private readonly record struct InstanceJsonInfo(
        string MinecraftVersion,
        string? InheritsFrom,
        IReadOnlyList<InstanceInfoItem> LoaderItems,
        bool IsModable);
}

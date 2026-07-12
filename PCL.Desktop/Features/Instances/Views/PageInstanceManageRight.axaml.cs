// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceManageRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstanceMetadata _metadata = new();
    private readonly SemaphoreSlim _metadataWriteLock = new(1, 1);
    private Task _pendingMetadataWrite = Task.CompletedTask;
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

    public Task WaitForPendingMetadataWritesAsync() => _pendingMetadataWrite;

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

        if (comboBox.SelectedItem is not MyComboBoxItem selectedItem ||
            string.IsNullOrWhiteSpace(selectedItem.Tag?.ToString()))
        {
            return;
        }

        TryDeleteCustomLogo(_instance);
        await PersistSelectedDisplayOptionsAsync().ConfigureAwait(true);
    }

    private async void ComboDisplayType_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingMetadata || sender is not MyComboBox comboBox)
            return;

        if (comboBox.SelectedIndex < 0)
            return;

        await PersistSelectedDisplayOptionsAsync().ConfigureAwait(true);
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

        await using (Stream source = await files[0].OpenReadAsync().ConfigureAwait(true))
        await using (FileStream destination = new(
                         logoPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 8 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await source.CopyToAsync(destination).ConfigureAwait(true);
        }

        await TrackMetadataUpdate(metadata => metadata with
        {
            LogoPath = InstanceDisplayHelper.CustomLogoRelativePath
        }).ConfigureAwait(true);
    }

    private Task TrackMetadataUpdate(Func<InstanceMetadata, InstanceMetadata> update)
    {
        Task task = UpdateMetadataAsync(update);
        _pendingMetadataWrite = task;
        return task;
    }

    private Task PersistSelectedDisplayOptionsAsync()
    {
        if (this.FindControl<MyComboBox>("ComboDisplayLogo") is not { SelectedItem: MyComboBoxItem logoItem } ||
            string.IsNullOrWhiteSpace(logoItem.Tag?.ToString()) ||
            this.FindControl<MyComboBox>("ComboDisplayType") is not { SelectedIndex: >= 0 } typeCombo)
        {
            return Task.CompletedTask;
        }

        string logoPath = logoItem.Tag!.ToString()!;
        int cardType = typeCombo.SelectedIndex;
        return TrackMetadataUpdate(metadata => metadata with
        {
            LogoPath = logoPath,
            CardType = cardType
        });
    }

    private Task UpdateMetadataAsync(Func<InstanceMetadata, InstanceMetadata> update)
    {
        LaunchInstanceInfo? instance = _instance;
        if (instance is null)
            return Task.CompletedTask;

        InstanceMetadata metadata = update(_metadata);
        _metadata = metadata;
        if (Dispatcher.UIThread.CheckAccess())
        {
            PopulateDisplayItem(instance);
            ApplyMetadataToControls();
        }

        return SaveMetadataAsync(instance, metadata);
    }

    private async Task SaveMetadataAsync(LaunchInstanceInfo instance, InstanceMetadata metadata)
    {
        await _metadataWriteLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await InstanceMetadataStore.SaveAsync(instance.InstanceDirectory, metadata).ConfigureAwait(false);
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
        MinecraftVersionJsonInfo jsonInfo = MinecraftVersionJsonInspector.Read(instance);
        IReadOnlyList<InstanceInfoItem> loaderItems = DetectLoaderInfo(
            jsonInfo.Libraries,
            ResourceText("Instance.Overall.Info.Installed", "已安装"));
        return new InstanceJsonInfo(
            jsonInfo.MinecraftVersionId,
            jsonInfo.InheritsFrom,
            loaderItems,
            loaderItems.Any(static item => item.IsModable));
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
        string? version = MinecraftLoaderLibraryDetector.DetectVersion(libraries, needles);
        if (string.IsNullOrWhiteSpace(version))
            return;

        items.Add(new InstanceInfoItem(title, explicitInfo ?? version, imageName, isModable));
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

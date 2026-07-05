// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceInstallRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;

    public PageInstanceInstallRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        WireWpfCopiedControls();
        HideLoading();
        HideAllHints();
    }

    public event EventHandler<LaunchInstanceInfo>? ModifyRequested;

    public event EventHandler<LaunchInstanceInfo>? DownloadRequested;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (_instance is null)
            return;

        PanScroll?.ScrollToHome();
        ApplySelectPageState();
        PopulateSelectedInstance(_instance);
        InitializeLoaderCards(_instance);
        HideAllHints();
        HideLoading();
    }

    private void WireWpfCopiedControls()
    {
        if (this.FindControl<MyExtraTextButton>("BtnSelectStart") is { } startButton)
        {
            startButton.Show = true;
            startButton.IsEnabled = true;
            startButton.Click += (_, _) =>
            {
                if (_instance is not null)
                    ModifyRequested?.Invoke(this, _instance);
            };
        }
    }

    private void ApplySelectPageState()
    {
        if (this.FindControl<Control>("PanMinecraft") is { } minecraft)
        {
            minecraft.IsVisible = false;
            minecraft.IsHitTestVisible = false;
            minecraft.Opacity = 0d;
            ResetTranslateX(minecraft);
        }

        if (this.FindControl<Control>("PanSelect") is { } select)
        {
            select.IsVisible = true;
            select.IsHitTestVisible = true;
            select.Opacity = 1d;
            ResetTranslateX(select);
        }

        if (this.FindControl<MyScrollViewer>("PanBack") is { } scroll)
        {
            scroll.IsHitTestVisible = true;
            scroll.ScrollToHome();
        }

        if (this.FindControl<MyExtraTextButton>("BtnSelectStart") is { } startButton)
        {
            startButton.Show = true;
            startButton.IsEnabled = true;
        }
    }

    private void PopulateSelectedInstance(LaunchInstanceInfo instance)
    {
        InstanceMetadata metadata = InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult();
        if (this.FindControl<MyListItem>("ItemSelect") is { } item)
        {
            item.Title = instance.Name;
            item.Info = instance.InstanceDirectory;
            item.Logo = InstanceDisplayHelper.ResolveLogo(instance, metadata);
        }

        if (this.FindControl<TextBlock>("LabMinecraft") is { } label)
            label.Text = instance.Name;

        if (this.FindControl<Image>("ImgMinecraft") is { } image)
            image.Source = LoadBlockImage("Grass.png");
    }

    private void InitializeLoaderCards(LaunchInstanceInfo instance)
    {
        CollapseLoaderCards();
        SetLoaderInfo("Forge", DetectLoader(instance, "forge"), "Anvil.png");
        SetLoaderInfo("Cleanroom", DetectLoader(instance, "cleanroom"), "Cleanroom.png");
        SetLoaderInfo("NeoForge", DetectLoader(instance, "neoforge"), "NeoForge.png");
        SetLoaderInfo("Fabric", DetectLoader(instance, "fabric-loader", "fabric"), "Fabric.png");
        SetLoaderInfo("LegacyFabric", DetectLoader(instance, "legacyfabric"), "Fabric.png");
        SetLoaderInfo("FabricApi", DetectModFile(instance, "fabric-api"), "Fabric.png");
        SetLoaderInfo("LegacyFabricApi", DetectModFile(instance, "legacy-fabric-api"), "Fabric.png");
        SetLoaderInfo("Quilt", DetectLoader(instance, "quilt"), "Quilt.png");
        SetLoaderInfo("QSL", DetectModFile(instance, "qsl", "quilted-fabric-api"), "Quilt.png");
        SetLoaderInfo("LabyMod", DetectLoader(instance, "labymod"), "LabyMod.png");
        SetLoaderInfo("OptiFine", DetectLoader(instance, "optifine"), "GrassPath.png");
        SetLoaderInfo("OptiFabric", DetectModFile(instance, "optifabric"), "OptiFabric.png");
        SetLoaderInfo("LiteLoader", DetectLoader(instance, "liteloader"), "Egg.png");
    }

    private void SetLoaderInfo(string name, string? detectedVersion, string imageName)
    {
        bool installed = !string.IsNullOrWhiteSpace(detectedVersion);
        if (this.FindControl<TextBlock>("Lab" + name) is { } label)
        {
            label.Text = installed ? detectedVersion : "可添加";
            label.Foreground = LegacyResourceResolver.Brush(label, "ColorBrushGray4", "#8c8c8c");
        }

        if (this.FindControl<Image>("Img" + name) is { } image)
        {
            image.Source = LoadBlockImage(imageName);
            image.IsVisible = installed;
        }

        if (this.FindControl<Control>("Btn" + name + "Clear") is { } clearButton)
            clearButton.IsVisible = installed;
    }

    private void CollapseLoaderCards()
    {
        foreach (MinecraftLoaderCardDescriptor loaderCard in MinecraftLoaderCardRegistry.AllCards)
        {
            string name = loaderCard.ControlSuffix;
            if (this.FindControl<MyCard>("Card" + name) is { } card)
                card.IsSwapped = true;
        }
    }

    private static void ResetTranslateX(Control control)
    {
        if (control.RenderTransform is TranslateTransform transform)
        {
            transform.X = 0d;
            return;
        }

        control.RenderTransform = new TranslateTransform();
    }

    private static string? DetectLoader(LaunchInstanceInfo instance, params string[] needles)
    {
        if (!Directory.Exists(instance.InstanceDirectory))
            return null;

        foreach (string file in Directory.EnumerateFiles(instance.InstanceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (needles.Any(needle => name.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                return SimplifyVersionName(name);
        }

        return null;
    }

    private static string? DetectModFile(LaunchInstanceInfo instance, params string[] needles)
    {
        string mods = Path.Combine(GetMinecraftRootFromInstance(instance), "mods");
        if (!Directory.Exists(mods))
            return null;

        foreach (string file in Directory.EnumerateFiles(mods, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(file);
            if (needles.Any(needle => name.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                return SimplifyVersionName(name);
        }

        return null;
    }

    private static string SimplifyVersionName(string fileName)
    {
        string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return string.IsNullOrWhiteSpace(withoutExtension) ? fileName : withoutExtension;
    }

    private void HideAllHints()
    {
        string[] names =
        [
            "HintFabricAPI",
            "HintLegacyFabricAPI",
            "HintOptiFabric",
            "HintOptiFabricOld",
            "HintLegacyOptiFabric",
            "HintModOptiFine",
            "HintQSL",
            "HintQuiltFabricAPI"
        ];

        foreach (string name in names)
        {
            if (this.FindControl<Control>(name) is { } hint)
                hint.IsVisible = false;
        }
    }

    private void HideLoading()
    {
        if (this.FindControl<Control>("PanLoad") is { } load)
        {
            load.IsVisible = false;
            load.IsHitTestVisible = false;
            load.Opacity = 0d;
        }

        if (this.FindControl<MyLoading>("LoadMinecraft") is { } loading)
            loading.Text = "正在准备安装器";
    }

    private static Bitmap? LoadBlockImage(string imageName)
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri(BlockAssetRoot + imageName, UriKind.Absolute));
            return new Bitmap(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private const string BlockAssetRoot = InstanceDisplayHelper.BlockAssetRoot;

    private void CardMinecraft_PreviewSwap(object sender, RouteEventArgs e)
    {
        e.Handled = true;
        if (_instance is not null)
            DownloadRequested?.Invoke(this, _instance);
    }

    private void CardForge_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardCleanroom_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardNeoForge_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardFabric_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardLegacyFabric_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardFabricApi_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardLegacyFabricApi_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardQuilt_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardQSL_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardLabyMod_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardOptiFabric_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private void CardLiteLoader_PreviewSwap(object sender, RouteEventArgs e) => HandleUnavailableLoader(e);

    private static void HandleUnavailableLoader(RouteEventArgs e)
    {
        e.Handled = false;
    }

    private void Forge_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("Forge", e);

    private void Cleanroom_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("Cleanroom", e);

    private void NeoForge_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("NeoForge", e);

    private void Fabric_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("Fabric", e);

    private void LegacyFabric_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("LegacyFabric", e);

    private void FabricApi_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("FabricApi", e);

    private void LegacyFabricApi_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("LegacyFabricApi", e);

    private void Quilt_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("Quilt", e);

    private void QSL_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("QSL", e);

    private void LabyMod_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("LabyMod", e);

    private void OptiFine_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("OptiFine", e);

    private void OptiFabric_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("OptiFabric", e);

    private void LiteLoader_Clear(object sender, PointerReleasedEventArgs e) => ClearLoader("LiteLoader", e);

    private void ClearLoader(string name, PointerReleasedEventArgs e)
    {
        SetLoaderInfo(name, null, "Grass.png");
        e.Handled = true;
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
}

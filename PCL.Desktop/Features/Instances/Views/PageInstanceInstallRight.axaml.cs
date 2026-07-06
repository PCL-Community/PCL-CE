// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Text.Json;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceInstallRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;

    private sealed record InstanceVersionJsonInfo(string MinecraftVersionId, IReadOnlyList<string> Libraries);

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
        string logo = InstanceDisplayHelper.ResolveLogo(instance, metadata);
        if (this.FindControl<MyListItem>("ItemSelect") is { } item)
        {
            item.Title = instance.Name;
            item.Logo = logo;
        }

        if (this.FindControl<TextBlock>("LabMinecraft") is { } label)
            label.Text = instance.Name;

        if (this.FindControl<Image>("ImgMinecraft") is { } image)
        {
            image.Source = LoadImage(logo) ?? LoadBlockImage("Grass.png");
            image.Tag = logo;
        }
    }

    private void InitializeLoaderCards(LaunchInstanceInfo instance)
    {
        CollapseLoaderCards();
        InstanceVersionJsonInfo versionInfo = ReadInstanceVersionJsonInfo(instance);
        string minecraftVersionId = versionInfo.MinecraftVersionId;
        IReadOnlyList<string> libraries = versionInfo.Libraries;
        string? forge = DetectLibrary(libraries, "net.minecraftforge:forge:", "minecraftforge") ?? DetectLoader(instance, "forge");
        string? cleanroom = DetectLibrary(libraries, "com.cleanroommc:cleanroom:", "cleanroom") ?? DetectLoader(instance, "cleanroom");
        string? neoForge = DetectLibrary(libraries, "net.neoforged:neoforge:", "net.neoforge:forge:", "neoforge") ?? DetectLoader(instance, "neoforge");
        string? fabric = DetectLibrary(libraries, "net.fabricmc:fabric-loader:") ?? DetectLoader(instance, "fabric-loader", "fabric");
        string? legacyFabric = DetectLibrary(libraries, "net.legacyfabric:", "legacyfabric") ?? DetectLoader(instance, "legacyfabric");
        string? fabricApi = DetectModFile(instance, "fabric-api");
        string? legacyFabricApi = DetectModFile(instance, "legacy-fabric-api");
        string? quilt = DetectLibrary(libraries, "org.quiltmc:quilt-loader:") ?? DetectLoader(instance, "quilt");
        string? qsl = DetectModFile(instance, "qsl", "quilted-fabric-api");
        string? labyMod = DetectLibrary(libraries, "labymod") ?? DetectLoader(instance, "labymod");
        string? optiFine = DetectLibrary(libraries, "optifine") ?? DetectLoader(instance, "optifine");
        string? optiFabric = DetectModFile(instance, "optifabric");
        string? liteLoader = DetectLibrary(libraries, "liteloader") ?? DetectLoader(instance, "liteloader");

        SetLoaderInfo("Forge", forge, "Anvil.png");
        SetLoaderInfo("Cleanroom", cleanroom, "Cleanroom.png");
        SetLoaderInfo("NeoForge", neoForge, "NeoForge.png");
        SetLoaderInfo("Fabric", fabric, "Fabric.png");
        SetLoaderInfo("LegacyFabric", legacyFabric, "Fabric.png");
        SetLoaderInfo("FabricApi", fabricApi, "Fabric.png");
        SetLoaderInfo("LegacyFabricApi", legacyFabricApi, "Fabric.png");
        SetLoaderInfo("Quilt", quilt, "Quilt.png");
        SetLoaderInfo("QSL", qsl, "Quilt.png");
        SetLoaderInfo("LabyMod", labyMod, "LabyMod.png");
        SetLoaderInfo("OptiFine", optiFine, "GrassPath.png");
        SetLoaderInfo("OptiFabric", optiFabric, "OptiFabric.png");
        SetLoaderInfo("LiteLoader", liteLoader, "Egg.png");
        ApplyLoaderCardVisibility(minecraftVersionId);
        ApplySelectedInstanceSummary(
            minecraftVersionId,
            fabric,
            legacyFabric,
            quilt,
            forge,
            neoForge,
            cleanroom,
            labyMod,
            optiFine,
            liteLoader);
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

    private void ApplyLoaderCardVisibility(string minecraftVersionId)
    {
        int vanillaDrop = MinecraftVersionRuleHelper.VersionToDrop(minecraftVersionId, allowSnapshot: true);
        SetLoaderCardVisible("LiteLoader", vanillaDrop < 130);
        SetLoaderCardVisible("Forge", MinecraftVersionRuleHelper.IsFormatFit(minecraftVersionId));
        SetLoaderCardVisible("Cleanroom", string.Equals(minecraftVersionId, "1.12.2", StringComparison.OrdinalIgnoreCase));
        SetLoaderCardVisible("NeoForge", !(vanillaDrop is > 0 and < 200));
        SetLoaderCardVisible("Fabric", vanillaDrop > 130);
        SetLoaderCardVisible("LegacyFabric", vanillaDrop <= 130);
        SetLoaderCardVisible("Quilt", vanillaDrop >= 144);
        SetLoaderCardVisible("LabyMod", vanillaDrop >= 80);
    }

    private void SetLoaderCardVisible(string name, bool visible)
    {
        if (this.FindControl<MyCard>("Card" + name) is not { } card)
            return;

        card.IsVisible = visible;
        if (!visible)
            card.IsSwapped = true;
    }

    private void ApplySelectedInstanceSummary(
        string minecraftVersionId,
        string? fabric,
        string? legacyFabric,
        string? quilt,
        string? forge,
        string? neoForge,
        string? cleanroom,
        string? labyMod,
        string? optiFine,
        string? liteLoader)
    {
        if (this.FindControl<MyListItem>("ItemSelect") is not { } item)
            return;

        List<string> parts = [minecraftVersionId];
        AddInstallPart(parts, "Common.Installation.Fabric", "Fabric", fabric?.Replace("+build", string.Empty, StringComparison.Ordinal));
        AddInstallPart(parts, "Common.Installation.LegacyFabric", "Legacy Fabric", legacyFabric);
        AddInstallPart(parts, "Common.Installation.Quilt", "Quilt", quilt);
        AddInstallPart(parts, "Common.Installation.Forge", "Forge", forge);
        AddInstallPart(parts, "Common.Installation.NeoForge", "NeoForge", neoForge);
        AddInstallPart(parts, "Common.Installation.Cleanroom", "Cleanroom", cleanroom);
        AddInstallPart(parts, "Common.Installation.LabyMod", "LabyMod", labyMod);
        AddInstallPart(parts, "Common.Installation.OptiFine", "OptiFine", optiFine);

        if (!string.IsNullOrWhiteSpace(liteLoader))
            parts.Add(ResourceText("Common.Installation.LiteLoader", "LiteLoader"));
        if (parts.Count == 1)
            parts.Add(ResourceText("Instance.Install.NoExtraInstall", "无额外安装"));

        item.Info = string.Join("  |  ", parts);
    }

    private void AddInstallPart(List<string> parts, string nameKey, string fallbackName, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return;

        parts.Add(ResourceText(nameKey, fallbackName) + " " + version);
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

    private static string ResolveMinecraftVersionId(LaunchInstanceInfo instance)
    {
        return ReadInstanceVersionJsonInfo(instance).MinecraftVersionId;
    }

    private static InstanceVersionJsonInfo ReadInstanceVersionJsonInfo(LaunchInstanceInfo instance)
    {
        if (!File.Exists(instance.VersionJsonPath))
            return new InstanceVersionJsonInfo(instance.Name, []);

        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            string inherited = ReadJsonString(root, "inheritsFrom");
            if (!string.IsNullOrWhiteSpace(inherited))
                return new InstanceVersionJsonInfo(inherited, ReadLibraryNames(root).ToArray());

            string id = ReadJsonString(root, "id");
            return new InstanceVersionJsonInfo(
                string.IsNullOrWhiteSpace(id) ? instance.Name : id,
                ReadLibraryNames(root).ToArray());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new InstanceVersionJsonInfo(instance.Name, []);
        }
    }

    private static string ReadJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

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

    private static string? DetectLibrary(IReadOnlyList<string> libraries, params string[] needles)
    {
        string? library = libraries.FirstOrDefault(library =>
            needles.Any(needle => library.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        return string.IsNullOrWhiteSpace(library) ? null : SimplifyLibraryVersion(library);
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
        return LoadImage(BlockAssetRoot + imageName);
    }

    private static Bitmap? LoadImage(string address)
    {
        try
        {
            using Stream stream = OpenImageStream(address);
            return new Bitmap(stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Stream OpenImageStream(string address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri))
        {
            if (uri.IsFile)
                return File.OpenRead(uri.LocalPath);
            if (uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase))
                return AssetLoader.Open(uri);
        }

        return File.OpenRead(address);
    }

    private string ResourceText(string key, string fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out object? value) && value is string text
            ? text
            : fallback;

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

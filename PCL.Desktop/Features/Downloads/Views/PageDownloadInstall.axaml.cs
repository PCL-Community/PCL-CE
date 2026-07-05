// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Application.Downloads;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Downloads.Views;

public partial class PageDownloadInstall : MyPageRight
{
    private static readonly MinecraftVersionCategory[] VersionCategoryOrder =
    [
        MinecraftVersionCategory.Release,
        MinecraftVersionCategory.Snapshot,
        MinecraftVersionCategory.BeforeRelease,
        MinecraftVersionCategory.AprilFools
    ];

    private static readonly string[] LoaderCardNames =
    [
        "Forge",
        "Cleanroom",
        "NeoForge",
        "Fabric",
        "LegacyFabric",
        "FabricApi",
        "LegacyFabricApi",
        "Quilt",
        "QSL",
        "LabyMod",
        "OptiFine",
        "OptiFabric",
        "LiteLoader"
    ];

    private static readonly string[] AprilFoolsVersionIds =
    [
        "15w14a",
        "1.RV-Pre1",
        "3D Shareware v1.34",
        "20w14infinite",
        "22w13oneblockatatime",
        "23w13a_or_b",
        "24w14potato",
        "25w14craftmine"
    ];

    private readonly MinecraftVanillaInstallService _installService;
    private IReadOnlyList<MinecraftVersionManifestEntry> _versions = [];
    private DownloadVersionFilter _filter = DownloadVersionFilter.All;
    private string _searchText = string.Empty;
    private MinecraftVersionManifestEntry? _selectedVersion;
    private bool _isLoading;
    private bool _isInSelectPage;
    private bool _isUpdatingSelectName;

    public PageDownloadInstall()
        : this(new MinecraftVanillaInstallService())
    {
    }

    public PageDownloadInstall(MinecraftVanillaInstallService installService)
    {
        _installService = installService;
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");

        InitializeWpfCopiedControls();
        AttachedToVisualTree += (_, _) =>
        {
            if (_versions.Count == 0 && !_isLoading)
                _ = RefreshVersionsAsync();
        };
    }

    public event EventHandler<MinecraftVersionManifestEntry>? InstallRequested;

    public async Task FocusVersionAsync(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return;

        ExitSelectPage();
        _filter = DownloadVersionFilter.All;
        if (this.FindControl<MySearchBar>("TextSearchVersion") is { } searchBar)
            searchBar.Text = string.Empty;

        if (_versions.Count == 0 && !_isLoading)
            await RefreshVersionsAsync().ConfigureAwait(true);

        if (TryFindVersion(versionId, out MinecraftVersionManifestEntry version))
        {
            ReloadVersionList();
            SelectVersion(version);
            return;
        }

        if (this.FindControl<MySearchBar>("TextSearchVersion") is { } fallbackSearchBar)
            fallbackSearchBar.Text = versionId;
        ReloadVersionList();
    }

    public void ApplyVersionFilter(DownloadVersionFilter filter)
    {
        ExitSelectPage();
        _filter = filter;
        if (this.FindControl<StackPanel>("PanMinecraft")?.Children.Count > 0)
            ApplyRenderedFilters();
        else
            ReloadVersionList();
    }

    public async Task RefreshVersionsAsync()
    {
        await RunOnUiThreadAsync(() =>
        {
            _isLoading = true;
            SetLoadingVisible(true);
            SetVersionListMessage("正在获取 Minecraft 版本列表。");
        }).ConfigureAwait(false);

        try
        {
            IReadOnlyList<MinecraftVersionManifestEntry> versions =
                await _installService.GetVersionManifestAsync(preferOfficialSource: true).ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                _versions = versions;
                ReloadVersionList();
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => SetVersionListMessage("获取版本列表失败：" + ex.Message)).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                _isLoading = false;
                SetLoadingVisible(false);
            }).ConfigureAwait(false);
        }
    }

    public void ExitSelectPage()
    {
        if (!_isInSelectPage)
        {
            ApplySelectPageState(isSelectPage: false);
            return;
        }

        _isInSelectPage = false;
        _selectedVersion = null;
        PrepareExitSelectPageAnimationState();

        if (TryGetTranslateX("PanSelect", out double selectX) &&
            TryGetTranslateX("PanMinecraft", out double minecraftX))
        {
            Control? panSelect = this.FindControl<Control>("PanSelect");
            Control? panMinecraft = this.FindControl<Control>("PanMinecraft");
            if (panSelect is not null && panMinecraft is not null)
            {
                ModAnimation.AniStart(
                    new List<ModAnimation.AniData>
                    {
                        ModAnimation.AaOpacity(panSelect, -panSelect.Opacity, 70, 10),
                        ModAnimation.AaTranslateX(panSelect, 50d - selectX, 90, 10),
                        ModAnimation.AaCode(() => this.FindControl<MyScrollViewer>("PanBack")?.ScrollToHome(), after: true),
                        ModAnimation.AaOpacity(panMinecraft, 1d - panMinecraft.Opacity, 70, 100),
                        ModAnimation.AaTranslateX(
                            panMinecraft,
                            -minecraftX,
                            160,
                            100,
                            new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                        ModAnimation.AaCode(() => ApplySelectPageState(isSelectPage: false), after: true)
                    },
                    "FrmDownloadInstall SelectPageSwitch");
                return;
            }
        }

        ApplySelectPageState(isSelectPage: false);
    }

    public new void PageOnEnter()
    {
        base.PageOnEnter();
        if (_isInSelectPage)
            ExitSelectPage();
    }

    private void InitializeWpfCopiedControls()
    {
        if (this.FindControl<MyIconButton>("BtnBack") is { } backButton)
            backButton.Click += (_, _) => ExitSelectPage();

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } startButton)
        {
            startButton.Show = false;
            startButton.Click += (_, _) => StartSelectedInstall();
        }

        if (this.FindControl<MySearchBar>("TextSearchVersion") is { } searchBar)
        {
            searchBar.TextChanged += (_, _) =>
            {
                _searchText = searchBar.Text?.Trim() ?? string.Empty;
                ApplyRenderedFilters();
            };
        }

        if (this.FindControl<MyTextBox>("TextSelectName") is { } selectName)
        {
            selectName.TextChanged += TextSelectName_TextChanged;
            selectName.KeyDown += TextSelectName_KeyDown;
        }

        if (this.FindControl<MyLoading>("LoadMinecraft") is { } loading)
            loading.Text = "正在获取 Minecraft 版本列表";

        InitializeLoaderCards();
        HideAllHints();
        ApplySelectPageState(isSelectPage: false);
        SetLoadingVisible(false);
    }

    private void ReloadVersionList()
    {
        StackPanel? panel = this.FindControl<StackPanel>("PanMinecraft");
        if (panel is null)
            return;

        IReadOnlyList<DownloadVersionView> visible = BuildVersionViews(_versions);
        panel.Children.Clear();
        if (visible.Count == 0)
        {
            panel.Children.Add(CreateMessageCard(
                "没有找到匹配的版本",
                "可以清空搜索词，或在左侧切换到“全部版本”后再试。"));
            return;
        }

        Dictionary<MinecraftVersionCategory, List<DownloadVersionView>> categories = CreateVersionDictionary(visible);
        AddLatestVersionCard(panel, categories);
        AddOtherVersionsCard(panel, categories);
        ApplyRenderedFilters();
    }

    private bool TryFindVersion(string versionId, out MinecraftVersionManifestEntry version)
    {
        foreach (MinecraftVersionManifestEntry entry in _versions)
        {
            MinecraftVersionClassification classification = MinecraftVersionCatalogClassifier.Classify(entry);
            if (string.Equals(entry.Id, versionId, StringComparison.OrdinalIgnoreCase))
            {
                version = entry;
                return true;
            }

            if (string.Equals(classification.Id, versionId, StringComparison.OrdinalIgnoreCase))
            {
                version = entry with
                {
                    Id = classification.Id,
                    Type = classification.Type
                };
                return true;
            }
        }

        version = default!;
        return false;
    }

    private static bool IsAprilFoolsVersion(string id)
    {
        foreach (string knownId in AprilFoolsVersionIds)
        {
            if (string.Equals(id, knownId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return id.Contains("infinite", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("shareware", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("potato", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("craftmine", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<MinecraftVersionCategory, List<DownloadVersionView>> CreateVersionDictionary(
        IReadOnlyList<DownloadVersionView> versions)
    {
        Dictionary<MinecraftVersionCategory, List<DownloadVersionView>> categories = VersionCategoryOrder.ToDictionary(
            category => category,
            _ => new List<DownloadVersionView>());
        foreach (DownloadVersionView version in versions)
            categories[version.Category].Add(version);

        foreach (MinecraftVersionCategory category in VersionCategoryOrder)
            categories[category] = categories[category]
                .OrderByDescending(version => version.ReleaseTime ?? DateTimeOffset.MinValue)
                .ToList();

        return categories;
    }

    private void AddLatestVersionCard(
        StackPanel panel,
        IReadOnlyDictionary<MinecraftVersionCategory, List<DownloadVersionView>> categories)
    {
        DownloadVersionView? latestRelease = categories[MinecraftVersionCategory.Release].FirstOrDefault();
        DownloadVersionView? latestSnapshot = categories[MinecraftVersionCategory.Snapshot].FirstOrDefault();
        List<DownloadVersionView> latest = [];

        if (latestRelease is not null)
        {
            latest.Add(latestRelease with
            {
                Info = ResourceText(
                    "Download.Version.Latest.Release",
                    "最新正式版，发布于 {0}",
                    FormatReleaseTime(latestRelease.ReleaseTime))
            });
        }

        if (latestSnapshot is not null &&
            (latestRelease is null ||
             (latestRelease.ReleaseTime ?? DateTimeOffset.MinValue) < (latestSnapshot.ReleaseTime ?? DateTimeOffset.MinValue)))
        {
            latest.Add(latestSnapshot with
            {
                Info = ResourceText(
                    "Download.Version.Latest.Development",
                    "最新预览版，发布于 {0}",
                    FormatReleaseTime(latestSnapshot.ReleaseTime))
            });
        }

        if (latest.Count == 0)
            return;

        panel.Children.Add(CreateVersionCard(
            ResourceText("Download.Version.Latest.Title", "最新版本"),
            latest,
            filterable: false,
            margin: new Thickness(0d, 15d, 0d, 15d)));
    }

    private void AddOtherVersionsCard(
        StackPanel panel,
        IReadOnlyDictionary<MinecraftVersionCategory, List<DownloadVersionView>> categories)
    {
        List<DownloadVersionView> allVersions = [];
        foreach (MinecraftVersionCategory category in VersionCategoryOrder)
            allVersions.AddRange(categories[category]);

        if (allVersions.Count == 0)
            return;

        panel.Children.Add(CreateVersionCard(
            ResourceText("Download.Version.Other.Title", "其他版本"),
            allVersions
                .OrderByDescending(version => version.ReleaseTime ?? DateTimeOffset.MinValue)
                .ToArray(),
            filterable: true,
            margin: new Thickness(0d, 0d, 0d, 15d)));
    }

    private MyCard CreateVersionCard(
        string title,
        IReadOnlyList<DownloadVersionView> versions,
        bool filterable,
        Thickness margin)
    {
        StackPanel stack = new()
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(),
            Tag = versions
        };
        MyCard card = new()
        {
            Title = title,
            Margin = margin,
            SwapControl = stack
        };
        card.Children.Add(stack);

        void Install(StackPanel target)
        {
            if (target.Tag is not IReadOnlyList<DownloadVersionView> entries)
                return;

            foreach (DownloadVersionView version in entries)
                target.Children.Add(CreateVersionItem(version, filterable));
        }

        MyCard.StackInstall(ref stack, Install);
        return card;
    }

    private MyListItem CreateVersionItem(DownloadVersionView version, bool filterable)
    {
        MyIconButton installIcon = new()
        {
            SvgIcon = "lucide/download",
            ToolTip = "选择并下载",
            LogoScale = 0.95d
        };
        installIcon.Click += (_, _) => SelectVersion(version.Manifest);

        MyListItem item = new()
        {
            Title = version.Title,
            Info = version.Info,
            Type = MyListItem.CheckType.Clickable,
            Logo = version.Logo,
            LogoScale = 1d,
            Height = 42d,
            Margin = new Thickness(0, 0, 0, 2),
            Buttons = [installIcon],
            Tag = new VersionListItemTag(version, filterable)
        };
        item.Click += (_, _) => SelectVersion(version.Manifest);
        return item;
    }

    private DownloadVersionView[] BuildVersionViews(IReadOnlyList<MinecraftVersionManifestEntry> versions)
    {
        DownloadVersionView[] views = new DownloadVersionView[versions.Count];
        for (int i = 0; i < versions.Count; i++)
            views[i] = CreateVersionView(versions[i]);
        return views;
    }

    private DownloadVersionView CreateVersionView(MinecraftVersionManifestEntry version)
    {
        MinecraftVersionClassification classification = MinecraftVersionCatalogClassifier.Classify(version);
        string id = classification.Id;
        string type = classification.Type;
        DateTimeOffset? releaseTime = version.ReleaseTime;
        string title = MinecraftVersionCatalogClassifier.FormatVersion(id).Replace("_", " ", StringComparison.Ordinal);
        string lore = CreateAprilFoolsLore(classification.AprilFoolsDescriptor);
        string info = CreateVersionInfo(id, title, lore, releaseTime);
        MinecraftVersionManifestEntry manifest = version with
        {
            Id = id,
            Type = type,
            ReleaseTime = releaseTime
        };

        return new DownloadVersionView(
            manifest,
            title,
            info,
            releaseTime,
            classification.Category,
            GetVersionLogoUri(type));
    }

    private static string CreateVersionInfo(string id, string formattedTitle, string lore, DateTimeOffset? releaseTime)
    {
        if (string.IsNullOrEmpty(lore))
        {
            string date = FormatReleaseTime(releaseTime);
            return string.Equals(formattedTitle, id, StringComparison.Ordinal)
                ? date
                : $"{date} | {id}";
        }

        return string.Equals(formattedTitle, id, StringComparison.Ordinal)
            ? lore
            : $"{lore} | {id}";
    }

    private static string FormatReleaseTime(DateTimeOffset? releaseTime)
    {
        return releaseTime?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "未知日期";
    }

    private void ApplyRenderedFilters()
    {
        foreach (MyListItem item in this.GetVisualDescendants().OfType<MyListItem>())
        {
            if (item.Tag is not VersionListItemTag tag)
                continue;

            bool categoryVisible = !tag.IsFilterable || IsVisibleByFilter(tag.Version.Category);
            bool searchVisible = string.IsNullOrWhiteSpace(_searchText) ||
                                 tag.Version.Manifest.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                                 tag.Version.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
            item.IsVisible = categoryVisible && searchVisible;
            item.Opacity = item.IsVisible ? 1d : 0d;
        }
    }

    private bool IsVisibleByFilter(MinecraftVersionCategory category)
    {
        return _filter switch
        {
            DownloadVersionFilter.Release => category == MinecraftVersionCategory.Release,
            DownloadVersionFilter.Snapshot => category == MinecraftVersionCategory.Snapshot,
            DownloadVersionFilter.BeforeRelease => category == MinecraftVersionCategory.BeforeRelease,
            DownloadVersionFilter.AprilFools => category == MinecraftVersionCategory.AprilFools,
            _ => true
        };
    }

    private void SelectVersion(MinecraftVersionManifestEntry version)
    {
        _selectedVersion = version;
        _isInSelectPage = true;
        SetSelectName(version.Id);
        SetSelectedLogo(version);
        InitializeLoaderCards();
        HideAllHints();

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } button)
        {
            button.IsEnabled = IsValidInstallName(this.FindControl<MyTextBox>("TextSelectName")?.Text);
            button.Show = true;
        }

        Control? panMinecraft = this.FindControl<Control>("PanMinecraft");
        Control? panSelect = this.FindControl<Control>("PanSelect");
        ApplySelectPageState(isSelectPage: true, beforeEnterAnimation: true);
        if (panMinecraft is not null && panSelect is not null &&
            TryGetTranslateX("PanMinecraft", out double minecraftX) &&
            TryGetTranslateX("PanSelect", out double selectX))
        {
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaOpacity(panMinecraft, -panMinecraft.Opacity, 70, 10),
                    ModAnimation.AaTranslateX(panMinecraft, -50d - minecraftX, 90, 10),
                    ModAnimation.AaCode(() => panMinecraft.IsVisible = false, after: true),
                    ModAnimation.AaOpacity(panSelect, 1d - panSelect.Opacity, 70, 100),
                    ModAnimation.AaTranslateX(
                        panSelect,
                        -selectX,
                        160,
                        100,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.ExtraStrong)),
                    ModAnimation.AaCode(() => SetScrollHitTestVisible(true), after: true)
                },
                "FrmDownloadInstall SelectPageSwitch",
                refreshTime: true);
            return;
        }

        ApplySelectPageState(isSelectPage: true);
    }

    private void InitializeLoaderCards()
    {
        const string canAdd = "可添加";
        CollapseLoaderCards();
        SetLoaderInfo("Forge", canAdd, iconVisible: false);
        SetLoaderInfo("Cleanroom", canAdd, iconVisible: false);
        SetLoaderInfo("NeoForge", canAdd, iconVisible: false);
        SetLoaderInfo("Fabric", canAdd, iconVisible: false);
        SetLoaderInfo("LegacyFabric", canAdd, iconVisible: false);
        SetLoaderInfo("FabricApi", canAdd, iconVisible: false);
        SetLoaderInfo("LegacyFabricApi", canAdd, iconVisible: false);
        SetLoaderInfo("Quilt", canAdd, iconVisible: false);
        SetLoaderInfo("QSL", canAdd, iconVisible: false);
        SetLoaderInfo("LabyMod", canAdd, iconVisible: false);
        SetLoaderInfo("OptiFine", canAdd, iconVisible: false);
        SetLoaderInfo("OptiFabric", canAdd, iconVisible: false);
        SetLoaderInfo("LiteLoader", canAdd, iconVisible: false);
    }

    private void CollapseLoaderCards()
    {
        foreach (string name in LoaderCardNames)
        {
            if (this.FindControl<MyCard>("Card" + name) is { } card)
                card.IsSwapped = true;
        }
    }

    private void SetLoaderInfo(string name, string text, bool iconVisible)
    {
        if (this.FindControl<TextBlock>("Lab" + name) is { } label)
        {
            label.Text = text;
            label.Foreground = LegacyResourceResolver.Brush(label, "ColorBrushGray4", "#8c8c8c");
        }

        if (this.FindControl<Image>("Img" + name) is { } image)
            image.IsVisible = iconVisible;

        if (this.FindControl<Control>("Btn" + name + "Clear") is { } clearButton)
            clearButton.IsVisible = false;
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

    private void ApplySelectPageState(bool isSelectPage, bool beforeEnterAnimation = false)
    {
        if (this.FindControl<Control>("TextSearchVersion") is { } search)
            search.IsVisible = !isSelectPage;

        if (this.FindControl<Grid>("PanAllBack") is { RowDefinitions.Count: > 0 } allBack)
            allBack.RowDefinitions[0].Height = new GridLength(isSelectPage ? 0d : 54d, GridUnitType.Pixel);

        if (this.FindControl<Grid>("PanInner") is { } inner)
            inner.Margin = isSelectPage
                ? new Thickness(25d, 10d, 25d, 40d)
                : new Thickness(25d, 10d, 25d, 25d);

        if (isSelectPage && this.FindControl<MyScrollViewer>("PanBack") is { } scroll)
            scroll.ScrollToHome();
        SetScrollHitTestVisible(!isSelectPage || !beforeEnterAnimation);

        if (this.FindControl<Control>("PanMinecraft") is { } minecraft)
        {
            minecraft.IsVisible = !isSelectPage || beforeEnterAnimation;
            minecraft.Opacity = isSelectPage && !beforeEnterAnimation ? 0d : 1d;
            minecraft.IsHitTestVisible = !isSelectPage;
        }

        if (this.FindControl<Control>("PanSelect") is { } select)
        {
            select.IsVisible = isSelectPage;
            select.Opacity = isSelectPage && !beforeEnterAnimation ? 1d : 0d;
            select.IsHitTestVisible = isSelectPage;
            if (select.RenderTransform is TranslateTransform transform && !isSelectPage)
                transform.X = 40d;
        }

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } button)
            button.Show = isSelectPage;
    }

    private void PrepareExitSelectPageAnimationState()
    {
        if (this.FindControl<Control>("TextSearchVersion") is { } search)
            search.IsVisible = true;

        if (this.FindControl<Grid>("PanAllBack") is { RowDefinitions.Count: > 0 } allBack)
            allBack.RowDefinitions[0].Height = new GridLength(54d, GridUnitType.Pixel);

        if (this.FindControl<Grid>("PanInner") is { } inner)
            inner.Margin = new Thickness(25d, 10d, 25d, 25d);

        if (this.FindControl<Control>("PanSelect") is { } select)
            select.IsHitTestVisible = false;

        if (this.FindControl<Control>("PanMinecraft") is { } minecraft)
        {
            minecraft.IsVisible = true;
            minecraft.IsHitTestVisible = true;
        }

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } button)
            button.Show = false;

        SetScrollHitTestVisible(false);
        if (this.FindControl<MyScrollViewer>("PanBack") is { } scroll)
            scroll.ScrollToHome();
    }

    private void SetScrollHitTestVisible(bool isVisible)
    {
        if (this.FindControl<MyScrollViewer>("PanBack") is { } scroll)
            scroll.IsHitTestVisible = isVisible;
    }

    private void SetLoadingVisible(bool visible)
    {
        if (this.FindControl<Control>("PanLoad") is { } panLoad)
        {
            panLoad.IsVisible = visible;
            panLoad.Opacity = visible ? 1d : 0d;
            panLoad.IsHitTestVisible = visible;
        }

        if (this.FindControl<Control>("PanAllBack") is { } content)
        {
            content.IsVisible = !visible;
            content.IsHitTestVisible = !visible;
        }

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } startButton && visible)
            startButton.Show = false;
    }

    private static bool TryGetTranslate(Control? control, out TranslateTransform transform)
    {
        if (control?.RenderTransform is TranslateTransform existing)
        {
            transform = existing;
            return true;
        }

        if (control is not null)
        {
            transform = new TranslateTransform();
            control.RenderTransform = transform;
            return true;
        }

        transform = new TranslateTransform();
        return false;
    }

    private bool TryGetTranslateX(string name, out double x)
    {
        Control? control = this.FindControl<Control>(name);
        if (TryGetTranslate(control, out TranslateTransform transform))
        {
            x = transform.X;
            return true;
        }

        x = 0d;
        return false;
    }

    private void SetVersionListMessage(string message)
    {
        StackPanel? panel = this.FindControl<StackPanel>("PanMinecraft");
        if (panel is null)
            return;

        panel.Children.Clear();
        panel.Children.Add(CreateMessageCard("Minecraft", message));
    }

    private static MyCard CreateMessageCard(string title, string message)
    {
        MyCard card = new()
        {
            Title = title,
            Margin = new Thickness(0d, 0d, 0d, 15d),
            UseAnimation = false
        };
        card.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13.5d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(25d, 38d, 23d, 16d)
        });
        return card;
    }

    private void SetSelectName(string text)
    {
        if (this.FindControl<MyTextBox>("TextSelectName") is not { } box)
            return;

        _isUpdatingSelectName = true;
        box.Text = text;
        _isUpdatingSelectName = false;
        RefreshSelectNameValidation();
    }

    private void SetSelectedLogo(MinecraftVersionManifestEntry version)
    {
        if (this.FindControl<MyImage>("ImgLogo") is not { } image)
            return;

        image.Source = LoadBlockImage(GetVersionLogoImageName(version));
    }

    private static string GetVersionLogoUri(string type) =>
        $"avares://PCL.Desktop/WpfOriginal/Images/Blocks/{GetVersionLogoImageName(type)}";

    private static string GetVersionLogoImageName(MinecraftVersionManifestEntry version)
    {
        if (IsAprilFoolsVersion(version.Id))
            return "GoldBlock.png";

        return GetVersionLogoImageName(version.Type);
    }

    private static string GetVersionLogoImageName(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "release" => "Grass.png",
            "snapshot" or "pending" => "CommandBlock.png",
            "special" => "GoldBlock.png",
            _ => "CobbleStone.png"
        };
    }

    private static Bitmap? LoadBlockImage(string imageName)
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri($"avares://PCL.Desktop/WpfOriginal/Images/Blocks/{imageName}", UriKind.Absolute));
            return new Bitmap(stream);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void TextSelectName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdatingSelectName)
            return;

        RefreshSelectNameValidation();
    }

    private void TextSelectName_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && this.FindControl<MyExtraTextButton>("BtnStart") is { IsEnabled: true })
            StartSelectedInstall();
    }

    private void StartSelectedInstall()
    {
        if (_selectedVersion is null || !TryGetInstallName(out string installName))
            return;

        MinecraftVersionManifestEntry request = string.Equals(installName, _selectedVersion.Id, StringComparison.Ordinal)
            ? _selectedVersion
            : _selectedVersion with { Id = installName };
        InstallRequested?.Invoke(this, request);
    }

    private void RefreshSelectNameValidation()
    {
        MyTextBox? box = this.FindControl<MyTextBox>("TextSelectName");
        bool isValid = IsValidInstallName(box?.Text);
        if (box is not null)
        {
            box.ValidateResult = isValid
                ? string.Empty
                : "版本名称不能包含 \\ / : * ? \" < > |，也不能为空。";
        }

        if (this.FindControl<MyExtraTextButton>("BtnStart") is { } button)
            button.IsEnabled = isValid;
    }

    private bool TryGetInstallName(out string installName)
    {
        installName = this.FindControl<MyTextBox>("TextSelectName")?.Text?.Trim() ?? string.Empty;
        return IsValidInstallName(installName);
    }

    private static bool IsValidInstallName(string? text)
    {
        string name = text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            return false;

        char[] invalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
        return name.IndexOfAny(invalidChars) < 0 &&
               name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
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
            : string.Format(CultureInfo.CurrentCulture, text, args);
    }

    private string CreateAprilFoolsLore(MinecraftAprilFoolsDescriptor? descriptor)
    {
        if (descriptor is not MinecraftAprilFoolsDescriptor value)
            return string.Empty;

        string description = ResourceText(value.DescriptionResourceKey, string.Empty);
        string tag = value.TagResourceKey is null
            ? string.Empty
            : ResourceText(value.TagResourceKey, string.Empty);
        return description + tag;
    }

    private sealed record DownloadVersionView(
        MinecraftVersionManifestEntry Manifest,
        string Title,
        string Info,
        DateTimeOffset? ReleaseTime,
        MinecraftVersionCategory Category,
        string Logo);

    private sealed record VersionListItemTag(DownloadVersionView Version, bool IsFilterable);
}

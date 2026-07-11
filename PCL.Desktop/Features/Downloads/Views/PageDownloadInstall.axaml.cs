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
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Downloads.Views;

public sealed record DownloadInstallRequest(
    string VersionId,
    string BaseVersionId,
    string VersionJsonUrl,
    MinecraftLoaderInstallRequest? Loader,
    string? MinecraftRootDirectory = null,
    bool ReplaceExistingVersion = false,
    IReadOnlyList<MinecraftInstallAddonRequest>? Addons = null);

public partial class PageDownloadInstall : MyPageRight
{
    private static readonly MinecraftVersionCategory[] VersionCategoryOrder =
    [
        MinecraftVersionCategory.Release,
        MinecraftVersionCategory.Snapshot,
        MinecraftVersionCategory.BeforeRelease,
        MinecraftVersionCategory.AprilFools
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
    private readonly IMinecraftLoaderMetadataService _loaderMetadataService;
    private readonly IMinecraftInstallAddonMetadataService _addonMetadataService;
    private readonly Dictionary<string, LoaderSupportState> _loaderStates = [];
    private readonly Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>> _loaderVersionCache = [];
    private readonly Dictionary<(MinecraftLoaderKind Kind, string GameVersion), Task<IReadOnlyList<MinecraftLoaderVersionEntry>>> _loaderVersionLoads = [];
    private readonly Dictionary<(MinecraftLoaderKind Kind, string GameVersion), string> _loaderVersionErrors = [];
    private readonly Dictionary<(MinecraftInstallAddonKind Kind, string GameVersion), IReadOnlyList<MinecraftInstallAddonVersionEntry>> _addonVersionCache = [];
    private readonly Dictionary<(MinecraftInstallAddonKind Kind, string GameVersion), Task<IReadOnlyList<MinecraftInstallAddonVersionEntry>>> _addonVersionLoads = [];
    private readonly Dictionary<(MinecraftInstallAddonKind Kind, string GameVersion), string> _addonVersionErrors = [];
    private readonly Dictionary<MinecraftInstallAddonKind, MinecraftInstallAddonVersionEntry> _selectedAddons = [];
    private IReadOnlyList<MinecraftVersionManifestEntry> _versions = [];
    private DownloadVersionFilter _filter = DownloadVersionFilter.All;
    private string _searchText = string.Empty;
    private MinecraftVersionManifestEntry? _selectedVersion;
    private MinecraftLoaderKind? _selectedLoaderKind;
    private MinecraftLoaderVersionEntry? _selectedLoaderVersion;
    private MinecraftLoaderVersionEntry? _selectedOptiFineAddon;
    private string? _preferredInstallName;
    private string? _targetMinecraftRootDirectory;
    private bool _replaceExistingVersion;
    private bool _preserveInstallNameOnLoaderSelect;
    private bool _isLoading;
    private bool _isInSelectPage;
    private bool _isUpdatingSelectName;
    private bool _keepSelectPageOnNextEnter;

    public PageDownloadInstall()
        : this(new MinecraftVanillaInstallService(), new MinecraftLoaderMetadataService(), new MinecraftInstallAddonMetadataService())
    {
    }

    public PageDownloadInstall(MinecraftVanillaInstallService installService)
        : this(installService, new MinecraftLoaderMetadataService(), new MinecraftInstallAddonMetadataService())
    {
    }

    public PageDownloadInstall(
        MinecraftVanillaInstallService installService,
        IMinecraftLoaderMetadataService loaderMetadataService,
        IMinecraftInstallAddonMetadataService? addonMetadataService = null)
    {
        _installService = installService;
        _loaderMetadataService = loaderMetadataService;
        _addonMetadataService = addonMetadataService ?? new MinecraftInstallAddonMetadataService();
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");

        InitializeWpfCopiedControls();
        AttachedToVisualTree += (_, _) =>
        {
            if (_versions.Count == 0 && !_isLoading)
                _ = RefreshVersionsAsync();
        };
    }

    public event EventHandler<DownloadInstallRequest>? InstallRequested;

    public bool HasPendingFocusedNavigation => _keepSelectPageOnNextEnter;

    public async Task FocusVersionAsync(
        string versionId,
        string? installName = null,
        bool preserveInstallNameOnLoaderSelect = false,
        string? minecraftRootDirectory = null,
        MinecraftLoaderKind? openLoaderKind = null,
        bool replaceExistingVersion = false)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return;

        _preferredInstallName = string.IsNullOrWhiteSpace(installName) ? null : installName.Trim();
        _preserveInstallNameOnLoaderSelect = preserveInstallNameOnLoaderSelect;
        _targetMinecraftRootDirectory = string.IsNullOrWhiteSpace(minecraftRootDirectory) ? null : minecraftRootDirectory;
        _replaceExistingVersion = replaceExistingVersion;
        _keepSelectPageOnNextEnter = !this.IsAttachedToVisualTree();

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
            if (openLoaderKind is { } loaderKind)
                await OpenLoaderCardAsync(loaderKind).ConfigureAwait(true);
            return;
        }

        if (this.FindControl<MySearchBar>("TextSearchVersion") is { } fallbackSearchBar)
            fallbackSearchBar.Text = versionId;
        ReloadVersionList();
    }

    public async Task OpenLoaderCardAsync(MinecraftLoaderKind kind)
    {
        if (_selectedVersion is null)
            return;

        string name = GetLoaderCardName(kind);
        if (!_loaderStates.TryGetValue(name, out LoaderSupportState? state) || !state.IsVisible)
            return;

        if (!state.CanOpen)
        {
            if (!await EnsureLoaderVersionsRenderedAsync(name).ConfigureAwait(true))
                return;
            ReloadSelectedLoaderCards();
            if (!_loaderStates.TryGetValue(name, out state) || !state.CanOpen)
                return;
        }

        if (this.FindControl<MyCard>("Card" + name) is not { } card)
            return;

        card.IsSwapped = false;
        RefreshLoaderInfoPanel(name);
        PopulateLoaderVersionList(name, kind, _loaderVersionCache[(kind, _selectedVersion.Id)]);
    }

    public async Task FocusExistingInstallAddonAsync(
        string gameVersion,
        string installName,
        string minecraftRootDirectory,
        MinecraftLoaderKind currentLoaderKind,
        string currentLoaderVersion,
        MinecraftInstallAddonKind addonKind,
        string? currentOptiFineVersion = null)
    {
        await FocusVersionAsync(
                gameVersion,
                installName,
                preserveInstallNameOnLoaderSelect: true,
                minecraftRootDirectory: minecraftRootDirectory,
                replaceExistingVersion: true)
            .ConfigureAwait(true);
        if (_selectedVersion is null)
            return;

        MinecraftLoaderVersionEntry loader = new(currentLoaderKind, currentLoaderVersion, true);
        _loaderVersionCache[(currentLoaderKind, _selectedVersion.Id)] = [loader];
        _selectedLoaderKind = currentLoaderKind;
        _selectedLoaderVersion = loader;
        if (!string.IsNullOrWhiteSpace(currentOptiFineVersion))
        {
            _selectedOptiFineAddon = new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.OptiFine,
                currentOptiFineVersion,
                true);
        }

        ReloadSelectedLoaderCards();
        BeginLoaderVersionPreload();
        await OpenAddonCardAsync(addonKind).ConfigureAwait(true);
    }

    public async Task OpenAddonCardAsync(MinecraftInstallAddonKind kind)
    {
        if (_selectedVersion is null)
            return;

        DownloadAddonDescriptor addon = DownloadAddonRegistry.All.ToArray().FirstOrDefault(candidate => candidate.Kind == kind);
        if (string.IsNullOrWhiteSpace(addon.CardName) ||
            !_loaderStates.TryGetValue(addon.CardName, out LoaderSupportState? state) ||
            !state.IsVisible)
        {
            return;
        }

        if (!state.CanOpen)
        {
            if (!await EnsureAddonVersionsRenderedAsync(addon.CardName).ConfigureAwait(true))
                return;
            ReloadSelectedLoaderCards();
        }

        if (this.FindControl<MyCard>("Card" + addon.CardName) is not { } card ||
            !_addonVersionCache.TryGetValue((kind, _selectedVersion.Id), out IReadOnlyList<MinecraftInstallAddonVersionEntry>? versions))
        {
            return;
        }

        PopulateAddonVersionList(addon.CardName, addon, versions);
        card.IsSwapped = false;
        RefreshLoaderInfoPanel(addon.CardName);
    }

    public void ClearInstallTargetOverride()
    {
        _preferredInstallName = null;
        _targetMinecraftRootDirectory = null;
        _preserveInstallNameOnLoaderSelect = false;
        _replaceExistingVersion = false;
        _keepSelectPageOnNextEnter = false;
    }

    public void ApplyVersionFilter(DownloadVersionFilter filter)
    {
        if (!_keepSelectPageOnNextEnter)
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
        bool keepSelectPage = _keepSelectPageOnNextEnter;
        _keepSelectPageOnNextEnter = false;
        if (_isInSelectPage && !keepSelectPage)
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
        WireLoaderCards();
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
            ControlVisualHelpers.AnimateListEntrance(panel, "Download Version List");
            return;
        }

        Dictionary<MinecraftVersionCategory, List<DownloadVersionView>> categories = CreateVersionDictionary(visible);
        AddLatestVersionCard(panel, categories);
        AddOtherVersionsCard(panel, categories);
        ApplyRenderedFilters();
        ControlVisualHelpers.AnimateListEntrance(panel, "Download Version List");
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
        ResetSelectedLoader();
        _isInSelectPage = true;
        SetSelectName(version.Id);
        if (!string.IsNullOrWhiteSpace(_preferredInstallName))
            SetSelectName(_preferredInstallName);
        SetSelectedLogo(version);
        ReloadSelectedLoaderCards();
        BeginLoaderVersionPreload();
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
        CollapseLoaderCards();
        foreach (MinecraftLoaderCardDescriptor card in MinecraftLoaderCardRegistry.AllCards)
            SetLoaderInfo(card.ControlSuffix, LoaderSupportState.VisibleClosed(CanAddText()));
    }

    private void WireLoaderCards()
    {
        foreach (MinecraftLoaderCardDescriptor loaderCard in MinecraftLoaderCardRegistry.AllCards)
        {
            string name = loaderCard.ControlSuffix;
            if (this.FindControl<MyCard>("Card" + name) is not { } card)
                continue;

            card.PreviewSwap += (_, args) =>
            {
                if (!_loaderStates.TryGetValue(name, out LoaderSupportState? state) || !state.CanOpen)
                    args.Handled = true;
            };
            card.Swap += (_, _) =>
            {
                RefreshLoaderInfoPanel(name);
            };

            if (this.FindControl<Control>("Btn" + name + "Clear") is { } clearButton)
            {
                clearButton.PointerReleased += (_, args) =>
                {
                    ClearSelectedLoader(name);
                    args.Handled = true;
                };
            }
        }
    }

    private void ReloadSelectedLoaderCards()
    {
        if (_selectedVersion is null)
        {
            InitializeLoaderCards();
            return;
        }

        CollapseLoaderCards();
        HideAllHints();

        string versionId = _selectedVersion.Id;
        int vanillaDrop = MinecraftVersionRuleHelper.VersionToDrop(versionId, allowSnapshot: true);
        bool formatFit = MinecraftVersionRuleHelper.IsFormatFit(versionId);
        string canAdd = CanAddText();
        string? incompatibleLoader = _selectedLoaderKind is null
            ? null
            : ResourceText(
                "Download.Install.Compat.IncompatibleWithLoader",
                "与 {0} 不兼容",
                GetLoaderDisplayName(_selectedLoaderKind.Value));

        SetLoaderInfo("OptiFine", CreateOptiFineState(canAdd));
        SetLoaderInfo("LiteLoader", vanillaDrop >= 130
            ? LoaderSupportState.Hidden()
            : CreateLoaderState(MinecraftLoaderKind.LiteLoader, canAdd, incompatibleLoader));
        SetLoaderInfo("Forge", formatFit
            ? CreateLoaderState(MinecraftLoaderKind.Forge, canAdd, incompatibleLoader)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("Cleanroom", string.Equals(versionId, "1.12.2", StringComparison.OrdinalIgnoreCase)
            ? CreateLoaderState(MinecraftLoaderKind.Cleanroom, canAdd, incompatibleLoader)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("NeoForge", vanillaDrop is > 0 and < 200
            ? LoaderSupportState.Hidden()
            : CreateLoaderState(MinecraftLoaderKind.NeoForge, canAdd, incompatibleLoader));
        SetLoaderInfo("Fabric", vanillaDrop > 130
            ? CreateLoaderState(MinecraftLoaderKind.Fabric, canAdd, incompatibleLoader)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("LegacyFabric", vanillaDrop > 130
            ? LoaderSupportState.Hidden()
            : CreateLoaderState(MinecraftLoaderKind.LegacyFabric, canAdd, incompatibleLoader));
        SetLoaderInfo("Quilt", vanillaDrop >= 144
            ? CreateLoaderState(MinecraftLoaderKind.Quilt, canAdd, incompatibleLoader)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("LabyMod", vanillaDrop >= 80
            ? CreateLoaderState(MinecraftLoaderKind.LabyMod, canAdd, incompatibleLoader)
            : LoaderSupportState.Hidden());

        SetLoaderInfo("FabricApi", _selectedLoaderKind is MinecraftLoaderKind.Fabric or MinecraftLoaderKind.Quilt
            ? CreateAddonState(MinecraftInstallAddonKind.FabricApi, canAdd)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("LegacyFabricApi", _selectedLoaderKind == MinecraftLoaderKind.LegacyFabric
            ? CreateAddonState(MinecraftInstallAddonKind.LegacyFabricApi, canAdd)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("QSL", _selectedLoaderKind == MinecraftLoaderKind.Quilt
            ? CreateAddonState(MinecraftInstallAddonKind.Qsl, canAdd)
            : LoaderSupportState.Hidden());
        SetLoaderInfo("OptiFabric",
            _selectedLoaderKind is MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric && _selectedOptiFineAddon is not null
                ? CreateAddonState(MinecraftInstallAddonKind.OptiFabric, canAdd)
                : LoaderSupportState.Hidden());

        if (_selectedLoaderKind == MinecraftLoaderKind.Fabric &&
            this.FindControl<Control>("HintFabricAPI") is { } fabricHint)
        {
            fabricHint.IsVisible = true;
        }

        if (_selectedLoaderKind == MinecraftLoaderKind.Quilt &&
            this.FindControl<Control>("HintQSL") is { } qslHint)
        {
            qslHint.IsVisible = true;
        }

        if (_selectedOptiFineAddon is not null &&
            this.FindControl<Control>("HintOptiFabric") is { } optiFabricHint)
        {
            optiFabricHint.IsVisible = _selectedLoaderKind is MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric;
        }
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

    private LoaderSupportState CreateLoaderState(
        MinecraftLoaderKind kind,
        string canAdd,
        string? incompatibleLoader)
    {
        if (_selectedLoaderKind == kind && _selectedLoaderVersion is { } selectedLoader)
            return LoaderSupportState.Selected(selectedLoader.DisplayVersion);

        if (_selectedLoaderKind is not null && _selectedLoaderKind != MinecraftLoaderKind.OptiFine)
            return LoaderSupportState.VisibleClosed(incompatibleLoader ?? canAdd);

        if (_selectedVersion is null)
            return LoaderSupportState.VisibleClosed(canAdd);

        (MinecraftLoaderKind Kind, string GameVersion) key = (kind, _selectedVersion.Id);
        if (_loaderVersionCache.ContainsKey(key))
            return LoaderSupportState.VisibleOpen(canAdd);

        return LoaderSupportState.VisibleClosed(
            _loaderVersionErrors.TryGetValue(key, out string? error)
                ? "版本列表加载失败：" + error
                : "正在获取版本列表");
    }

    private LoaderSupportState CreateOptiFineState(string canAdd)
    {
        if (_selectedOptiFineAddon is { } addon)
            return LoaderSupportState.Selected(addon.DisplayVersion);
        if (_selectedLoaderKind == MinecraftLoaderKind.OptiFine && _selectedLoaderVersion is { } selected)
            return LoaderSupportState.Selected(selected.DisplayVersion);
        if (_selectedVersion is null)
            return LoaderSupportState.VisibleClosed(canAdd);

        (MinecraftLoaderKind Kind, string GameVersion) key = (MinecraftLoaderKind.OptiFine, _selectedVersion.Id);
        if (_loaderVersionCache.ContainsKey(key))
            return LoaderSupportState.VisibleOpen(canAdd);
        return LoaderSupportState.VisibleClosed(
            _loaderVersionErrors.TryGetValue(key, out string? error)
                ? "版本列表加载失败：" + error
                : "正在获取版本列表");
    }

    private void BeginLoaderVersionPreload()
    {
        if (_selectedVersion is null)
            return;

        foreach (DownloadLoaderDescriptor loader in DownloadLoaderRegistry.All)
        {
            string name = loader.CardName;
            if (_loaderStates.TryGetValue(name, out LoaderSupportState? state) && state.IsVisible)
                _ = PreloadLoaderVersionsAsync(name, _selectedVersion.Id);
        }

        foreach (DownloadAddonDescriptor addon in DownloadAddonRegistry.All)
        {
            string name = addon.CardName;
            if (_loaderStates.TryGetValue(name, out LoaderSupportState? state) && state.IsVisible)
                _ = PreloadAddonVersionsAsync(name, _selectedVersion.Id);
        }
    }

    private LoaderSupportState CreateAddonState(MinecraftInstallAddonKind kind, string canAdd)
    {
        if (_selectedAddons.TryGetValue(kind, out MinecraftInstallAddonVersionEntry? selected))
            return LoaderSupportState.Selected(selected.Version);
        if (_selectedVersion is null)
            return LoaderSupportState.VisibleClosed(canAdd);

        (MinecraftInstallAddonKind Kind, string GameVersion) key = (kind, _selectedVersion.Id);
        if (_addonVersionCache.ContainsKey(key))
            return LoaderSupportState.VisibleOpen(canAdd);
        return LoaderSupportState.VisibleClosed(
            _addonVersionErrors.TryGetValue(key, out string? error)
                ? "版本列表加载失败：" + error
                : "正在获取版本列表");
    }

    private async Task PreloadLoaderVersionsAsync(string name, string expectedGameVersion)
    {
        await EnsureLoaderVersionsRenderedAsync(name).ConfigureAwait(true);
        if (string.Equals(_selectedVersion?.Id, expectedGameVersion, StringComparison.Ordinal))
            ReloadSelectedLoaderCards();
    }

    private async Task PreloadAddonVersionsAsync(string name, string expectedGameVersion)
    {
        await EnsureAddonVersionsRenderedAsync(name).ConfigureAwait(true);
        if (string.Equals(_selectedVersion?.Id, expectedGameVersion, StringComparison.Ordinal))
            ReloadSelectedLoaderCards();
    }

    private void SetLoaderInfo(string name, LoaderSupportState state)
    {
        _loaderStates[name] = state;

        if (this.FindControl<MyCard>("Card" + name) is { } card)
        {
            card.IsVisible = state.IsVisible;
            if (!state.CanOpen)
                card.IsSwapped = true;
            card.MainSwap.IsVisible = state.CanOpen;
        }

        RefreshLoaderInfoPanel(name);

        if (this.FindControl<TextBlock>("Lab" + name) is { } label)
        {
            label.Text = state.Text;
            label.Foreground = LegacyResourceResolver.Brush(label, "ColorBrushGray4", "#8c8c8c");
        }

        if (this.FindControl<Image>("Img" + name) is { } image)
            image.IsVisible = state.IconVisible;

        if (this.FindControl<Control>("Btn" + name + "Clear") is { } clearButton)
            clearButton.IsVisible = state.ClearVisible;
    }

    private void RefreshLoaderInfoPanel(string name)
    {
        if (this.FindControl<Control>("Pan" + name + "Info") is not { } info)
            return;

        bool isCollapsed = this.FindControl<MyCard>("Card" + name)?.IsSwapped ?? true;
        info.IsVisible = isCollapsed;
        info.Opacity = isCollapsed ? 1d : 0d;
    }

    private async Task<bool> EnsureLoaderVersionsRenderedAsync(string name)
    {
        if (_selectedVersion is null || !DownloadLoaderRegistry.TryGetByCardName(name, out DownloadLoaderDescriptor loader))
            return false;

        string gameVersion = _selectedVersion.Id;
        MinecraftLoaderKind kind = loader.Kind;
        (MinecraftLoaderKind Kind, string GameVersion) key = (kind, gameVersion);
        if (_loaderVersionCache.TryGetValue(key, out IReadOnlyList<MinecraftLoaderVersionEntry>? cached))
        {
            PopulateLoaderVersionList(name, kind, cached);
            return true;
        }

        SetLoaderVersionPanelMessage(name, "正在获取版本列表", "请稍候。");
        try
        {
            if (!_loaderVersionLoads.TryGetValue(key, out Task<IReadOnlyList<MinecraftLoaderVersionEntry>>? load))
            {
                load = _loaderMetadataService.GetLoaderVersionsAsync(kind, gameVersion);
                _loaderVersionLoads[key] = load;
            }

            IReadOnlyList<MinecraftLoaderVersionEntry> versions = await load.ConfigureAwait(true);
            _loaderVersionCache[key] = versions;
            _loaderVersionErrors.Remove(key);
            PopulateLoaderVersionList(name, kind, versions);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or FormatException or InvalidOperationException)
        {
            _loaderVersionErrors[key] = ex.Message;
            SetLoaderVersionPanelMessage(name, "获取版本列表失败", ex.Message);
            return false;
        }
        finally
        {
            _loaderVersionLoads.Remove(key);
        }
    }

    private async Task<bool> EnsureAddonVersionsRenderedAsync(string name)
    {
        if (_selectedVersion is null || !DownloadAddonRegistry.TryGetByCardName(name, out DownloadAddonDescriptor addon))
            return false;

        string gameVersion = _selectedVersion.Id;
        (MinecraftInstallAddonKind Kind, string GameVersion) key = (addon.Kind, gameVersion);
        if (_addonVersionCache.TryGetValue(key, out IReadOnlyList<MinecraftInstallAddonVersionEntry>? cached))
        {
            PopulateAddonVersionList(name, addon, cached);
            return true;
        }

        SetLoaderVersionPanelMessage(name, "正在获取版本列表", "请稍候。");
        try
        {
            if (!_addonVersionLoads.TryGetValue(key, out Task<IReadOnlyList<MinecraftInstallAddonVersionEntry>>? load))
            {
                load = _addonMetadataService.GetVersionsAsync(addon.Kind, gameVersion);
                _addonVersionLoads[key] = load;
            }

            IReadOnlyList<MinecraftInstallAddonVersionEntry> versions = await load.ConfigureAwait(true);
            _addonVersionCache[key] = versions;
            _addonVersionErrors.Remove(key);
            PopulateAddonVersionList(name, addon, versions);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or FormatException or InvalidOperationException)
        {
            _addonVersionErrors[key] = ex.Message;
            SetLoaderVersionPanelMessage(name, "获取版本列表失败", ex.Message);
            return false;
        }
        finally
        {
            _addonVersionLoads.Remove(key);
        }
    }

    private void PopulateAddonVersionList(
        string name,
        DownloadAddonDescriptor addon,
        IReadOnlyList<MinecraftInstallAddonVersionEntry> versions)
    {
        if (this.FindControl<StackPanel>("Pan" + name) is not { } panel)
            return;

        panel.Children.Clear();
        if (versions.Count == 0)
        {
            SetLoaderVersionPanelMessage(name, "没有可用版本", "当前 Minecraft 版本暂时没有兼容的附加组件版本。");
            return;
        }

        foreach (MinecraftInstallAddonVersionEntry version in versions)
        {
            MyListItem item = new()
            {
                Title = version.Version,
                Info = version.Stable ? "稳定版" : "测试版",
                Type = MyListItem.CheckType.Clickable,
                Logo = addon.Logo,
                LogoScale = 0.82d,
                Height = 42d,
                Margin = new Thickness(0, 0, 0, 2),
                Tag = version
            };
            item.Click += (_, _) => SelectAddonVersion(addon, version);
            panel.Children.Add(item);
        }
        ControlVisualHelpers.AnimateListEntrance(panel, "Download Addon List " + name);
    }

    private void SelectAddonVersion(DownloadAddonDescriptor addon, MinecraftInstallAddonVersionEntry version)
    {
        _selectedAddons[addon.Kind] = version;
        if (this.FindControl<MyCard>("Card" + addon.CardName) is { } card)
            card.IsSwapped = true;
        ReloadSelectedLoaderCards();
    }

    private void PopulateLoaderVersionList(
        string name,
        MinecraftLoaderKind kind,
        IReadOnlyList<MinecraftLoaderVersionEntry> versions)
    {
        if (this.FindControl<StackPanel>("Pan" + name) is not { } panel)
            return;

        panel.Children.Clear();
        if (versions.Count == 0)
        {
            SetLoaderVersionPanelMessage(name, "没有可用版本", "当前 Minecraft 版本暂时没有可安装的加载器版本。");
            return;
        }

        foreach (MinecraftLoaderVersionEntry version in versions)
            panel.Children.Add(CreateLoaderVersionItem(kind, version));
        ControlVisualHelpers.AnimateListEntrance(panel, "Download Loader List " + name);
    }

    private MyListItem CreateLoaderVersionItem(MinecraftLoaderKind kind, MinecraftLoaderVersionEntry version)
    {
        MyListItem item = new()
        {
            Title = version.DisplayVersion,
            Info = version.Stable ? "稳定版" : "测试版",
            Type = MyListItem.CheckType.Clickable,
            Logo = GetLoaderLogo(kind),
            LogoScale = 0.82d,
            Height = 42d,
            Margin = new Thickness(0, 0, 0, 2),
            Tag = version
        };
        item.Click += (_, _) => SelectLoaderVersion(kind, version);
        return item;
    }

    private void SetLoaderVersionPanelMessage(string name, string title, string info)
    {
        if (this.FindControl<StackPanel>("Pan" + name) is not { } panel)
            return;

        panel.Children.Clear();
        panel.Children.Add(new MyListItem
        {
            Title = title,
            Info = info,
            Type = MyListItem.CheckType.None,
            Logo = TryGetLoaderLogo(name),
            LogoScale = 0.82d,
            Height = 42d
        });
    }

    private void SelectLoaderVersion(MinecraftLoaderKind kind, MinecraftLoaderVersionEntry version)
    {
        if (_selectedVersion is null)
            return;

        bool installsAsAddon = kind == MinecraftLoaderKind.OptiFine &&
                               _selectedLoaderKind is not null and not MinecraftLoaderKind.OptiFine;
        if (installsAsAddon)
        {
            _selectedOptiFineAddon = version;
        }
        else
        {
            if (_selectedLoaderKind == MinecraftLoaderKind.OptiFine && kind != MinecraftLoaderKind.OptiFine &&
                _selectedLoaderVersion is { } selectedOptiFine)
            {
                _selectedOptiFineAddon = selectedOptiFine;
            }

            _selectedLoaderKind = kind;
            _selectedLoaderVersion = version;
            if (!_preserveInstallNameOnLoaderSelect || string.IsNullOrWhiteSpace(this.FindControl<MyTextBox>("TextSelectName")?.Text))
                SetSelectName(MinecraftLoaderVersionJsonBuilder.CreateDefaultVersionId(kind, _selectedVersion.Id, version.Version));
        }
        if (this.FindControl<MyCard>("Card" + GetLoaderCardName(kind)) is { } card)
            card.IsSwapped = true;
        HideAllHints();
        ReloadSelectedLoaderCards();
        BeginLoaderVersionPreload();
    }

    private void ClearSelectedLoader(string name)
    {
        if (DownloadAddonRegistry.TryGetByCardName(name, out DownloadAddonDescriptor addon))
        {
            _selectedAddons.Remove(addon.Kind);
            ReloadSelectedLoaderCards();
            return;
        }

        if (!DownloadLoaderRegistry.TryGetByCardName(name, out DownloadLoaderDescriptor loader) ||
            _selectedLoaderKind != loader.Kind)
        {
            if (loader.Kind == MinecraftLoaderKind.OptiFine && _selectedOptiFineAddon is not null)
            {
                _selectedOptiFineAddon = null;
                _selectedAddons.Remove(MinecraftInstallAddonKind.OptiFabric);
                ReloadSelectedLoaderCards();
            }
            return;
        }

        ResetSelectedLoader();
        if (_selectedVersion is not null)
            SetSelectName(_preserveInstallNameOnLoaderSelect && !string.IsNullOrWhiteSpace(_preferredInstallName)
                ? _preferredInstallName
                : _selectedVersion.Id);
        HideAllHints();
        ReloadSelectedLoaderCards();
    }

    private void ResetSelectedLoader()
    {
        _selectedLoaderKind = null;
        _selectedLoaderVersion = null;
        _selectedOptiFineAddon = null;
        _selectedAddons.Clear();
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

        MinecraftLoaderInstallRequest? loader = CreateSelectedLoaderRequest();
        if (loader is not null && string.Equals(installName, _selectedVersion.Id, StringComparison.OrdinalIgnoreCase))
        {
            installName = MinecraftLoaderVersionJsonBuilder.CreateDefaultVersionId(
                loader.Kind,
                _selectedVersion.Id,
                loader.LoaderVersion);
            SetSelectName(installName);
        }

        DownloadInstallRequest request = new(
            installName,
            _selectedVersion.Id,
            _selectedVersion.Url,
            loader,
            _targetMinecraftRootDirectory,
            _replaceExistingVersion,
            CreateSelectedAddonRequests());
        InstallRequested?.Invoke(this, request);
    }

    private MinecraftLoaderInstallRequest? CreateSelectedLoaderRequest()
    {
        return _selectedLoaderKind is { } kind && _selectedLoaderVersion is { } version
            ? new MinecraftLoaderInstallRequest(kind, version.Version)
            : null;
    }

    private MinecraftInstallAddonRequest[] CreateSelectedAddonRequests()
    {
        List<MinecraftInstallAddonRequest> result = _selectedAddons.Values
            .Select(version => new MinecraftInstallAddonRequest(
                version.Kind,
                version.Version,
                version.FileName,
                version.Url,
                version.Sha1,
                version.Size))
            .ToList();
        if (_selectedVersion is not null && _selectedOptiFineAddon is { } optiFine)
        {
            MinecraftLoaderInstallerArtifact artifact = MinecraftLoaderInstallerArtifactResolver.Resolve(
                MinecraftLoaderKind.OptiFine,
                _selectedVersion.Id,
                optiFine.Version);
            result.Add(new MinecraftInstallAddonRequest(
                MinecraftInstallAddonKind.OptiFine,
                optiFine.Version,
                artifact.FileName,
                artifact.Sources[0],
                null,
                -1));
        }

        return result.ToArray();
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

    private string CanAddText() =>
        ResourceText("Download.Install.State.CanAdd", "可以添加");

    private static string GetLoaderCardName(MinecraftLoaderKind kind) =>
        DownloadLoaderRegistry.Get(kind).CardName;

    private static string GetLoaderDisplayName(MinecraftLoaderKind kind) =>
        DownloadLoaderRegistry.Get(kind).DisplayName;

    private static string GetLoaderLogo(MinecraftLoaderKind kind) =>
        DownloadLoaderRegistry.Get(kind).Logo;

    private static string TryGetLoaderLogo(string name) =>
        DownloadLoaderRegistry.TryGetByCardName(name, out DownloadLoaderDescriptor loader) ? loader.Logo : string.Empty;

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

    private sealed record LoaderSupportState(
        bool IsVisible,
        bool CanOpen,
        string Text,
        bool IconVisible,
        bool ClearVisible)
    {
        public static LoaderSupportState Hidden() => new(false, false, string.Empty, false, false);

        public static LoaderSupportState VisibleClosed(string text) => new(true, false, text, false, false);

        public static LoaderSupportState VisibleOpen(string text) => new(true, true, text, false, false);

        public static LoaderSupportState Selected(string text) => new(true, true, text, true, true);
    }
}

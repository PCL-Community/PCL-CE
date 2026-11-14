using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Text;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>安装页的加载器种类。</summary>
internal enum InstallLoaderKind
{
    Forge,
    Cleanroom,
    NeoForge,
    Fabric,
    LegacyFabric,
    FabricApi,
    LegacyFabricApi,
    LabyMod,
    OptiFine,
    OptiFabric,
    LiteLoader
}

/// <summary>安装页当前选择的加载器与附属组件。</summary>
internal sealed class InstallLoaderSelection
{
    public GameDownload.DlCleanroomListEntry? Cleanroom;
    public Comp.CompFile? FabricApi;
    public string? FabricVersion;
    public GameDownload.DlForgeVersionEntry? Forge;
    public string? LabyModChannel;
    public string? LabyModCommitRef;
    public string? LabyModVersion;
    public Comp.CompFile? LegacyFabricApi;
    public string? LegacyFabricVersion;
    public GameDownload.DlLiteLoaderListEntry? LiteLoader;
    public GameDownload.DlNeoForgeListEntry? NeoForge;
    public Comp.CompFile? OptiFabric;
    public GameDownload.DlOptiFineListEntry? OptiFine;

    public bool HasAny =>
        Forge is not null
        || Cleanroom is not null
        || NeoForge is not null
        || FabricVersion is not null
        || LegacyFabricVersion is not null
        || FabricApi is not null
        || LegacyFabricApi is not null
        || OptiFabric is not null
        || OptiFine is not null
        || LiteLoader is not null
        || LabyModCommitRef is not null;

    public bool HasMainLoader =>
        Forge is not null
        || Cleanroom is not null
        || NeoForge is not null
        || FabricVersion is not null
        || LegacyFabricVersion is not null
        || LabyModCommitRef is not null;

    public void Clear()
    {
        Forge = null;
        Cleanroom = null;
        NeoForge = null;
        FabricVersion = null;
        LegacyFabricVersion = null;
        FabricApi = null;
        LegacyFabricApi = null;
        OptiFabric = null;
        OptiFine = null;
        LiteLoader = null;
        LabyModChannel = null;
        LabyModCommitRef = null;
        LabyModVersion = null;
    }

    public void ApplyTo(McInstallRequest request)
    {
        request.forgeEntry = Forge;
        request.cleanroomEntry = Cleanroom;
        request.neoForgeEntry = NeoForge;
        request.fabricVersion = FabricVersion;
        request.legacyFabricVersion = LegacyFabricVersion;
        request.fabricApi = FabricApi;
        request.legacyFabricApi = LegacyFabricApi;
        request.optiFabric = OptiFabric;
        request.optiFineEntry = OptiFine;
        request.liteLoaderEntry = LiteLoader;
        request.labyModChannel = LabyModChannel;
        request.labyModCommitRef = LabyModCommitRef;
        request.forgeVersion = Forge?.VersionName;
        request.neoForgeVersion = NeoForge?.VersionName;
        request.cleanroomVersion = Cleanroom?.VersionName;
    }

    public string GetSuffix()
    {
        var name = "";
        if (FabricVersion is not null)
            name += $"-Fabric_{FabricVersion.Replace("+build", "")}";
        if (LegacyFabricVersion is not null)
            name += $"-LegacyFabric_{LegacyFabricVersion}";
        if (LabyModVersion is not null)
            name += $"-LabyMod_{LabyModVersion.BeforeFirst(" ").Replace(" ", "_")}"
                + (LabyModChannel == "snapshot" ? "_Snapshot" : "_Production");
        if (Forge is not null)
            name += $"-Forge_{Forge.VersionName}";
        if (NeoForge is not null)
            name += $"-NeoForge_{NeoForge.VersionName}";
        if (Cleanroom is not null)
            name += $"-Cleanroom_{Cleanroom.VersionName}";
        if (LiteLoader is not null)
            name += "-LiteLoader";
        if (OptiFine is not null)
            name += $"-OptiFine_{DisplayOptiFineVersion(OptiFine).Replace(" ", "_")}";
        return name;
    }

    public string GetLoaderInfo()
    {
        var parts = new List<string>();
        if (Forge is not null)
            parts.Add($"Forge {Forge.VersionName}");
        if (NeoForge is not null)
            parts.Add($"NeoForge {NeoForge.VersionName}");
        if (Cleanroom is not null)
            parts.Add($"Cleanroom {Cleanroom.VersionName}");
        if (FabricVersion is not null)
            parts.Add($"Fabric {FabricVersion.Replace("+build", "")}");
        if (LegacyFabricVersion is not null)
            parts.Add($"Legacy Fabric {LegacyFabricVersion}");
        if (LiteLoader is not null)
            parts.Add("LiteLoader");
        if (OptiFine is not null)
            parts.Add($"OptiFine {DisplayOptiFineVersion(OptiFine)}");
        if (LabyModVersion is not null)
            parts.Add($"LabyMod {LabyModVersion.BeforeFirst(" ")}");
        return parts.Count == 0
            ? Lang.Text("Instance.Install.NoExtraInstall")
            : string.Join(", ", parts);
    }

    public string GetInfo(string minecraftName)
    {
        var loaderInfo = GetLoaderInfo();
        return string.Equals(
            loaderInfo,
            Lang.Text("Instance.Install.NoExtraInstall"),
            StringComparison.Ordinal)
            ? minecraftName
            : $"{minecraftName}  |  {loaderInfo}";
    }

    public static string DisplayOptiFineVersion(GameDownload.DlOptiFineListEntry entry)
    {
        return (entry.DisplayName ?? entry.NameVersion ?? "")
            .Replace(entry.Inherit + " ", "")
            .Replace("HD U ", "");
    }
}

/// <summary>
///     可复用的加载器选择器：为下载安装页与实例修改页生成 11 张加载器卡，
///     负责版本过滤、兼容性提示、错误/重试、清空与 Fabric API / OptiFabric 的自动选择。
/// </summary>
internal sealed class InstallLoaderSelector : IDisposable
{
    private static readonly (InstallLoaderKind Kind, string TitleKey)[] Definitions =
    [
        (InstallLoaderKind.Forge, "Common.Installation.Forge"),
        (InstallLoaderKind.Cleanroom, "Common.Installation.Cleanroom"),
        (InstallLoaderKind.NeoForge, "Common.Installation.NeoForge"),
        (InstallLoaderKind.Fabric, "Common.Installation.Fabric"),
        (InstallLoaderKind.LegacyFabric, "Common.Installation.LegacyFabric"),
        (InstallLoaderKind.FabricApi, "Common.Installation.FabricApi"),
        (InstallLoaderKind.LegacyFabricApi, "Common.Installation.LegacyFabricApi"),
        (InstallLoaderKind.LabyMod, "Common.Installation.LabyMod"),
        (InstallLoaderKind.OptiFine, "Common.Installation.OptiFine"),
        (InstallLoaderKind.OptiFabric, "Common.Installation.OptiFabric"),
        (InstallLoaderKind.LiteLoader, "Common.Installation.LiteLoader")
    ];

    private readonly List<CardState> _cards = [];
    private readonly Action _changed;
    private readonly StackPanel _host;

    private readonly List<(Loading.LoaderBase Loader,
            Loading.LoaderBase.OnStateChangedUiEventHandler Handler)>
        _listSubscriptions = [];

    private bool _active;
    private bool _autoFabricApi;
    private bool _autoLegacyFabricApi;
    private bool _autoOptiFabric;
    private bool _built;
    private string? _existingLabyModVersion;
    private Loading.LoaderBase? _forgeLoader;
    private Loading.LoaderBase.OnStateChangedUiEventHandler? _forgeLoaderHandler;
    private string? _forgeLoaderVersion;
    private bool _isInstalling;
    private bool _updating;

    public InstallLoaderSelector(
        StackPanel host,
        Action changed,
        InstallLoaderSelection? selection = null)
    {
        _host = host;
        _changed = changed;
        Selection = selection ?? new InstallLoaderSelection();
        _active = true;
        SubscribeStaticLoaders();
    }

    public string? MinecraftName { get; private set; }

    public string? MinecraftJsonUrl { get; private set; }

    public InstallLoaderSelection Selection { get; }

    internal IReadOnlyList<CardState> Cards => _cards;

    public void Dispose()
    {
        Deactivate();
        _cards.Clear();
        _host.Children.Clear();
    }

    public void Activate()
    {
        if (_active)
            return;
        _active = true;
        SubscribeStaticLoaders();
        if (_forgeLoader is not null)
            SubscribeForgeLoader(_forgeLoader);
    }

    public void Deactivate()
    {
        if (!_active)
            return;
        _active = false;
        UnsubscribeStaticLoaders();
        UnsubscribeForgeLoader();
    }

    public void SetMinecraft(
        string? minecraftName,
        string? minecraftJsonUrl,
        bool resetSelection = true)
    {
        var versionChanged = !string.Equals(
            MinecraftName,
            minecraftName,
            StringComparison.Ordinal);
        MinecraftName = minecraftName;
        MinecraftJsonUrl = minecraftJsonUrl;
        if (versionChanged || resetSelection)
        {
            Selection.Clear();
            _autoFabricApi = false;
            _autoLegacyFabricApi = false;
            _autoOptiFabric = false;
            _existingLabyModVersion = null;
        }

        EnsureBuilt();
        StartListLoaders(true);
        RefreshAll();
        NotifyChanged();
    }

    public void SetInstalling(bool installing)
    {
        _isInstalling = installing;
        foreach (var card in _cards)
            card.Card.IsEnabled = !installing;
    }

    public void RefreshAll()
    {
        EnsureBuilt();
        foreach (var card in _cards)
            RefreshCard(card);
        if (TryAutoSelectApis(out var autoSelected))
            foreach (var kind in autoSelected)
                RefreshKind(kind);
        NotifyChanged();
    }

    public void RefreshLocalization()
    {
        if (!_built)
            return;
        foreach (var card in _cards)
        {
            card.Host.Title = Lang.Text(card.TitleKey);
            RefreshCard(card);
        }

        NotifyChanged();
    }

    public void ClearAll()
    {
        Selection.Clear();
        _existingLabyModVersion = null;
        _autoFabricApi = true;
        _autoLegacyFabricApi = true;
        _autoOptiFabric = true;
        foreach (var card in _cards)
            RefreshCard(card);
        NotifyChanged();
    }

    public void ApplyExisting(McInstance instance)
    {
        var info = instance.Info;
        Selection.Clear();
        if (info.HasForge)
            Selection.Forge = new GameDownload.DlForgeVersionEntry(
                info.Forge ?? "",
                null,
                info.VanillaName)
            {
                Category = "installer",
                Inherit = info.VanillaName
            };
        if (info.HasNeoForge)
        {
            var version = info.NeoForge ?? "";
            Selection.NeoForge = new GameDownload.DlNeoForgeListEntry(
                info.VanillaName == "1.20.1" && !version.StartsWith("1.20.1-")
                    ? $"1.20.1-{version}"
                    : version);
        }

        if (info.HasCleanroom)
            Selection.Cleanroom = new GameDownload.DlCleanroomListEntry(info.Cleanroom ?? "");
        if (info.HasFabric)
            Selection.FabricVersion = info.Fabric;
        if (info.HasLegacyFabric)
            Selection.LegacyFabricVersion = info.LegacyFabric;
        if (info.HasOptiFine)
        {
            var optiFineVersion = info.OptiFine ?? "";
            if (!optiFineVersion.StartsWith("HD_U", StringComparison.OrdinalIgnoreCase))
                optiFineVersion = $"HD_U_{optiFineVersion}";
            var isPreview = optiFineVersion.Contains(
                "pre",
                StringComparison.OrdinalIgnoreCase);
            Selection.OptiFine = new GameDownload.DlOptiFineListEntry
            {
                DisplayName =
                    $"{info.VanillaName} {optiFineVersion.Replace("HD_U_", "").Replace("_", " ")}",
                IsPreview = isPreview,
                Inherit = info.VanillaName,
                NameVersion = $"{info.VanillaName}-OptiFine_{optiFineVersion}",
                NameFile =
                    $"{(isPreview ? "preview_" : "")}OptiFine_{info.VanillaName}_{optiFineVersion}.jar"
            };
        }

        if (info.HasLiteLoader)
            Selection.LiteLoader = new GameDownload.DlLiteLoaderListEntry
            {
                Inherit = info.VanillaName
            };
        if (info.HasLabyMod)
        {
            Selection.LabyModChannel = "production";
            Selection.LabyModVersion = info.LabyMod;
            _existingLabyModVersion = info.LabyMod;
        }
        else
        {
            _existingLabyModVersion = null;
        }

        _autoFabricApi = true;
        _autoLegacyFabricApi = true;
        _autoOptiFabric = true;
        RefreshAll();
    }

    public IReadOnlyList<string> GetWarnings()
    {
        var warnings = new List<string>();
        var flags = GetWarningFlags();
        if (flags.FabricApi)
            warnings.Add(Lang.Text("Download.Install.Warning.FabricApi"));
        if (flags.LegacyFabricApi)
            warnings.Add(Lang.Text("Download.Install.Warning.LegacyFabricApi"));
        if (flags.OptiFabric)
            warnings.Add(Lang.Text("Download.Install.Warning.OptiFabric"));
        if (flags.OptiFabricOld)
            warnings.Add(Lang.Text("Download.Install.Warning.OptiFabricOld"));
        if (flags.LegacyOptiFabric)
            warnings.Add(Lang.Text("Download.Install.Warning.LegacyOptiFabric"));
        if (flags.ModOptiFine)
            warnings.Add(Lang.Text("Download.Install.Warning.ModOptiFine"));
        return warnings;
    }

    internal InstallLoaderWarningState GetWarningFlags()
    {
        var flags = new InstallLoaderWarningState
        {
            FabricApi = Selection.FabricVersion is not null && Selection.FabricApi is null,
            LegacyFabricApi = Selection.LegacyFabricVersion is not null
                && Selection.LegacyFabricApi is null
        };

        if ((Selection.FabricVersion is not null
                || Selection.LegacyFabricVersion is not null)
            && Selection.OptiFine is not null
            && Selection.OptiFabric is null)
        {
            var drop = McInstanceInfo.VersionToDrop(MinecraftName, true);
            if (drop is >= 140 and <= 150)
                flags.OptiFabricOld = true;
            else if (Selection.LegacyFabricVersion is not null)
                flags.LegacyOptiFabric = true;
            else
                flags.OptiFabric = true;
        }

        flags.ModOptiFine = McInstanceInfo.VersionToDrop(MinecraftName, true) >= 160
            && Selection.OptiFine is not null
            && (Selection.Forge is not null || Selection.FabricVersion is not null);
        return flags;
    }

    private void EnsureBuilt()
    {
        if (_built)
            return;
        _built = true;
        var existing = _host.Children.OfType<InstallLoaderCard>().ToList();
        var index = 0;
        foreach (var (kind, titleKey) in Definitions)
        {
            var host = index < existing.Count
                ? existing[index]
                : new InstallLoaderCard();
            if (index >= existing.Count)
                _host.Children.Add(host);
            index++;
            host.Title = Lang.Text(titleKey);
            host.SetLogo(
                GetLoaderLogo(kind),
                kind == InstallLoaderKind.LiteLoader ? 20d : 18d,
                kind == InstallLoaderKind.LiteLoader
                    ? new Thickness(-1d, 0d, 6d, 0d)
                    : new Thickness(0d, 0d, 7d, 0d));
            host.SetBodyMargin(
                kind is InstallLoaderKind.Fabric
                    or InstallLoaderKind.LegacyFabric
                    or InstallLoaderKind.LabyMod
                    ? new Thickness(20d, 40d, 18d, 0d)
                    : new Thickness(20d, 40d, 18d, 15d));

            var state = new CardState
            {
                Kind = kind,
                TitleKey = titleKey,
                Host = host
            };
            host.ClearButton.Click += (_, _) =>
            {
                if (_isInstalling)
                    return;
                ClearKind(kind);
                host.Card.IsSwapped = true;
                RefreshAll();
            };
            host.Card.PreviewSwap += (_, args) =>
            {
                var error = GetCardError(state);
                if (error is null)
                    return;
                // 列表加载失败时允许展开，否则重试按钮永远无法点到。
                if (GetListLoader(state.Kind)?.State != LoadState.Failed)
                    args.handled = true;
            };
            host.Card.Swap += (_, _) =>
            {
                if (!host.Card.IsSwapped)
                    BuildBody(state);
            };
            host.Card.InstallMethod = _ => BuildBody(state);
            _cards.Add(state);
        }
    }

    private static string? GetLoaderLogo(InstallLoaderKind kind)
    {
        return kind switch
        {
            InstallLoaderKind.Forge => $"{LauncherPaths.ImageRoot}Blocks/Anvil.png",
            InstallLoaderKind.Cleanroom => $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png",
            InstallLoaderKind.NeoForge => $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png",
            InstallLoaderKind.Fabric or InstallLoaderKind.FabricApi =>
                $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
            InstallLoaderKind.LegacyFabric or InstallLoaderKind.LegacyFabricApi =>
                $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
            InstallLoaderKind.LabyMod => $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png",
            InstallLoaderKind.OptiFine => $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png",
            InstallLoaderKind.OptiFabric => $"{LauncherPaths.ImageRoot}Blocks/OptiFabric.png",
            InstallLoaderKind.LiteLoader => $"{LauncherPaths.ImageRoot}Blocks/Egg.png",
            _ => null
        };
    }

    private void RefreshCard(CardState card)
    {
        var applicable = IsApplicable(card.Kind);
        card.Host.IsVisible = applicable;
        card.Card.IsVisible = applicable;
        if (!applicable)
        {
            card.Card.IsSwapped = true;
            return;
        }

        var selected = GetSelectedText(card.Kind);
        var error = GetCardError(card);
        if (error is not null
            && GetListLoader(card.Kind)?.State != LoadState.Failed
            && !card.Card.IsSwapped)
            // 兼容性错误出现时强制折叠；列表加载失败时保留展开状态以显示重试按钮。
            card.Card.IsSwapped = true;

        card.ClearButton.IsVisible = selected is not null;
        card.Host.Logo.IsVisible = selected is not null;
        card.Info.Text = selected
            ?? error
            ?? Lang.Text("Download.Install.State.CanAdd");
        card.Info.Opacity = selected is null ? 0.7d : 1d;
        if (!card.Card.IsSwapped)
            BuildBody(card);
    }

    private string? GetSelectedText(InstallLoaderKind kind)
    {
        return kind switch
        {
            InstallLoaderKind.Forge => Selection.Forge?.VersionName,
            InstallLoaderKind.Cleanroom => Selection.Cleanroom?.VersionName,
            InstallLoaderKind.NeoForge => Selection.NeoForge?.VersionName,
            InstallLoaderKind.Fabric => Selection.FabricVersion?.Replace("+build", ""),
            InstallLoaderKind.LegacyFabric => Selection.LegacyFabricVersion,
            InstallLoaderKind.FabricApi => Selection.FabricApi is { } file
                ? StripGameVersionPrefix(FormatFabricApiTitle(file))
                : null,
            InstallLoaderKind.LegacyFabricApi => Selection.LegacyFabricApi?.DisplayName is
                { } legacy
                ? legacy.Replace("Legacy Fabric API ", "")
                : null,
            InstallLoaderKind.LabyMod => Selection.LabyModVersion,
            InstallLoaderKind.OptiFine => Selection.OptiFine is { } optiFine
                ? InstallLoaderSelection.DisplayOptiFineVersion(optiFine)
                : null,
            InstallLoaderKind.OptiFabric => Selection.OptiFabric?.DisplayName is { } optiFabric
                ? optiFabric
                    .ToLower()
                    .Replace("optifabric-", "")
                    .Replace(".jar", "")
                    .Trim()
                    .TrimStart('v')
                : null,
            InstallLoaderKind.LiteLoader => Selection.LiteLoader?.Inherit,
            _ => null
        };
    }

    private bool IsApplicable(InstallLoaderKind kind)
    {
        if (MinecraftName is null)
            return false;
        var drop = McInstanceInfo.VersionToDrop(MinecraftName, true);
        var formatFit = McInstanceInfo.IsFormatFit(MinecraftName);
        return kind switch
        {
            InstallLoaderKind.OptiFine =>
                McVersionComparer.CompareVersionGe(MinecraftName, "1.7.2"),
            InstallLoaderKind.LiteLoader => formatFit
                && McVersionComparer.CompareVersionGe(MinecraftName, "1.5.2")
                && McVersionComparer.CompareVersionGe("1.12.2", MinecraftName),
            InstallLoaderKind.Forge => formatFit
                && McVersionComparer.CompareVersionGe(MinecraftName, "1.1"),
            InstallLoaderKind.Cleanroom => MinecraftName == "1.12.2",
            InstallLoaderKind.NeoForge => McVersionComparer.CompareVersionGe(
                MinecraftName,
                "1.20.1"),
            InstallLoaderKind.Fabric => drop >= 130
                && (drop > 130 || McVersionComparer.CompareVersionGe(MinecraftName, "18w43b")),
            InstallLoaderKind.LegacyFabric => drop is >= 30 and <= 130,
            InstallLoaderKind.FabricApi => Selection.FabricVersion is not null,
            InstallLoaderKind.LegacyFabricApi => Selection.LegacyFabricVersion is not null,
            InstallLoaderKind.LabyMod => formatFit
                && McVersionComparer.CompareVersionGe(MinecraftName, "1.8.9"),
            InstallLoaderKind.OptiFabric => Selection.FabricVersion is not null
                && Selection.OptiFine is not null,
            _ => true
        };
    }

    private string? GetCardError(CardState card)
    {
        if (MinecraftName is null)
            return null;

        var conflict = GetMainLoaderConflict(card.Kind);
        if (conflict is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", conflict);

        return card.Kind switch
        {
            InstallLoaderKind.Forge => GetForgeError(),
            InstallLoaderKind.Cleanroom => GetCleanroomError(),
            InstallLoaderKind.NeoForge => GetNeoForgeError(),
            InstallLoaderKind.Fabric => GetFabricError(),
            InstallLoaderKind.LegacyFabric => GetLegacyFabricError(),
            InstallLoaderKind.FabricApi => GetFabricApiError(),
            InstallLoaderKind.LegacyFabricApi => GetLegacyFabricApiError(),
            InstallLoaderKind.LabyMod => GetLabyModError(),
            InstallLoaderKind.OptiFine => GetOptiFineError(),
            InstallLoaderKind.OptiFabric => GetOptiFabricError(),
            InstallLoaderKind.LiteLoader => GetLiteLoaderError(),
            _ => null
        };
    }

    private string? GetMainLoaderConflict(InstallLoaderKind kind)
    {
        var self = GetMainLoader(kind);
        if (self is null)
            return null;
        if (kind is not (InstallLoaderKind.Forge
            or InstallLoaderKind.Cleanroom
            or InstallLoaderKind.NeoForge
            or InstallLoaderKind.Fabric
            or InstallLoaderKind.LegacyFabric
            or InstallLoaderKind.LabyMod))
            return null;

        var others = new (string Name, bool Selected)[]
        {
            ("Forge", Selection.Forge is not null),
            ("NeoForge", Selection.NeoForge is not null),
            ("Cleanroom", Selection.Cleanroom is not null),
            ("Fabric", Selection.FabricVersion is not null),
            ("Legacy Fabric", Selection.LegacyFabricVersion is not null),
            ("LabyMod", Selection.LabyModCommitRef is not null)
        };
        return others.FirstOrDefault(loader => loader.Selected && loader.Name != self).Name;
    }

    private static string? GetMainLoader(InstallLoaderKind kind)
    {
        return kind switch
        {
            InstallLoaderKind.Forge => "Forge",
            InstallLoaderKind.Cleanroom => "Cleanroom",
            InstallLoaderKind.NeoForge => "NeoForge",
            InstallLoaderKind.Fabric => "Fabric",
            InstallLoaderKind.LegacyFabric => "Legacy Fabric",
            InstallLoaderKind.LabyMod => "LabyMod",
            _ => null
        };
    }

    private string? GetForgeError()
    {
        var loaderError = GetListError(InstallLoaderKind.Forge);
        if (loaderError is not null)
            return loaderError;
        var versions = GetForgeVersions();
        if (versions.Count == 0)
            return Lang.Text("Download.Install.State.NoVersion");
        if (Selection.OptiFine is null)
            return null;
        return versions.Any(version => IsOptiFineSuitForForge(Selection.OptiFine, version))
            ? null
            : Lang.Text("Download.Install.Compat.CompatForgeSpecificOnly");
    }

    private string? GetCleanroomError()
    {
        if (MinecraftName != "1.12.2")
            return Lang.Text("Download.Install.State.NoVersion");
        if (Selection.OptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (Selection.LiteLoader is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
        var loaderError = GetListError(InstallLoaderKind.Cleanroom);
        if (loaderError is not null)
            return loaderError;
        return GetCleanroomVersions().Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetNeoForgeError()
    {
        if (Selection.OptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        var loaderError = GetListError(InstallLoaderKind.NeoForge);
        if (loaderError is not null)
            return loaderError;
        return GetNeoForgeVersions().Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetFabricError()
    {
        if (Selection.OptiFine is not null
            && McVersionComparer.CompareVersionGe(MinecraftName ?? "", "1.20.5"))
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        var loaderError = GetListError(InstallLoaderKind.Fabric);
        if (loaderError is not null)
            return loaderError;
        return GetFabricGameVersions()
            .Any(version =>
                string.Equals(
                    version,
                    NormalizeGameVersion(MinecraftName ?? ""),
                    StringComparison.OrdinalIgnoreCase))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetLegacyFabricError()
    {
        if (Selection.LiteLoader is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
        var loaderError = GetListError(InstallLoaderKind.LegacyFabric);
        if (loaderError is not null)
            return loaderError;
        return GetLegacyFabricGameVersions()
            .Any(version =>
                string.Equals(version, MinecraftName, StringComparison.OrdinalIgnoreCase))
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetFabricApiError()
    {
        if (Selection.FabricVersion is null)
            return Lang.Text("Download.Install.Compat.RequiresFabric");
        var loaderError = GetListError(InstallLoaderKind.FabricApi);
        if (loaderError is not null)
            return loaderError;
        return GetCompatibleCompFiles(CompDownloadLoaders.DlFabricApiLoader.output, false).Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetLegacyFabricApiError()
    {
        if (Selection.LegacyFabricVersion is null)
            return Lang.Text("Download.Install.Compat.RequiresLegacyFabric");
        var loaderError = GetListError(InstallLoaderKind.LegacyFabricApi);
        if (loaderError is not null)
            return loaderError;
        return GetCompatibleCompFiles(CompDownloadLoaders.DlLegacyFabricApiLoader.output, false)
                .Count
            > 0
                ? null
                : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetLabyModError()
    {
        if (Selection.OptiFine is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        if (Selection.LiteLoader is not null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithLiteLoader");
        var loaderError = GetListError(InstallLoaderKind.LabyMod);
        if (loaderError is not null)
            return loaderError;
        return GetLabyModChoices().Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetOptiFineError()
    {
        if (Selection.NeoForge is not null
            || Selection.Cleanroom is not null
            || Selection.LabyModCommitRef is not null)
            return Lang.Text(
                "Download.Install.Compat.IncompatibleWithLoader",
                Selection.NeoForge is not null
                    ? "NeoForge"
                    : Selection.Cleanroom is not null
                        ? "Cleanroom"
                        : "LabyMod");
        if (Selection.Forge is not null
            && McVersionComparer.CompareVersionGe(MinecraftName ?? "", "1.13")
            && McVersionComparer.CompareVersionGe("1.14.3", MinecraftName ?? ""))
            return Lang.Text("Download.Install.Compat.IncompatibleWithLoader", "Forge");
        if (Selection.FabricVersion is not null
            && McVersionComparer.CompareVersionGe(MinecraftName ?? "", "1.20.5")
            && Selection.OptiFabric is null)
            return Lang.Text("Download.Install.Compat.IncompatibleWithOptiFine");
        var loaderError = GetListError(InstallLoaderKind.OptiFine);
        if (loaderError is not null)
            return loaderError;
        var versions = GetOptiFineVersions();
        if (versions.Count == 0)
            return Lang.Text("Download.Install.State.NoVersion");
        if (Selection.Forge is null)
            return null;
        return versions.Any(version => IsOptiFineSuitForForge(version, Selection.Forge))
            ? null
            : Lang.Text("Download.Install.Compat.CompatForgeSpecificOnly");
    }

    private string? GetOptiFabricError()
    {
        var drop = McInstanceInfo.VersionToDrop(MinecraftName, true);
        if (drop is >= 140 and <= 150)
            return Lang.Text("Download.Install.Compat.OptiFabricOriginsRequired");
        if (Selection.FabricVersion is null && Selection.OptiFine is null)
            return Lang.Text("Download.Install.Compat.RequiresOptiFineAndFabric");
        if (Selection.FabricVersion is null)
            return Lang.Text("Download.Install.Compat.RequiresFabric");
        if (Selection.OptiFine is null)
            return Lang.Text("Download.Install.Compat.RequiresOptiFine");
        var loaderError = GetListError(InstallLoaderKind.OptiFabric);
        if (loaderError is not null)
            return loaderError;
        return GetCompatibleCompFiles(CompDownloadLoaders.DlOptiFabricLoader.output, true).Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetLiteLoaderError()
    {
        if (Selection.NeoForge is not null
            || Selection.LegacyFabricVersion is not null
            || Selection.LabyModCommitRef is not null
            || Selection.Cleanroom is not null)
            return Lang.Text(
                "Download.Install.Compat.IncompatibleWithLoader",
                Selection.NeoForge is not null
                    ? "NeoForge"
                    : Selection.LegacyFabricVersion is not null
                        ? "Legacy Fabric"
                        : Selection.LabyModCommitRef is not null
                            ? "LabyMod"
                            : "Cleanroom");
        var loaderError = GetListError(InstallLoaderKind.LiteLoader);
        if (loaderError is not null)
            return loaderError;
        return GetLiteLoaderVersions().Count > 0
            ? null
            : Lang.Text("Download.Install.State.NoVersion");
    }

    private string? GetListError(InstallLoaderKind kind)
    {
        var loader = GetListLoader(kind);
        if (loader is null)
            return Lang.Text("Download.Install.State.Getting");
        return loader.State switch
        {
            LoadState.Failed => Lang.Text(
                "Download.Install.State.GetVersionListFailed",
                loader.Error?.Message ?? Lang.Text("Download.Install.Error.OperationFailed")),
            LoadState.Waiting or LoadState.Loading => Lang.Text("Download.Install.State.Getting"),
            _ => null
        };
    }

    private Loading.LoaderBase? GetListLoader(InstallLoaderKind kind)
    {
        return kind switch
        {
            InstallLoaderKind.Forge => _forgeLoader,
            InstallLoaderKind.Cleanroom => GameDownload.DlCleanroomListLoader,
            InstallLoaderKind.NeoForge => GameDownload.DlNeoForgeListLoader,
            InstallLoaderKind.Fabric => GameDownload.DlFabricListLoader,
            InstallLoaderKind.LegacyFabric => GameDownload.DlLegacyFabricListLoader,
            InstallLoaderKind.FabricApi => CompDownloadLoaders.DlFabricApiLoader,
            InstallLoaderKind.LegacyFabricApi => CompDownloadLoaders.DlLegacyFabricApiLoader,
            InstallLoaderKind.LabyMod => GameDownload.DlLabyModListLoader,
            InstallLoaderKind.OptiFine => GameDownload.DlOptiFineListLoader,
            InstallLoaderKind.OptiFabric => CompDownloadLoaders.DlOptiFabricLoader,
            InstallLoaderKind.LiteLoader => GameDownload.DlLiteLoaderListLoader,
            _ => null
        };
    }

    private void StartListLoaders(bool forceRestart)
    {
        if (MinecraftName is null)
            return;
        StartForgeLoader(forceRestart);
        GameDownload.DlOptiFineListLoader.Start(null, forceRestart);
        GameDownload.DlLiteLoaderListLoader.Start(null, forceRestart);
        GameDownload.DlNeoForgeListLoader.Start(null, forceRestart);
        GameDownload.DlCleanroomListLoader.Start(null, forceRestart);
        GameDownload.DlFabricListLoader.Start(null, forceRestart);
        GameDownload.DlLegacyFabricListLoader.Start(null, forceRestart);
        GameDownload.DlLabyModListLoader.Start(null, forceRestart);
        CompDownloadLoaders.DlFabricApiLoader.Start(0, forceRestart);
        CompDownloadLoaders.DlLegacyFabricApiLoader.Start(0, forceRestart);
        CompDownloadLoaders.DlOptiFabricLoader.Start(0, forceRestart);
    }

    private void StartForgeLoader(bool forceRestart = false)
    {
        if (MinecraftName is null || !McInstanceInfo.IsFormatFit(MinecraftName))
        {
            UnsubscribeForgeLoader();
            _forgeLoader = null;
            return;
        }

        if (!forceRestart
            && _forgeLoader is not null
            && _forgeLoaderVersion == MinecraftName
            && _forgeLoader.State is not LoadState.Failed)
            return;

        UnsubscribeForgeLoader();
        var loader =
            new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
                $"DlForgeVersion {MinecraftName}",
                GameDownload.DlForgeVersionMain);
        _forgeLoader = loader;
        _forgeLoaderVersion = MinecraftName;
        if (_active)
            SubscribeForgeLoader(loader);
        loader.Start(MinecraftName, true);
    }

    private void SubscribeForgeLoader(Loading.LoaderBase loader)
    {
        if (_forgeLoaderHandler is not null)
            return;
        _forgeLoaderHandler = (_, state, _) =>
        {
            if (state is LoadState.Loading or LoadState.Finished or LoadState.Failed)
                Dispatcher.UIThread.Post(() => RefreshKind(InstallLoaderKind.Forge));
        };
        loader.OnStateChangedUi += _forgeLoaderHandler;
    }

    private void UnsubscribeForgeLoader()
    {
        if (_forgeLoader is not null && _forgeLoaderHandler is not null)
            _forgeLoader.OnStateChangedUi -= _forgeLoaderHandler;
        _forgeLoaderHandler = null;
    }

    private void SubscribeStaticLoaders()
    {
        if (_listSubscriptions.Count > 0)
            return;

        void Subscribe(Loading.LoaderBase loader)
        {
            void Handler(Loading.LoaderBase _, LoadState state, LoadState __)
            {
                if (state is LoadState.Loading or LoadState.Finished or LoadState.Failed)
                    Dispatcher.UIThread.Post(RefreshAll);
            }

            Loading.LoaderBase.OnStateChangedUiEventHandler handler = Handler;
            loader.OnStateChangedUi += handler;
            _listSubscriptions.Add((loader, handler));
        }

        Subscribe(GameDownload.DlOptiFineListLoader);
        Subscribe(GameDownload.DlLiteLoaderListLoader);
        Subscribe(GameDownload.DlNeoForgeListLoader);
        Subscribe(GameDownload.DlCleanroomListLoader);
        Subscribe(GameDownload.DlFabricListLoader);
        Subscribe(GameDownload.DlLegacyFabricListLoader);
        Subscribe(GameDownload.DlLabyModListLoader);
        Subscribe(CompDownloadLoaders.DlFabricApiLoader);
        Subscribe(CompDownloadLoaders.DlLegacyFabricApiLoader);
        Subscribe(CompDownloadLoaders.DlOptiFabricLoader);
    }

    private void UnsubscribeStaticLoaders()
    {
        foreach (var (loader, handler) in _listSubscriptions)
            loader.OnStateChangedUi -= handler;
        _listSubscriptions.Clear();
    }

    private void RefreshKind(InstallLoaderKind kind)
    {
        var card = _cards.FirstOrDefault(item => item.Kind == kind);
        if (card is null)
            return;
        RefreshCard(card);
    }

    private void BuildBody(CardState card)
    {
        var body = card.Body;
        body.Children.Clear();
        if (_isInstalling)
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.Loading"),
                    Opacity = 0.7d,
                    Margin = new Thickness(6d, 4d, 0d, 4d)
                });
            return;
        }

        var error = GetCardError(card);
        var loader = GetListLoader(card.Kind);
        if (loader?.State is LoadState.Waiting or LoadState.Loading)
        {
            body.Children.Add(
                new MyLoading
                {
                    Margin = new Thickness(5d),
                    Text = Lang.Text("Download.Install.State.Getting")
                });
            return;
        }

        if (error is not null)
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = error,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(6d, 4d, 6d, 8d)
                });
            if (loader?.State == LoadState.Failed)
            {
                var retry = new MyButton
                {
                    Text = Lang.Text("Common.Action.Refresh"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    MinWidth = 90d,
                    Padding = new Thickness(13d, 0d, 13d, 0d)
                };
                retry.Click += (_, _) =>
                {
                    if (card.Kind == InstallLoaderKind.Forge)
                    {
                        UnsubscribeForgeLoader();
                        _forgeLoader = null;
                        _forgeLoaderVersion = null;
                        StartForgeLoader(true);
                    }
                    else
                    {
                        loader?.Start(null, true);
                    }

                    RefreshKind(card.Kind);
                };
                body.Children.Add(retry);
            }

            return;
        }

        if (!AddVersionItems(card.Kind, body))
            body.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoAvailableVersion"),
                    Margin = new Thickness(6d, 4d, 0d, 4d),
                    Opacity = 0.7d
                });
    }

    private bool AddVersionItems(InstallLoaderKind kind, StackPanel body)
    {
        switch (kind)
        {
            case InstallLoaderKind.Forge:
            {
                var versions = GetForgeVersions();
                if (versions.Count == 0)
                    return false;
                var recommended = versions.LastOrDefault(version => version.IsRecommended);
                var latest = versions.FirstOrDefault();
                if (recommended is not null)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            recommended,
                            $"{Lang.Text("Download.Version.Type.Recommended")}  |  {Lang.Text("Download.Version.Type.Stable")}",
                            "Blocks/Anvil.png"));
                if (latest is not null && !ReferenceEquals(latest, recommended))
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            latest,
                            $"{Lang.Text("Download.Version.Latest.Title")}  |  {Lang.Text("Download.Version.Type.Stable")}",
                            "Blocks/Anvil.png"));
                AddAllLabel(body, versions.Count);
                foreach (var version in versions)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            version,
                            GetForgelikeInfo(version),
                            "Blocks/Anvil.png"));
                return true;
            }
            case InstallLoaderKind.NeoForge:
            {
                var versions = GetNeoForgeVersions();
                if (versions.Count == 0)
                    return false;
                var stable = versions.FirstOrDefault(version => !version.IsBeta);
                var beta = versions.FirstOrDefault(version => version.IsBeta);
                if (stable is not null)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            stable,
                            $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {Lang.Text("Download.Version.Type.Stable")}",
                            "Blocks/NeoForge.png"));
                if (beta is not null)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            beta,
                            $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {Lang.Text("Download.Version.Type.Preview")}",
                            "Blocks/NeoForge.png"));
                AddAllLabel(body, versions.Count);
                foreach (var version in versions)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            version,
                            version.IsBeta
                                ? Lang.Text("Download.Version.Type.Preview")
                                : Lang.Text("Download.Version.Type.Stable"),
                            "Blocks/NeoForge.png"));
                return true;
            }
            case InstallLoaderKind.Cleanroom:
            {
                var versions = GetCleanroomVersions();
                if (versions.Count == 0)
                    return false;
                body.Children.Add(
                    CreateForgelikeItem(
                        kind,
                        versions[0],
                        $"{Lang.Text("Download.Version.Fresh.Latest")}  |  {Lang.Text("Download.Version.Type.Preview")}",
                        "Blocks/Cleanroom.png"));
                AddAllLabel(body, versions.Count);
                foreach (var version in versions)
                    body.Children.Add(
                        CreateForgelikeItem(
                            kind,
                            version,
                            version.IsBeta
                                ? Lang.Text("Download.Version.Type.Preview")
                                : Lang.Text("Download.Version.Type.Stable"),
                            "Blocks/Cleanroom.png"));
                return true;
            }
            case InstallLoaderKind.OptiFine:
            {
                var versions = GetOptiFineVersions();
                if (versions.Count == 0)
                    return false;
                foreach (var version in versions)
                    body.Children.Add(
                        CreateItem(
                            kind,
                            version,
                            version.DisplayName ?? version.NameVersion ?? "",
                            GetOptiFineInfo(version),
                            "Blocks/GrassPath.png",
                            version.Inherit ?? ""));
                return true;
            }
            case InstallLoaderKind.LiteLoader:
            {
                var versions = GetLiteLoaderVersions();
                if (versions.Count == 0)
                    return false;
                foreach (var version in versions)
                    body.Children.Add(
                        CreateItem(
                            kind,
                            version,
                            version.Inherit ?? "",
                            GetLiteLoaderInfo(version),
                            "Blocks/Egg.png",
                            version.ReleaseTime?.ToString("g", CultureInfo.CurrentCulture) ?? ""));
                return true;
            }
            case InstallLoaderKind.Fabric:
            {
                foreach (var version in GetFabricLoaderVersions())
                    body.Children.Add(
                        CreateJsonLoaderItem(
                            kind,
                            version,
                            "Blocks/Fabric.png"));
                return body.Children.Count > 0;
            }
            case InstallLoaderKind.LegacyFabric:
            {
                foreach (var version in GetLegacyFabricLoaderVersions())
                    body.Children.Add(
                        CreateJsonLoaderItem(
                            kind,
                            version,
                            "Blocks/Fabric.png"));
                return body.Children.Count > 0;
            }
            case InstallLoaderKind.FabricApi:
            case InstallLoaderKind.LegacyFabricApi:
            case InstallLoaderKind.OptiFabric:
            {
                var files = kind switch
                {
                    InstallLoaderKind.FabricApi => GetCompatibleCompFiles(
                        CompDownloadLoaders.DlFabricApiLoader.output,
                        false),
                    InstallLoaderKind.LegacyFabricApi => GetCompatibleCompFiles(
                        CompDownloadLoaders.DlLegacyFabricApiLoader.output,
                        false),
                    _ => GetCompatibleCompFiles(
                        CompDownloadLoaders.DlOptiFabricLoader.output,
                        true)
                };
                foreach (var file in files)
                    body.Children.Add(
                        CreateCompItem(kind, file, GetCompLogo(kind)));
                return files.Count > 0;
            }
            case InstallLoaderKind.LabyMod:
            {
                foreach (var choice in GetLabyModChoices())
                    body.Children.Add(
                        CreateItem(
                            kind,
                            choice,
                            choice.DisplayName,
                            choice.Channel == "snapshot"
                                ? Lang.Text("Download.Version.Type.Snapshot")
                                : Lang.Text("Download.Version.Type.Stable"),
                            "Blocks/LabyMod.png",
                            "LabyMod"));
                return body.Children.Count > 0;
            }
            default:
                return false;
        }
    }

    private static string GetCompLogo(InstallLoaderKind kind)
    {
        return kind switch
        {
            InstallLoaderKind.OptiFabric => "Blocks/OptiFabric.png",
            _ => "Blocks/Fabric.png"
        };
    }

    private string FormatFabricApiTitle(Comp.CompFile file)
    {
        var title = file.DisplayName ?? "";
        if (!title.StartsWith("[", StringComparison.Ordinal)
            && !string.IsNullOrEmpty(MinecraftName))
            title = $"[{MinecraftName}] {title}";
        return title.Replace("Fabric API ", "").Replace(" build ", ".").Trim();
    }

    private static string StripGameVersionPrefix(string title)
    {
        if (!title.StartsWith("[", StringComparison.Ordinal))
            return title;
        var end = title.IndexOf(']');
        return end >= 0 ? title[(end + 1)..].Trim() : title;
    }

    private static string GetOptiFineInfo(GameDownload.DlOptiFineListEntry entry)
    {
        var parts = new List<string>
        {
            entry.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Release")
        };
        if (entry.ReleaseTime is { } releaseTime)
            parts.Add(
                Lang.Text(
                    "Download.Version.ReleaseDate",
                    Lang.Date(releaseTime, "d")));
        if (entry.RequiredForgeVersion is null)
            parts.Add(Lang.Text("Download.Version.Optifine.IncompatibleForge"));
        else if (!string.IsNullOrEmpty(entry.RequiredForgeVersion))
            parts.Add(
                Lang.Text(
                    "Download.Version.Optifine.CompatibleForge",
                    entry.RequiredForgeVersion));
        return string.Join("  |  ", parts);
    }

    private static string GetLiteLoaderInfo(GameDownload.DlLiteLoaderListEntry entry)
    {
        var parts = new List<string>
        {
            entry.IsPreview
                ? Lang.Text("Download.Version.Type.Preview")
                : Lang.Text("Download.Version.Type.Stable")
        };
        if (entry.ReleaseTime is { } releaseTime)
            parts.Add(
                Lang.Text(
                    "Download.Version.ReleaseDate",
                    Lang.Date(releaseTime, "g")));
        return string.Join("  |  ", parts);
    }

    private static string GetForgelikeInfo(GameDownload.DlForgelikeEntry entry)
    {
        return entry is GameDownload.DlForgeVersionEntry forge
            && forge.ReleaseTime is { } releaseTime
                ? Lang.Text("Download.Version.ReleaseDate", Lang.Date(releaseTime, "g"))
                : Lang.Text("Download.Version.Type.Stable");
    }

    private static void AddAllLabel(StackPanel body, int count)
    {
        body.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Version.AllVersions", count),
                Margin = new Thickness(6d, 13d, 0d, 4d),
                Opacity = 0.7d
            });
    }

    private MyListItem CreateForgelikeItem(
        InstallLoaderKind kind,
        GameDownload.DlForgelikeEntry entry,
        string info,
        string logo)
    {
        return CreateItem(kind, entry, entry.VersionName, info, logo, entry.VersionName);
    }

    private MyListItem CreateJsonLoaderItem(
        InstallLoaderKind kind,
        JsonObject entry,
        string logo)
    {
        var version = (entry["version"]?.ToString() ?? "").Replace("+build", "");
        var stable = entry["stable"]?.GetValue<bool>() == true;
        return CreateItem(
            kind,
            entry,
            version,
            stable
                ? Lang.Text("Download.Version.Type.Stable")
                : Lang.Text("Download.Version.Type.Preview"),
            logo,
            "");
    }

    private MyListItem CreateCompItem(
        InstallLoaderKind kind,
        Comp.CompFile file,
        string logo)
    {
        var title = file.DisplayName;
        if (kind == InstallLoaderKind.FabricApi)
            title = FormatFabricApiTitle(file);
        else if (kind == InstallLoaderKind.LegacyFabricApi)
            title = title.Replace("Legacy Fabric API ", "");
        else
            title = title
                .ToLower()
                .Replace("optifabric-", "")
                .Replace(".jar", "")
                .Trim()
                .TrimStart('v');
        var info = file.ReleaseDate == default
            ? file.StatusDescription
            : $"{file.StatusDescription}  |  {Lang.Date(file.ReleaseDate, "g")}";
        return CreateItem(kind, file, title, info, logo, file.FileName);
    }

    private MyListItem CreateItem(
        InstallLoaderKind kind,
        object entry,
        string title,
        string info,
        string logo,
        string _)
    {
        var item = new MyListItem
        {
            Title = title,
            Height = 42d,
            Type = MyListItem.CheckType.Clickable,
            Logo = $"{LauncherPaths.ImageRoot}{logo}",
            LogoScale = 1d,
            Info = info,
            Tag = entry
        };
        item.Click += (_, _) =>
        {
            if (_isInstalling)
                return;
            SelectEntry(kind, entry);
        };
        return item;
    }

    private void SelectEntry(InstallLoaderKind kind, object entry)
    {
        if (_isInstalling || GetCardError(_cards.First(card => card.Kind == kind)) is not null)
            return;
        switch (kind)
        {
            case InstallLoaderKind.Forge:
                Selection.Forge = (GameDownload.DlForgeVersionEntry)entry;
                ClearMainOtherThan(kind);
                break;
            case InstallLoaderKind.Cleanroom:
                Selection.Cleanroom = (GameDownload.DlCleanroomListEntry)entry;
                ClearMainOtherThan(kind);
                break;
            case InstallLoaderKind.NeoForge:
                Selection.NeoForge = (GameDownload.DlNeoForgeListEntry)entry;
                ClearMainOtherThan(kind);
                break;
            case InstallLoaderKind.Fabric:
                Selection.FabricVersion = ((JsonObject)entry)["version"]?.ToString();
                ClearMainOtherThan(kind);
                _autoFabricApi = false;
                break;
            case InstallLoaderKind.LegacyFabric:
                Selection.LegacyFabricVersion = ((JsonObject)entry)["version"]?.ToString();
                ClearMainOtherThan(kind);
                _autoLegacyFabricApi = false;
                break;
            case InstallLoaderKind.FabricApi:
                Selection.FabricApi = (Comp.CompFile)entry;
                _autoFabricApi = true;
                break;
            case InstallLoaderKind.LegacyFabricApi:
                Selection.LegacyFabricApi = (Comp.CompFile)entry;
                _autoLegacyFabricApi = true;
                break;
            case InstallLoaderKind.OptiFabric:
                Selection.OptiFabric = (Comp.CompFile)entry;
                _autoOptiFabric = true;
                break;
            case InstallLoaderKind.LabyMod:
                var choice = (LabyModChoice)entry;
                Selection.LabyModChannel = choice.Channel;
                Selection.LabyModCommitRef = choice.CommitReference;
                Selection.LabyModVersion = choice.DisplayName;
                ClearMainOtherThan(kind);
                break;
            case InstallLoaderKind.OptiFine:
                Selection.OptiFine = (GameDownload.DlOptiFineListEntry)entry;
                if (Selection.Forge is not null
                    && !IsOptiFineSuitForForge(Selection.OptiFine, Selection.Forge))
                    Selection.Forge = null;
                _autoOptiFabric = false;
                break;
            case InstallLoaderKind.LiteLoader:
                Selection.LiteLoader = (GameDownload.DlLiteLoaderListEntry)entry;
                break;
        }

        // 关闭展开卡片，更新文件名与警告。
        var card = _cards.First(item => item.Kind == kind);
        card.Card.IsSwapped = true;
        RefreshAll();
        NotifyChanged();
    }

    private void ClearMainOtherThan(InstallLoaderKind selected)
    {
        if (selected != InstallLoaderKind.Forge)
            Selection.Forge = null;
        if (selected != InstallLoaderKind.Cleanroom)
            Selection.Cleanroom = null;
        if (selected != InstallLoaderKind.NeoForge)
            Selection.NeoForge = null;
        if (selected != InstallLoaderKind.Fabric)
        {
            Selection.FabricVersion = null;
            Selection.FabricApi = null;
            Selection.OptiFabric = null;
            _autoFabricApi = false;
            _autoOptiFabric = false;
        }

        if (selected != InstallLoaderKind.LegacyFabric)
        {
            Selection.LegacyFabricVersion = null;
            Selection.LegacyFabricApi = null;
            _autoLegacyFabricApi = false;
        }

        if (selected != InstallLoaderKind.LabyMod)
        {
            Selection.LabyModChannel = null;
            Selection.LabyModCommitRef = null;
            Selection.LabyModVersion = null;
        }
    }

    private void ClearKind(InstallLoaderKind kind)
    {
        switch (kind)
        {
            case InstallLoaderKind.Forge:
                Selection.Forge = null;
                break;
            case InstallLoaderKind.Cleanroom:
                Selection.Cleanroom = null;
                break;
            case InstallLoaderKind.NeoForge:
                Selection.NeoForge = null;
                break;
            case InstallLoaderKind.Fabric:
                Selection.FabricVersion = null;
                Selection.FabricApi = null;
                Selection.OptiFabric = null;
                _autoFabricApi = false;
                _autoOptiFabric = false;
                break;
            case InstallLoaderKind.LegacyFabric:
                Selection.LegacyFabricVersion = null;
                Selection.LegacyFabricApi = null;
                _autoLegacyFabricApi = false;
                break;
            case InstallLoaderKind.FabricApi:
                Selection.FabricApi = null;
                _autoFabricApi = true;
                break;
            case InstallLoaderKind.LegacyFabricApi:
                Selection.LegacyFabricApi = null;
                _autoLegacyFabricApi = true;
                break;
            case InstallLoaderKind.OptiFabric:
                Selection.OptiFabric = null;
                _autoOptiFabric = true;
                break;
            case InstallLoaderKind.LabyMod:
                Selection.LabyModChannel = null;
                Selection.LabyModCommitRef = null;
                Selection.LabyModVersion = null;
                _existingLabyModVersion = null;
                break;
            case InstallLoaderKind.OptiFine:
                Selection.OptiFine = null;
                Selection.OptiFabric = null;
                _autoOptiFabric = false;
                break;
            case InstallLoaderKind.LiteLoader:
                Selection.LiteLoader = null;
                break;
        }
    }

    private void TryResolveExistingLabyMod()
    {
        if (Selection.LabyModCommitRef is not null
            || string.IsNullOrEmpty(_existingLabyModVersion))
            return;
        var choice = GetLabyModChoices()
            .FirstOrDefault(item =>
                string.Equals(
                    item.Version,
                    _existingLabyModVersion,
                    StringComparison.OrdinalIgnoreCase)
                || item.DisplayName.StartsWith(
                    _existingLabyModVersion,
                    StringComparison.OrdinalIgnoreCase));
        if (choice is null)
            return;
        Selection.LabyModChannel = choice.Channel;
        Selection.LabyModCommitRef = choice.CommitReference;
        Selection.LabyModVersion = choice.DisplayName;
    }

    private bool TryAutoSelectApis(out List<InstallLoaderKind> selected)
    {
        selected = [];
        if (_updating || MinecraftName is null)
            return false;
        _updating = true;
        try
        {
            if (Selection.FabricVersion is not null
                && Selection.FabricApi is null
                && !_autoFabricApi)
            {
                var file = GetCompatibleCompFiles(
                        CompDownloadLoaders.DlFabricApiLoader.output,
                        false)
                    .FirstOrDefault();
                if (file is not null)
                {
                    Selection.FabricApi = file;
                    _autoFabricApi = true;
                    selected.Add(InstallLoaderKind.FabricApi);
                    LauncherCommon.Log($"[Install] 已自动选择 Fabric API：{file.DisplayName}");
                }
            }

            if (Selection.LegacyFabricVersion is not null
                && Selection.LegacyFabricApi is null
                && !_autoLegacyFabricApi)
            {
                var file = GetCompatibleCompFiles(
                        CompDownloadLoaders.DlLegacyFabricApiLoader.output,
                        false)
                    .FirstOrDefault();
                if (file is not null)
                {
                    Selection.LegacyFabricApi = file;
                    _autoLegacyFabricApi = true;
                    selected.Add(InstallLoaderKind.LegacyFabricApi);
                    LauncherCommon.Log($"[Install] 已自动选择 Legacy Fabric API：{file.DisplayName}");
                }
            }

            if (Selection.FabricVersion is not null
                && Selection.OptiFine is not null
                && Selection.OptiFabric is null
                && !_autoOptiFabric
                && McInstanceInfo.VersionToDrop(MinecraftName, true) is not (>= 140 and <= 150))
            {
                var file = GetCompatibleCompFiles(
                        CompDownloadLoaders.DlOptiFabricLoader.output,
                        true)
                    .FirstOrDefault();
                if (file is not null)
                {
                    Selection.OptiFabric = file;
                    _autoOptiFabric = true;
                    selected.Add(InstallLoaderKind.OptiFabric);
                    LauncherCommon.Log($"[Install] 已自动选择 OptiFabric：{file.DisplayName}");
                }
            }
        }
        finally
        {
            _updating = false;
        }

        return selected.Count > 0;
    }

    private void NotifyChanged()
    {
        if (_updating)
            return;
        _changed();
    }

    internal sealed class CardState
    {
        public required InstallLoaderCard Host;
        public required InstallLoaderKind Kind;
        public required string TitleKey;

        public MyCard Card => Host.Card;

        public StackPanel Body => Host.Body;

        public MyIconButton ClearButton => Host.ClearButton;

        public TextBlock Info => Host.Info;
    }

    internal sealed class InstallLoaderWarningState
    {
        public bool FabricApi;
        public bool LegacyFabricApi;
        public bool LegacyOptiFabric;
        public bool ModOptiFine;
        public bool OptiFabric;
        public bool OptiFabricOld;
    }

    private sealed class LabyModChoice
    {
        public required string Channel;
        public required string CommitReference;
        public required string DisplayName;
        public required string Version;
    }

    #region 版本数据

    private List<GameDownload.DlForgeVersionEntry> GetForgeVersions()
    {
        if (_forgeLoader is not { State: LoadState.Finished } loader
            || loader is not Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>> task
            || !string.Equals(task.input, MinecraftName, StringComparison.Ordinal))
            return [];
        var versions = (task.output ?? new List<GameDownload.DlForgeVersionEntry>())
            .Where(version => version.Category is not ("universal" or "client"))
            .OrderByDescending(version => version)
            .ToList();
        if (Selection.OptiFine is not null)
            versions = versions
                .Where(version => IsOptiFineSuitForForge(Selection.OptiFine, version))
                .ToList();
        return versions;
    }

    private List<GameDownload.DlNeoForgeListEntry> GetNeoForgeVersions()
    {
        return (GameDownload.DlNeoForgeListLoader.output.Value
                ?? new List<GameDownload.DlNeoForgeListEntry>())
            .Where(version => version.Inherit == MinecraftName)
            .OrderByDescending(version => version)
            .ToList();
    }

    private List<GameDownload.DlCleanroomListEntry> GetCleanroomVersions()
    {
        if (MinecraftName != "1.12.2")
            return [];
        return (GameDownload.DlCleanroomListLoader.output.Value
                ?? new List<GameDownload.DlCleanroomListEntry>())
            .Where(version => version.Inherit == "1.12.2")
            .OrderByDescending(version => version)
            .ToList();
    }

    private List<GameDownload.DlOptiFineListEntry> GetOptiFineVersions()
    {
        var versions = (GameDownload.DlOptiFineListLoader.output.Value
                ?? new List<GameDownload.DlOptiFineListEntry>())
            .Where(version => (version.DisplayName ?? "").StartsWith(
                MinecraftName + " ",
                StringComparison.OrdinalIgnoreCase))
            .Where(version => Selection.Forge is null
                || IsOptiFineSuitForForge(version, Selection.Forge))
            .ToList();
        return versions
            .OrderBy(version => version.IsPreview)
            .ThenByDescending(
                version => version.DisplayName ?? version.NameVersion ?? "",
                new McVersionComparer.VersionComparer())
            .ToList();
    }

    private List<GameDownload.DlLiteLoaderListEntry> GetLiteLoaderVersions()
    {
        return (GameDownload.DlLiteLoaderListLoader.output.Value
                ?? new List<GameDownload.DlLiteLoaderListEntry>())
            .Where(version => string.Equals(
                version.Inherit,
                MinecraftName,
                StringComparison.Ordinal))
            .ToList();
    }

    private List<JsonObject> GetFabricLoaderVersions()
    {
        return GetFabricLoaderArray(GameDownload.DlFabricListLoader.output.Value?["loader"])
            .OfType<JsonObject>()
            .ToList();
    }

    private List<JsonObject> GetLegacyFabricLoaderVersions()
    {
        return GetFabricLoaderArray(GameDownload.DlLegacyFabricListLoader.output.Value?["loader"])
            .OfType<JsonObject>()
            .ToList();
    }

    private static JsonArray GetFabricLoaderArray(JsonNode? node)
    {
        return node as JsonArray ?? new JsonArray();
    }

    private IEnumerable<string> GetFabricGameVersions()
    {
        return (GameDownload.DlFabricListLoader.output.Value?["game"] as JsonArray
                ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(version => version["version"]?.ToString() ?? "");
    }

    private IEnumerable<string> GetLegacyFabricGameVersions()
    {
        return (GameDownload.DlLegacyFabricListLoader.output.Value?["game"] as JsonArray
                ?? new JsonArray())
            .OfType<JsonObject>()
            .Select(version => version["version"]?.ToString() ?? "");
    }

    private List<Comp.CompFile> GetCompatibleCompFiles(
        List<Comp.CompFile>? files,
        bool optiFabric)
    {
        if (files is null || MinecraftName is null)
            return [];
        var target = NormalizeGameVersion(MinecraftName);
        return files
            .Where(file => file.GameVersions.Any(version =>
                    string.Equals(
                        NormalizeGameVersion(version),
                        target,
                        StringComparison.OrdinalIgnoreCase))
                || file.RawGameVersions.Any(version =>
                    string.Equals(
                        NormalizeGameVersion(version),
                        target,
                        StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(file => file.ReleaseDate)
            .ToList();
    }

    private List<LabyModChoice> GetLabyModChoices()
    {
        var result = new List<LabyModChoice>();
        var value = GameDownload.DlLabyModListLoader.output.Value;
        if (value is null || MinecraftName is null)
            return result;
        foreach (var channel in new[]
                 {
                     "production",
                     "snapshot"
                 })
        {
            var channelNode = value[channel] as JsonObject;
            if (channelNode is null)
                continue;
            var minecraftVersions = channelNode["minecraftVersions"] as JsonArray;
            if (minecraftVersions is null
                || !minecraftVersions
                    .OfType<JsonObject>()
                    .Any(version =>
                        string.Equals(
                            version["version"]?.ToString(),
                            MinecraftName,
                            StringComparison.OrdinalIgnoreCase)))
                continue;
            var version = channelNode["labyModVersion"]?.ToString() ?? "";
            result.Add(
                new LabyModChoice
                {
                    Channel = channel,
                    CommitReference = channelNode["commitReference"]?.ToString() ?? "",
                    Version = version,
                    DisplayName =
                        $"{version} {(channel == "snapshot" ? Lang.Text("Download.Version.Type.Snapshot") : Lang.Text("Download.Version.Type.Stable"))}"
                });
        }

        return result;
    }

    private static bool IsOptiFineSuitForForge(
        GameDownload.DlOptiFineListEntry optiFine,
        GameDownload.DlForgeVersionEntry forge)
    {
        if (!string.Equals(forge.Inherit ?? "", optiFine.Inherit ?? "", StringComparison.Ordinal))
            return false;
        if (optiFine.RequiredForgeVersion is null)
            return false;
        if (string.IsNullOrWhiteSpace(optiFine.RequiredForgeVersion))
            return true;
        if (optiFine.RequiredForgeVersion.Contains('.'))
            return McVersionComparer.CompareVersion(
                    forge.version.ToString(),
                    optiFine.RequiredForgeVersion)
                == 0;
        return Math.Abs(forge.version.Revision - Convert.ToDouble(optiFine.RequiredForgeVersion))
            < 0.01d;
    }

    private static string NormalizeGameVersion(string version)
    {
        return version
            .Replace("∞", "infinite")
            .Replace("Combat Test 7c", "1.16_combat-3")
            .ToLowerInvariant();
    }

    #endregion
}
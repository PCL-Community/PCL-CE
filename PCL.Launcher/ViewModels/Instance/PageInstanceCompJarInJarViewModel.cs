using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Instance;

namespace PCL;

/// <summary>内嵌模组（Jar-in-Jar）页的视图模型。</summary>
public partial class PageInstanceCompJarInJarViewModel : ViewModelBase
{
    private readonly List<LocalComp.LocalCompFile> _allMods = [];
    private JarInJarIndex? _index;
    private string _instanceMc = string.Empty;
    private Loading.LoaderBase.OnStateChangedUiEventHandler? _loaderStateHandler;

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsLoading { get; set; }

    [ObservableProperty] public partial bool ShowContent { get; set; }

    [ObservableProperty] public partial bool ShowEmpty { get; set; }

    [ObservableProperty] public partial string EmptyTitle { get; set; } = string.Empty;

    [ObservableProperty] public partial string EmptyMessage { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CardWarningsVisible { get; set; }

    [ObservableProperty] public partial bool CardConflictsVisible { get; set; }

    [ObservableProperty] public partial bool SectionRelationsVisible { get; set; }

    [ObservableProperty] public partial bool SectionBundledVisible { get; set; }

    [ObservableProperty] public partial string MessageText { get; set; } = string.Empty;

    public List<JijNoticeRow> WarningRows { get; } = [];

    public List<JijConflictRow> ConflictRows { get; } = [];

    public List<JijRelationRow> RelationRows { get; } = [];

    public List<JijBundledRow> BundledRows { get; } = [];

    public McInstance? Instance { get; private set; }

    public string? CurrentFolderPath => null;

    public bool IsCurrentPage { get; set; } = true;

    public event Action? ReloadRequested;

    public event Action? StateChanged;

    public void SetInstance(McInstance? instance)
    {
        Instance = instance;
    }

    partial void OnSearchTextChanged(string value)
    {
        RebuildSections();
    }

    public void Reload(object? pageHost)
    {
        Instance ??= PageInstanceLeft.ResolveInstance();
        if (Instance is null)
        {
            SetEmptyState(
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"));
            return;
        }

        var output = LocalComp.CompResourceListLoader.output;
        if (output is null || !IsCurrentInstanceModOutput(output))
        {
            StartLoader(pageHost);
            return;
        }

        BuildFromOutput(output);
    }

    public void DetachLoaderHandler()
    {
        if (_loaderStateHandler is null)
            return;
        LocalComp.CompResourceListLoader.OnStateChangedUi -= _loaderStateHandler;
        _loaderStateHandler = null;
    }

    private void StartLoader(object? pageHost)
    {
        if (Instance is null)
            return;
        ShowLoading();
        DetachLoaderHandler();
        _loaderStateHandler = (_, newState, _) =>
        {
            if (newState is not (LoadState.Finished or LoadState.Failed or LoadState.Aborted))
                return;
            LauncherCommon.RunInUi(() =>
            {
                try
                {
                    var output = LocalComp.CompResourceListLoader.output;
                    if (newState == LoadState.Finished && output is not null)
                        BuildFromOutput(output);
                    else
                        SetEmptyState(
                            Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
                            Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"));
                }
                finally
                {
                    DetachLoaderHandler();
                }
            });
        };
        LocalComp.CompResourceListLoader.OnStateChangedUi += _loaderStateHandler;

        var folder = GetModFolder(Instance);
        var started = Loading.LoaderFolderRun(
            LocalComp.CompResourceListLoader,
            folder,
            Loading.LoaderFolderRunType.ForceRun,
            loaderInput: new LocalComp.CompLocalLoaderData
            {
                gameVersion = Instance,
                compType = Comp.CompType.Mod,
                compPath = folder + Path.DirectorySeparatorChar,
                loaders = LocalComp.GetCurrentVersionModLoader(),
                frm = pageHost
            });
        if (!started && LocalComp.CompResourceListLoader.State == LoadState.Finished)
        {
            var output = LocalComp.CompResourceListLoader.output;
            if (output is not null)
                BuildFromOutput(output);
        }
    }

    private static string GetModFolder(McInstance instance)
    {
        return instance.PathIndie
            + (instance.Info.HasLabyMod
                ? Path.Combine("labymod-neo", "fabric", instance.Info.VanillaName)
                : string.Empty)
            + LocalComp.GetPathNameByCompType(Comp.CompType.Mod);
    }

    private bool IsCurrentInstanceModOutput(List<LocalComp.LocalCompFile> output)
    {
        if (Instance is null || output.Count == 0)
            return false;
        var input = LocalComp.CompResourceListLoader.input;
        if (input is null || input.compType != Comp.CompType.Mod || input.gameVersion is null)
            return false;

        try
        {
            var instancePath = Path
                .GetFullPath(Instance.PathInstance)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var loaderPath = Path
                .GetFullPath(input.gameVersion.PathInstance)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(instancePath, loaderPath, StringComparison.OrdinalIgnoreCase))
                return false;
            var root = Path
                    .GetFullPath(Instance.PathIndie)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var first = output.FirstOrDefault(entry => !string.IsNullOrEmpty(entry.path));
            return first is null
                || Path
                    .GetFullPath(first.path)
                    .StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void BuildFromOutput(IReadOnlyList<LocalComp.LocalCompFile> output)
    {
        if (Instance is null)
            return;
        var allMods = output
            .Where(entry => !entry.IsFolder
                && entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable)
            .ToList();
        if (allMods.Count == 0)
        {
            SetEmptyState(
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"));
            return;
        }

        _instanceMc = Instance.Info.VanillaName;
        _index = new JarInJarIndex(allMods, _instanceMc);
        _allMods.Clear();
        _allMods.AddRange(allMods);
        IsLoading = false;
        ShowContent = true;
        ShowEmpty = false;
        RebuildSections();
    }

    public void RebuildSections()
    {
        if (_index is null)
            return;

        WarningRows.Clear();
        foreach (var host in _allMods.Where(host =>
                     host.State == LocalComp.LocalCompFile.LocalFileStatus.Fine))
        {
            if (Match(host))
                AppendWarnings(host, null, host.DetectedLoader);
            foreach (var node in _index.GetLoadableEmbedded(host))
                if (Match(node))
                    AppendWarnings(node, host, node.JijLoader);
        }

        ConflictRows.Clear();
        foreach (var (a, b, hard) in _index.FindActiveConflicts())
        {
            if (!Match(a) && !Match(b))
                continue;
            ConflictRows.Add(
                new JijConflictRow
                {
                    Severity = Lang.Text(
                        hard
                            ? "Instance.Resource.Mod.JarInJar.Conflict.Incompatible"
                            : "Instance.Resource.Mod.JarInJar.Conflict.Discouraged"),
                    SeverityKind = hard ? JijRowKind.Error : JijRowKind.Warn,
                    Pair = Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Conflict.Pair",
                        DisplayName(a),
                        DisplayName(b))
                });
        }

        RelationRows.Clear();
        foreach (var host in _allMods)
        {
            var hostDeps = JarInJarIndex
                .BuildOwnDependencies(host, host.DetectedLoader ?? string.Empty)
                .Where(dep => !DependencyIds.IsPlatform(dep.DepId))
                .ToList();
            if (hostDeps.Count > 0 && (Match(host) || hostDeps.Any(dep => Contains(dep.DepId))))
                RelationRows.Add(CreateRelationRow(host, null, hostDeps));

            foreach (var node in _index.GetLoadableEmbedded(host))
            {
                var nodeDeps = JarInJarIndex
                    .BuildOwnDependencies(node, node.JijLoader ?? string.Empty)
                    .Where(dep => !DependencyIds.IsPlatform(dep.DepId))
                    .ToList();
                if (nodeDeps.Count == 0)
                    continue;
                if (!Match(node) && !nodeDeps.Any(dep => Contains(dep.DepId)))
                    continue;
                RelationRows.Add(CreateRelationRow(node, host, nodeDeps));
            }
        }

        BundledRows.Clear();
        foreach (var mod in _allMods.Where(entry => entry.EmbeddedMods is { Count: > 0 }
                     && (Match(entry)
                         || AnyEmbeddedMatch(entry.EmbeddedMods))))
            BundledRows.Add(CreateBundledRow(mod));

        CardWarningsVisible = WarningRows.Count > 0;
        CardConflictsVisible = ConflictRows.Count > 0;
        SectionRelationsVisible = RelationRows.Count > 0;
        SectionBundledVisible = BundledRows.Count > 0;
        StateChanged?.Invoke();
    }

    private JijRelationRow CreateRelationRow(
        LocalComp.LocalCompFile mod,
        LocalComp.LocalCompFile? parent,
        List<JarInJarIndex.DepRow> deps)
    {
        var title = parent is null
            ? DisplayName(mod)
            : $"{DisplayName(mod)}  {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", DisplayName(parent))}";
        var row = new JijRelationRow
        {
            Title = title
        };
        foreach (var dep in deps)
        {
            var status = _index!.Analyze(mod, dep);
            var text = dep.Raw is null ? dep.DepId ?? string.Empty : $"{dep.DepId} {dep.Raw}";
            row.Dependencies.Add(
                new JijDependencyRow
                {
                    Primary = text,
                    Optional = dep.Optional
                        ? Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Optional")
                        : string.Empty,
                    Status = StatusLabel(status),
                    StatusKind = status switch
                    {
                        JijDepStatus.Installed => JijRowKind.Ok,
                        JijDepStatus.Disabled => JijRowKind.Warn,
                        JijDepStatus.VersionMismatch => JijRowKind.Warn,
                        _ => JijRowKind.Error
                    },
                    UseThemeBrush = dep.Optional || status == JijDepStatus.Bundled
                });
        }

        return row;
    }

    private JijBundledRow CreateBundledRow(LocalComp.LocalCompFile mod)
    {
        var row = new JijBundledRow
        {
            Title = DisplayName(mod)
        };
        if (_index is null)
            return row;
        var selected = mod.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
            ? _index.GetLoadableEmbedded(mod).ToHashSet()
            : [];
        AppendTreeRows(row.Rows, mod.EmbeddedMods, 0, selected);
        return row;
    }

    private void AppendTreeRows(
        List<JijTreeRow> rows,
        List<LocalComp.LocalCompFile>? embedded,
        int depth,
        HashSet<LocalComp.LocalCompFile> selected)
    {
        if (embedded is null || embedded.Count == 0)
            return;

        foreach (var ungrouped in embedded.Where(entry => string.IsNullOrEmpty(entry.ModId)))
        {
            rows.Add(
                CreateTreeRow(
                    ungrouped,
                    depth,
                    0,
                    McConstraintMatcher.MatchKind.None,
                    null,
                    false));
            AppendTreeRows(rows, ungrouped.EmbeddedMods, depth + 1, selected);
        }

        foreach (var group in embedded
                     .Where(entry => !string.IsNullOrEmpty(entry.ModId))
                     .GroupBy(entry => entry.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group.ToList();
            if (versions.Count == 1)
            {
                rows.Add(
                    CreateTreeRow(
                        versions[0],
                        depth,
                        0,
                        McConstraintMatcher.MatchKind.None,
                        null,
                        false));
                AppendTreeRows(rows, versions[0].EmbeddedMods, depth + 1, selected);
                continue;
            }

            var scored = versions
                .Select(version => (
                    Version: version,
                    Kind: McConstraintMatcher.Match(
                        version.FileName,
                        version.Version ?? string.Empty,
                        version.JijTargetMcVersion ?? string.Empty,
                        version.JijLoader ?? string.Empty,
                        _instanceMc)))
                .OrderByDescending(pair => (int)pair.Kind)
                .ToList();
            var selectedRepresentative = versions.FirstOrDefault(selected.Contains);
            var representative = selectedRepresentative ?? scored[0].Version;
            var kind = selectedRepresentative is null
                ? scored[0].Kind
                : McConstraintMatcher.Match(
                    representative.FileName,
                    representative.Version ?? string.Empty,
                    representative.JijTargetMcVersion ?? string.Empty,
                    representative.JijLoader ?? string.Empty,
                    _instanceMc);
            var tooltip = string.Join(
                "\n",
                versions.Select(version => string.IsNullOrWhiteSpace(version.Version)
                    ? version.FileName
                    : version.Version));
            rows.Add(
                CreateTreeRow(
                    representative,
                    depth,
                    versions.Count,
                    kind,
                    tooltip,
                    selectedRepresentative is not null));
            AppendTreeRows(rows, representative.EmbeddedMods, depth + 1, selected);
        }
    }

    private static JijTreeRow CreateTreeRow(
        LocalComp.LocalCompFile entry,
        int depth,
        int versionCount,
        McConstraintMatcher.MatchKind kind,
        string? versionsTooltip,
        bool selected)
    {
        var version = CleanPlaceholder(entry.Version);
        var mc = CleanPlaceholder(entry.JijTargetMcVersion);
        var showVersions = versionCount > 1;
        var isCurrent = selected
            || kind is McConstraintMatcher.MatchKind.Exact or McConstraintMatcher.MatchKind.Range;
        var isIncompatible = !isCurrent && kind == McConstraintMatcher.MatchKind.Incompatible;
        return new JijTreeRow
        {
            Depth = depth,
            Name = DisplayName(entry),
            Version = version ?? string.Empty,
            Loader = entry.JijLoader ?? string.Empty,
            McVersion = mc ?? string.Empty,
            Versions = showVersions
                ? Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Versions", versionCount)
                : string.Empty,
            VersionsTooltip = showVersions ? versionsTooltip : null,
            IsCurrent = isCurrent,
            IsIncompatible = isIncompatible,
            CurrentText = isCurrent
                ? Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Current")
                : string.Empty,
            IncompatibleText = isIncompatible
                ? Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Incompatible")
                : string.Empty
        };
    }

    private void AppendWarnings(
        LocalComp.LocalCompFile mod,
        LocalComp.LocalCompFile? parent,
        string? loader)
    {
        if (_index is null)
            return;
        var probed = JarInJarIndex
            .BuildOwnDependencies(mod, loader ?? string.Empty)
            .Where(dep => !DependencyIds.IsPlatform(dep.DepId) && !dep.Optional)
            .Select(dep => (Dep: dep, Status: _index.Analyze(mod, dep)))
            .ToList();
        var missing = probed
            .Where(pair => pair.Status == JijDepStatus.Missing)
            .Select(pair => pair.Dep.DepId ?? string.Empty)
            .ToList();
        var mismatch = probed
            .Where(pair => pair.Status == JijDepStatus.VersionMismatch)
            .Select(pair => Lang.Text(
                "Instance.Resource.Mod.JarInJar.Warning.VersionMismatch.Detail",
                pair.Dep.DepId,
                pair.Dep.Raw ?? "*",
                string.Join(", ", _index.GetProviderVersions(mod, pair.Dep.DepId ?? string.Empty))))
            .ToList();
        var disabled = probed
            .Where(pair => pair.Status == JijDepStatus.Disabled)
            .Select(pair => pair.Dep.DepId ?? string.Empty)
            .ToList();
        if (missing.Count == 0 && mismatch.Count == 0 && disabled.Count == 0)
            return;

        var name = parent is null
            ? DisplayName(mod)
            : $"{DisplayName(mod)} {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", DisplayName(parent))}";
        if (missing.Count > 0)
            WarningRows.Add(
                new JijNoticeRow
                {
                    Text = Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.Missing",
                        name,
                        string.Join(", ", missing)),
                    Kind = JijRowKind.Error
                });
        if (mismatch.Count > 0)
            WarningRows.Add(
                new JijNoticeRow
                {
                    Text = Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.VersionMismatch",
                        name,
                        string.Join(", ", mismatch)),
                    Kind = JijRowKind.Warn
                });
        if (disabled.Count > 0)
            WarningRows.Add(
                new JijNoticeRow
                {
                    Text = Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.Disabled",
                        name,
                        string.Join(", ", disabled)),
                    Kind = JijRowKind.Warn
                });
    }

    private bool Match(LocalComp.LocalCompFile mod)
    {
        var search = SearchText.Trim();
        if (search.Length == 0)
            return true;
        return Contains(DisplayName(mod), search)
            || Contains(mod.ModId, search)
            || Contains(mod.FileName, search);
    }

    private bool Contains(string? text)
    {
        return Contains(text, SearchText.Trim());
    }

    private static bool Contains(string? text, string search)
    {
        return !string.IsNullOrEmpty(text)
            && text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private bool AnyEmbeddedMatch(List<LocalComp.LocalCompFile>? embedded)
    {
        return embedded is not null
            && embedded.Any(entry => Match(entry) || AnyEmbeddedMatch(entry.EmbeddedMods));
    }

    private static string StatusLabel(JijDepStatus status)
    {
        return Lang.Text(
            status switch
            {
                JijDepStatus.Installed => "Instance.Resource.Mod.JarInJar.Dep.Installed",
                JijDepStatus.Disabled => "Instance.Resource.Mod.JarInJar.Dep.Disabled",
                JijDepStatus.Bundled => "Instance.Resource.Mod.JarInJar.Dep.Bundled",
                JijDepStatus.VersionMismatch =>
                    "Instance.Resource.Mod.JarInJar.Dep.VersionMismatch",
                _ => "Instance.Resource.Mod.JarInJar.Dep.Missing"
            });
    }

    private static string? CleanPlaceholder(string? value)
    {
        return string.IsNullOrEmpty(value) || value.Contains("${", StringComparison.Ordinal)
            ? null
            : value;
    }

    private static string DisplayName(LocalComp.LocalCompFile mod)
    {
        if (mod.Comp is not null)
        {
            var title = mod.Comp.GetControlTitle(false);
            return title.Key + title.Value;
        }

        return string.IsNullOrWhiteSpace(mod.Name) ? mod.FileName : mod.Name;
    }

    public void SetEmptyState(string title, string message)
    {
        IsLoading = false;
        ShowContent = false;
        ShowEmpty = true;
        EmptyTitle = title;
        EmptyMessage = message;
        StateChanged?.Invoke();
    }

    public void ShowLoading()
    {
        IsLoading = true;
        ShowContent = false;
        ShowEmpty = false;
        StateChanged?.Invoke();
    }

    public void UpdateOnly()
    {
        RebuildSections();
    }

    public void RefreshModList()
    {
        if (!IsCurrentPage)
            return;
        var output = LocalComp.CompResourceListLoader.output;
        if (output is null || !IsCurrentInstanceModOutput(output))
            return;
        BuildFromOutput(output);
    }
}

/// <summary>内嵌模组页行的显示严重级别。</summary>
public enum JijRowKind
{
    None,
    Ok,
    Warn,
    Error
}

public sealed class JijNoticeRow
{
    public required string Text { get; init; }
    public JijRowKind Kind { get; init; }
}

public sealed class JijConflictRow
{
    public required string Severity { get; init; }
    public JijRowKind SeverityKind { get; init; }
    public required string Pair { get; init; }
}

public sealed class JijDependencyRow
{
    public required string Primary { get; init; }
    public string Optional { get; init; } = string.Empty;
    public required string Status { get; init; }
    public JijRowKind StatusKind { get; init; }
    public bool UseThemeBrush { get; init; }
}

public partial class JijRelationRow : ObservableObject
{
    public required string Title { get; init; }
    public List<JijDependencyRow> Dependencies { get; } = [];

    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;
}

public partial class JijBundledRow : ObservableObject
{
    public required string Title { get; init; }
    public List<JijTreeRow> Rows { get; } = [];

    [ObservableProperty] public partial bool IsExpanded { get; set; } = true;
}

public sealed class JijTreeRow
{
    public int Depth { get; init; }
    public required string Name { get; init; }
    public string Version { get; init; } = string.Empty;
    public string Loader { get; init; } = string.Empty;
    public string McVersion { get; init; } = string.Empty;
    public string Versions { get; init; } = string.Empty;
    public string? VersionsTooltip { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsIncompatible { get; init; }
    public string CurrentText { get; init; } = string.Empty;
    public string IncompatibleText { get; init; } = string.Empty;
}
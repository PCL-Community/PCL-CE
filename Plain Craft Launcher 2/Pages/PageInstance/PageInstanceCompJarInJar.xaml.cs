using PCL.Core.App.Localization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CompFile = PCL.LocalComp.LocalCompFile;

namespace PCL;

public partial class PageInstanceCompJarInJar : IPageView
{
    private readonly PageInstanceCompJarInJarViewModel _vm = new();
    private JarInJarIndex _index = null!;
    private string? _instanceMc;
    private string _search = "";
    private bool _refreshed;

    public PageInstanceCompJarInJar()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
    }

    private void BtnBack_Click(object sender, MouseButtonEventArgs e) => GoBack();

    private void BtnEmptyBack_Click(object sender, MouseButtonEventArgs e) => GoBack();

    private void BtnRefresh_Click(object sender, MouseButtonEventArgs e) => RefreshList();

    private void SearchBox_TextChanged(object sender, EventArgs e)
    {
        _search = (SearchBox.Text).Trim();
        RefreshList();
    }

    public PageInstanceCompJarInJarViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        RefreshList();
    }

    private bool _Match(CompFile m)
    {
        if (_search.Length == 0) return true;
        return _Contains(_DisplayName(m)) || _Contains(m.ModId) || _Contains(m.FileName);
    }

    private bool _Contains(string? s) =>
        !string.IsNullOrEmpty(s) && s.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

    private bool _AnyEmbeddedMatch(List<CompFile>? embedded) =>
        embedded is not null && embedded.Any(e => _Match(e) || _AnyEmbeddedMatch(e.EmbeddedMods));

    private static void GoBack()
        => Launcher.frmInstanceLeft?.PageChange(FormMain.PageSubType.VersionMod);

    /// <summary>后台内嵌解析完成后刷新当前页。</summary>
    public void OnJijResolved()
    {
        if (IsVisible) RefreshList();
    }

    #region 列表构建

    private void RefreshList()
    {
        var output = LocalComp.CompResourceListLoader.output!;
        if (!_OutputBelongsToCurrentInstance(output))
        {
            GoBack();
            return;
        }

        var allMods = output
            .Where(m => !m.IsFolder && m.State != CompFile.LocalFileStatus.Unavailable)
            .ToList();
        _instanceMc = PageInstanceLeft.McInstance.Info.VanillaName;
        _index = new JarInJarIndex(allMods, _instanceMc);
        _vm.HideLoading();

        _vm.WarningRows.Clear();
        foreach (var host in allMods)
        {
            if (host.State != CompFile.LocalFileStatus.Fine) continue;
            if (_Match(host)) _AppendMissingWarning(host, null, host.DetectedLoader);
            foreach (var node in _index.GetLoadableEmbedded(host))
                if (_Match(node))
                    _AppendMissingWarning(node, host, node.JijLoader);
        }

        var hasWarning = _vm.WarningRows.Count > 0;

        _vm.ConflictRows.Clear();
        var conflictCount = 0;
        foreach (var (a, b, hard) in _index.FindActiveConflicts())
        {
            if (!_Match(a) && !_Match(b)) continue;
            _vm.ConflictRows.Add(_MakeConflictRow(a, b, hard));
            conflictCount++;
        }

        PanRelationList.Children.Clear();
        var relationCount = 0;
        foreach (var host in allMods)
        {
            var hostDeps = JarInJarIndex.BuildOwnDependencies(host, host.DetectedLoader)
                .Where(d => !DependencyIds.IsPlatform(d.DepId)).ToList();
            if (hostDeps.Count > 0 && (_Match(host) || hostDeps.Any(d => _Contains(d.DepId))))
            {
                PanRelationList.Children.Add(_MakeRelationCard(host, null, hostDeps));
                relationCount++;
            }

            foreach (var node in _index.GetLoadableEmbedded(host))
            {
                var nodeDeps = JarInJarIndex.BuildOwnDependencies(node, node.JijLoader)
                    .Where(d => !DependencyIds.IsPlatform(d.DepId)).ToList();
                if (nodeDeps.Count == 0) continue;
                if (!_Match(node) && !nodeDeps.Any(d => _Contains(d.DepId))) continue;
                PanRelationList.Children.Add(_MakeRelationCard(node, host, nodeDeps));
                relationCount++;
            }
        }

        PanBundledList.Children.Clear();
        var bundledMods = allMods
            .Where(m => m.EmbeddedMods is { Count: > 0 } && (_Match(m) || _AnyEmbeddedMatch(m.EmbeddedMods)))
            .ToList();
        foreach (var mod in bundledMods)
            PanBundledList.Children.Add(_MakeBundledCard(mod));

        // 搜索无结果时不显示"没有模组"空态（避免误导为实例本身没有），仅留空白
        _vm.ApplySectionVisibility(hasWarning, conflictCount, relationCount, bundledMods.Count,
            _search.Length > 0);
    }

    private MyCard _MakeCard(string title, IEnumerable<object> rows, string templateKey)
    {
        var card = new MyCard
        { Title = title, CanSwap = true, Margin = new Thickness(0, 0, 0, 10) };
        var content = new ItemsControl
        {
            Margin = new Thickness(20, MyCard.SwapedHeight, 18, 30),
            VerticalAlignment = VerticalAlignment.Top,
            ItemsSource = rows,
            ItemTemplate = (DataTemplate)FindResource(templateKey)
        };
        card.Children.Add(content);
        card.SwapControl = content;
        card.IsSwapped = true;
        return card;
    }

    private void _AppendMissingWarning(CompFile mod, CompFile? parent, string? loader)
    {
        var probed = JarInJarIndex.BuildOwnDependencies(mod, loader)
            .Where(d => !DependencyIds.IsPlatform(d.DepId) && !d.Optional)
            .Select(d => (d, status: _index.Analyze(mod, d)))
            .ToList();
        var missing = probed.Where(x => x.status == JijDepStatus.Missing).Select(x => x.d.DepId).ToList();
        var mismatch = probed.Where(x => x.status == JijDepStatus.VersionMismatch)
            .Select(x => Lang.Text("Instance.Resource.Mod.JarInJar.Warning.VersionMismatch.Detail",
                x.d.DepId, x.d.Raw ?? "*", string.Join(", ", _index.GetProviderVersions(mod, x.d.DepId))))
            .ToList();
        // 版本匹配的 provider 全部被禁用：运行时同样不可用，需提示（用户重新启用即可修复）
        var disabled = probed.Where(x => x.status == JijDepStatus.Disabled).Select(x => x.d.DepId).ToList();
        if (missing.Count == 0 && mismatch.Count == 0 && disabled.Count == 0) return;
        var name = parent is null
            ? _DisplayName(mod)
            : _DisplayName(mod) + " " +
              Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", _DisplayName(parent));
        if (missing.Count > 0)
            _vm.WarningRows.Add(new JijNoticeRow
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Missing", name, string.Join(", ", missing)),
                Foreground = BrushError
            });
        if (mismatch.Count > 0)
            _vm.WarningRows.Add(new JijNoticeRow
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Warning.VersionMismatch", name,
                    string.Join(", ", mismatch)),
                Foreground = BrushWarn
            });
        if (disabled.Count > 0)
            _vm.WarningRows.Add(new JijNoticeRow
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Disabled", name, string.Join(", ", disabled)),
                Foreground = BrushWarn
            });
    }

    private JijConflictRow _MakeConflictRow(CompFile a, CompFile b, bool hard) => new()
    {
        Severity = Lang.Text(hard
            ? "Instance.Resource.Mod.JarInJar.Conflict.Incompatible"
            : "Instance.Resource.Mod.JarInJar.Conflict.Discouraged"),
        SeverityBrush = hard ? BrushError : BrushWarn,
        Pair = "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Conflict.Pair", _DisplayName(a), _DisplayName(b))
    };

    private MyCard _MakeRelationCard(CompFile mod, CompFile? parent, List<JarInJarIndex.DepRow> deps)
    {
        var title = parent is null
            ? _DisplayName(mod)
            : _DisplayName(mod) + "  " +
              Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", _DisplayName(parent));
        return _MakeCard(title, deps.Select(dep => BuildDependencyRow(mod, dep)).ToList(), "JijDependencyRowTemplate");
    }

    private MyCard _MakeBundledCard(CompFile mod)
        => _MakeCard(_DisplayName(mod), _BuildBundledRows(mod), "JijTreeRowTemplate");

    private List<JijTreeRow> _BuildBundledRows(CompFile mod)
    {
        var selected = mod.State == CompFile.LocalFileStatus.Fine
            ? _index.GetLoadableEmbedded(mod).ToHashSet()
            : new HashSet<CompFile>();
        var rows = new List<JijTreeRow>();
        AppendTreeRows(rows, mod.EmbeddedMods, 0, selected);
        return rows;
    }

    private void AppendTreeRows(List<JijTreeRow> rows, List<CompFile> embedded, int depth, HashSet<CompFile> selected)
    {
        if (embedded is { Count: 0 }) return;

        var ungrouped = embedded.Where(e => string.IsNullOrEmpty(e.ModId)).ToList();
        foreach (var g in embedded.Where(e => !string.IsNullOrEmpty(e.ModId))
                     .GroupBy(e => e.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = g.ToList();
            if (versions.Count == 1)
            {
                rows.Add(BuildTreeRow(versions[0], depth, 0, McConstraintMatcher.MatchKind.None));
                AppendTreeRows(rows, versions[0].EmbeddedMods, depth + 1, selected);
                continue;
            }

            var scored = versions
                .Select(v => (v, kind: McConstraintMatcher.Match(v.FileName, v.Version, v.JijTargetMcVersion,
                    v.JijLoader, _instanceMc)))
                .OrderByDescending(x => (int)x.kind).ToList();
            var selectedRep = versions.FirstOrDefault(selected.Contains);
            var rep = selectedRep ?? scored[0].v;
            var kind = selectedRep is null
                ? scored[0].kind
                : McConstraintMatcher.Match(rep.FileName, rep.Version, rep.JijTargetMcVersion, rep.JijLoader,
                    _instanceMc);
            var tooltip = string.Join("\n",
                versions.Select(v => string.IsNullOrWhiteSpace(v.Version) ? v.FileName : v.Version));
            rows.Add(BuildTreeRow(rep, depth, versions.Count, kind, tooltip, selectedRep is not null));
            AppendTreeRows(rows, rep.EmbeddedMods, depth + 1, selected);
        }

        foreach (var e in ungrouped)
        {
            rows.Add(BuildTreeRow(e, depth, 0, McConstraintMatcher.MatchKind.None));
            AppendTreeRows(rows, e.EmbeddedMods, depth + 1, selected);
        }
    }

    private static JijTreeRow BuildTreeRow(CompFile e, int depth, int versionCount, McConstraintMatcher.MatchKind kind,
        string? versionsTooltip = null, bool selected = false)
    {
        var ver = _CleanPlaceholder(e.Version);
        var mc = _CleanPlaceholder(e.JijTargetMcVersion);
        var showVersions = versionCount > 1;
        var isCurrent = selected || kind is McConstraintMatcher.MatchKind.Exact or McConstraintMatcher.MatchKind.Range;
        var isIncompatible = !isCurrent && kind == McConstraintMatcher.MatchKind.Incompatible;
        return new JijTreeRow
        {
            Margin = new Thickness(8 + depth * 20, 2, 0, 2),
            Name = "• " + _DisplayName(e),
            Version = ver is null ? "" : "  (" + ver + ")",
            Loader = string.IsNullOrEmpty(e.JijLoader) ? "" : "  [" + e.JijLoader + "]",
            McVersion = mc is null ? "" : "  (MC " + mc + ")",
            Versions = showVersions
                ? "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Versions", versionCount)
                : "",
            VersionsTooltip = showVersions && !string.IsNullOrEmpty(versionsTooltip) ? versionsTooltip : null,
            Status = isCurrent
                ? "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Current")
                : isIncompatible
                    ? "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Incompatible")
                    : "",
            StatusBrush = isCurrent ? BrushOk : isIncompatible ? BrushWarn : null
        };
    }

    private JijDependencyRow BuildDependencyRow(CompFile mod, JarInJarIndex.DepRow dep)
    {
        var status = _index.Analyze(mod, dep);
        var optional = dep.Optional;
        var text = dep.Raw is null ? dep.DepId : dep.DepId + " " + dep.Raw;
        var label = "  (" + _StatusLabel(status) + ")";
        // 可选依赖缺失不算问题，用主题灰；其余：内嵌提供灰、已装/已禁/缺失为绿/橙/红语义色
        var useThemeBrush = optional || status == JijDepStatus.Bundled;
        return new JijDependencyRow
        {
            Primary = "• " + text,
            Optional = optional ? "  " + Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Optional") : "",
            GrayLabel = useThemeBrush ? label : "",
            ColorLabel = useThemeBrush ? "" : label,
            LabelBrush = useThemeBrush ? null : _StatusBrush(status)
        };
    }

    private static string _StatusLabel(JijDepStatus status) => Lang.Text(status switch
    {
        JijDepStatus.Installed => "Instance.Resource.Mod.JarInJar.Dep.Installed",
        JijDepStatus.Disabled => "Instance.Resource.Mod.JarInJar.Dep.Disabled",
        JijDepStatus.Bundled => "Instance.Resource.Mod.JarInJar.Dep.Bundled",
        JijDepStatus.VersionMismatch => "Instance.Resource.Mod.JarInJar.Dep.VersionMismatch",
        _ => "Instance.Resource.Mod.JarInJar.Dep.Missing"
    });

    private static Brush _StatusBrush(JijDepStatus status) => status switch
    {
        JijDepStatus.Installed => BrushOk,
        JijDepStatus.Disabled => BrushWarn,
        JijDepStatus.VersionMismatch => BrushWarn,
        _ => BrushError
    };

    #endregion

    #region 辅助

    // output 是否属于当前实例（mod 路径必在实例游戏目录 PathIndie 下）；空列表无从判断按匹配处理避免误弹
    private static bool _OutputBelongsToCurrentInstance(List<CompFile> output)
    {
        var inst = PageInstanceLeft.McInstance;
        if (output is { Count: 0 }) return false;
        try
        {
            var input = LocalComp.CompResourceListLoader.input;
            if (input is null || input.compType != Comp.CompType.Mod ||
                !string.Equals(Path.GetFullPath(input.gameVersion.PathInstance)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(inst.PathInstance)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            var first = output.FirstOrDefault(m => !string.IsNullOrEmpty(m.path));
            if (first is null) return true;
            var root = Path.GetFullPath(inst.PathIndie)
                           .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            return Path.GetFullPath(first.path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    // 未替换的版本占位符不显示。
    private static string? _CleanPlaceholder(string? v) => string.IsNullOrEmpty(v) || v.Contains("${") ? null : v;

    private static string _DisplayName(CompFile m)
    {
        if (m.Comp is not null)
        {
            var t = m.Comp.GetControlTitle(false);
            return t.Key + t.Value;
        }

        return string.IsNullOrWhiteSpace(m.Name) ? m.FileName : m.Name;
    }

    private static readonly Brush BrushOk = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly Brush BrushWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
    private static readonly Brush BrushError = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    #endregion
}

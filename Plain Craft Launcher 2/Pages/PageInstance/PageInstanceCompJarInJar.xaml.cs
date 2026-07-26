using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;
using CompFile = PCL.ModLocalComp.LocalCompFile;

namespace PCL;

public partial class PageInstanceCompJarInJar
{
    private ModJarInJarIndex _index;
    private string _instanceMc;
    private string _search = "";

    public PageInstanceCompJarInJar()
    {
        InitializeComponent();
        BtnBack.Click += (_, _) => GoBack();
        BtnEmptyBack.Click += (_, _) => GoBack();
        BtnRefresh.Click += (_, _) => RefreshList();
        SearchBox.TextChanged += (_, _) =>
        {
            _search = (SearchBox.Text ?? "").Trim();
            RefreshList();
        };
        Loaded += (_, _) => RefreshList();
    }

    private bool _Match(CompFile m)
    {
        if (_search.Length == 0) return true;
        return _Contains(_DisplayName(m)) || _Contains(m.ModId) || _Contains(m.FileName);
    }

    private bool _Contains(string s) =>
        !string.IsNullOrEmpty(s) && s.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;

    private bool _AnyEmbeddedMatch(List<CompFile> embedded) =>
        embedded is not null && embedded.Any(e => _Match(e) || _AnyEmbeddedMatch(e.EmbeddedMods));

    private IEnumerable<CompFile> _DistinctEmbedded(CompFile host)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in _index.GetLoadableEmbedded(host))
            if (string.IsNullOrEmpty(n.ModId) || seen.Add(n.ModId))
                yield return n;
    }

    private static void GoBack()
        => ModMain.frmInstanceLeft?.PageChange(FormMain.PageSubType.VersionMod);

    #region 列表构建

    private void RefreshList()
    {
        var output = ModLocalComp.compResourceListLoader.output;
        if (!_OutputBelongsToCurrentInstance(output))
        {
            GoBack();
            return;
        }

        var allMods = output.Where(m => !m.IsFolder).ToList();
        _instanceMc = PageInstanceLeft.McInstance?.Info?.VanillaName;
        _index = new ModJarInJarIndex(allMods, _instanceMc);
        PanLoad.Visibility = Visibility.Collapsed;

        PanWarnList.Children.Clear();
        foreach (var host in allMods)
        {
            if (host.State != CompFile.LocalFileStatus.Fine) continue; // 禁用的 mod 不加载，其缺失前置不算问题
            if (_Match(host)) _AppendMissingWarning(host, null, host.DetectedLoader);
            foreach (var node in _DistinctEmbedded(host))
                if (_Match(node))
                    _AppendMissingWarning(node, host, node.JijLoader);
        }

        var hasWarning = PanWarnList.Children.Count > 0;
        CardWarnings.Visibility = hasWarning ? Visibility.Visible : Visibility.Collapsed;

        PanRelationList.Children.Clear();
        var relationCount = 0;
        foreach (var host in allMods)
        {
            var hostDeps = ModJarInJarIndex.BuildOwnDependencies(host, host.DetectedLoader)
                .Where(d => !ModJarInJarIndex.IsPlatform(d.DepId)).ToList();
            if (hostDeps.Count > 0 && (_Match(host) || hostDeps.Any(d => _Contains(d.DepId))))
            {
                PanRelationList.Children.Add(_MakeRelationCard(host, null, hostDeps));
                relationCount++;
            }

            foreach (var node in _DistinctEmbedded(host))
            {
                var nodeDeps = ModJarInJarIndex.BuildOwnDependencies(node, node.JijLoader)
                    .Where(d => !ModJarInJarIndex.IsPlatform(d.DepId)).ToList();
                if (nodeDeps.Count == 0) continue;
                if (!_Match(node) && !nodeDeps.Any(d => _Contains(d.DepId))) continue;
                PanRelationList.Children.Add(_MakeRelationCard(node, host, nodeDeps));
                relationCount++;
            }
        }

        SectionRelations.Visibility = relationCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        PanBundledList.Children.Clear();
        var bundledMods = allMods
            .Where(m => m.EmbeddedMods is { Count: > 0 } && (_Match(m) || _AnyEmbeddedMatch(m.EmbeddedMods)))
            .ToList();
        foreach (var mod in bundledMods)
            PanBundledList.Children.Add(_MakeCard(mod, _BuildBundledContent));
        SectionBundled.Visibility = bundledMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 搜索无结果时不显示"没有模组"空态（避免误导为实例本身没有），仅留空白
        PanEmpty.Visibility = hasWarning || relationCount > 0 || bundledMods.Count > 0 || _search.Length > 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private MyCard _MakeCard(CompFile mod, Action<StackPanel, CompFile> build)
    {
        var card = new MyCard { Title = _DisplayName(mod), CanSwap = true, Margin = new Thickness(0, 0, 0, 10) };
        var stack = new StackPanel
        {
            Margin = new Thickness(20, MyCard.SwapedHeight, 18, 12),
            VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0, 0),
            Tag = mod // StackInstall 靠 Tag 非空触发一次性懒加载
        };
        card.Children.Add(stack);
        card.SwapControl = stack;
        card.InstallMethod = s => build(s, mod);
        card.IsSwapped = true;
        return card;
    }

    private void _AppendMissingWarning(CompFile mod, CompFile parent, string loader)
    {
        var probed = ModJarInJarIndex.BuildOwnDependencies(mod, loader)
            .Where(d => !ModJarInJarIndex.IsPlatform(d.DepId) && !d.Optional)
            .Select(d => (d, status: _index.Analyze(mod, d)))
            .ToList();
        var missing = probed.Where(x => x.status == JijDepStatus.Missing).Select(x => x.d.DepId).ToList();
        var mismatch = probed.Where(x => x.status == JijDepStatus.VersionMismatch)
            .Select(x => x.d.Raw is null ? x.d.DepId : x.d.DepId + " " + x.d.Raw).ToList();
        if (missing.Count == 0 && mismatch.Count == 0) return;
        var name = parent is null
            ? _DisplayName(mod)
            : _DisplayName(mod) + " " +
              Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", _DisplayName(parent));
        if (missing.Count > 0)
            PanWarnList.Children.Add(_Text(
                Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Missing", name, string.Join(", ", missing)),
                _BrushError));
        if (mismatch.Count > 0)
            PanWarnList.Children.Add(_Text(
                Lang.Text("Instance.Resource.Mod.JarInJar.Warning.VersionMismatch", name, string.Join(", ", mismatch)),
                _BrushWarn));
    }

    private MyCard _MakeRelationCard(CompFile mod, CompFile parent, List<ModJarInJarIndex.DepRow> deps)
    {
        var title = parent is null
            ? _DisplayName(mod)
            : _DisplayName(mod) + "  " +
              Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", _DisplayName(parent));
        var card = new MyCard { Title = title, CanSwap = true, Margin = new Thickness(0, 0, 0, 10) };
        var stack = new StackPanel
        {
            Margin = new Thickness(20, MyCard.SwapedHeight, 18, 12),
            VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0, 0),
            Tag = mod
        };
        card.Children.Add(stack);
        card.SwapControl = stack;
        card.InstallMethod = s =>
        {
            foreach (var dep in deps) s.Children.Add(BuildDependencyRow(mod, dep));
        };
        card.IsSwapped = true;
        return card;
    }

    // 内嵌模组：仅展示内嵌树（禁用/删除请在模组列表进行，级联在那里统一处理）
    private void _BuildBundledContent(StackPanel stack, CompFile mod)
    {
        AppendTreeRows(stack, mod.EmbeddedMods, 0);
    }

    private void AppendTreeRows(StackPanel stack, List<CompFile> embedded, int depth)
    {
        if (embedded is null) return;

        var ungrouped = embedded.Where(e => string.IsNullOrEmpty(e.ModId)).ToList();
        foreach (var g in embedded.Where(e => !string.IsNullOrEmpty(e.ModId))
                     .GroupBy(e => e.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = g.ToList();
            if (versions.Count == 1)
            {
                stack.Children.Add(BuildTreeRow(versions[0], depth, 0, McConstraintMatcher.MatchKind.None));
                AppendTreeRows(stack, versions[0].EmbeddedMods, depth + 1);
                continue;
            }

            var scored = versions
                .Select(v => (v, kind: McConstraintMatcher.Match(v.FileName, v.Version, v.JijTargetMcVersion,
                    v.JijLoader, _instanceMc)))
                .OrderByDescending(x => (int)x.kind).ToList();
            var rep = scored[0].v;
            var tooltip = string.Join("\n",
                versions.Select(v => string.IsNullOrWhiteSpace(v.Version) ? v.FileName : v.Version));
            stack.Children.Add(BuildTreeRow(rep, depth, versions.Count, scored[0].kind, tooltip));
            AppendTreeRows(stack, rep.EmbeddedMods, depth + 1);
        }

        foreach (var e in ungrouped)
        {
            stack.Children.Add(BuildTreeRow(e, depth, 0, McConstraintMatcher.MatchKind.None));
            AppendTreeRows(stack, e.EmbeddedMods, depth + 1);
        }
    }

    private Panel BuildTreeRow(CompFile e, int depth, int versionCount, McConstraintMatcher.MatchKind kind,
        string versionsTooltip = null)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(8 + depth * 20, 2, 0, 2)
        };
        row.Children.Add(_TextRes("• " + _DisplayName(e), "ColorBrush1", true));
        var ver = _CleanPlaceholder(e.Version);
        if (!string.IsNullOrEmpty(ver))
            row.Children.Add(_TextRes("  (" + ver + ")", "ColorBrush2"));
        if (!string.IsNullOrEmpty(e.JijLoader))
            row.Children.Add(_TextRes("  [" + e.JijLoader + "]", "ColorBrush3"));
        var mc = _CleanPlaceholder(e.JijTargetMcVersion);
        if (!string.IsNullOrEmpty(mc))
            row.Children.Add(_TextRes("  (MC " + mc + ")", "ColorBrushInfo"));

        if (versionCount > 1)
        {
            var badge = _TextRes("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Versions", versionCount),
                "ColorBrush2");
            if (!string.IsNullOrEmpty(versionsTooltip)) badge.ToolTip = versionsTooltip;
            row.Children.Add(badge);
        }

        if (kind is McConstraintMatcher.MatchKind.Exact or McConstraintMatcher.MatchKind.Range)
            row.Children.Add(_Text("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Current"), _BrushOk));
        else if (kind == McConstraintMatcher.MatchKind.Incompatible)
            row.Children.Add(_Text("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Incompatible"),
                _BrushWarn));

        return row;
    }

    private Panel BuildDependencyRow(CompFile mod, ModJarInJarIndex.DepRow dep)
    {
        var status = _index.Analyze(mod, dep);
        var optional = dep.Optional;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 0, 2) };
        var text = dep.Raw is null ? dep.DepId : dep.DepId + " " + dep.Raw;
        row.Children.Add(_TextRes("• " + text, "ColorBrush1"));
        if (optional)
            row.Children.Add(_TextRes("  " + Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Optional"), "ColorBrush2"));
        var label = "  (" + _StatusLabel(status) + ")";
        // 可选依赖缺失不算问题，用主题灰；其余：内嵌提供灰、已装/已禁/缺失为绿/橙/红语义色
        if (optional || status == JijDepStatus.Bundled)
            row.Children.Add(_TextRes(label, "ColorBrush2"));
        else
            row.Children.Add(_Text(label, _StatusBrush(status)));
        return row;
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
        JijDepStatus.Installed => _BrushOk,
        JijDepStatus.Disabled => _BrushWarn,
        JijDepStatus.VersionMismatch => _BrushWarn,
        _ => _BrushError
    };

    #endregion

    #region 辅助

    // output 是否属于当前实例（mod 路径必在实例游戏目录 PathIndie 下）；空列表无从判断按匹配处理避免误弹
    private static bool _OutputBelongsToCurrentInstance(List<CompFile> output)
    {
        var inst = PageInstanceLeft.McInstance;
        if (inst is null || output is null) return false;
        var first = output.FirstOrDefault(m => !string.IsNullOrEmpty(m.path));
        return first is null || first.path.StartsWith(inst.PathIndie, StringComparison.OrdinalIgnoreCase);
    }

    // 与模组列表卡片一致的名称显示：有在线工程信息时用 译名 | 原名，否则回退本地名/文件名
    // 未替换的版本占位符（Forge ${file.jarVersion}、Fabric ${version} 等）不显示原文
    private static string _CleanPlaceholder(string v) => string.IsNullOrEmpty(v) || v.Contains("${") ? null : v;

    private static string _DisplayName(CompFile m)
    {
        if (m.Comp is not null)
        {
            var t = m.Comp.GetControlTitle(false);
            return t.Key + t.Value;
        }

        return string.IsNullOrWhiteSpace(m.Name) ? m.FileName : m.Name;
    }

    private static readonly Brush _BrushOk = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly Brush _BrushWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
    private static readonly Brush _BrushError = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    private static TextBlock _Text(string text, Brush brush, bool bold = false) => new()
    {
        Text = text, Foreground = brush, VerticalAlignment = VerticalAlignment.Center,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock _TextRes(string text, string resourceKey, bool bold = false)
    {
        var tb = new TextBlock
        {
            Text = text, VerticalAlignment = VerticalAlignment.Center,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
        return tb;
    }

    #endregion
}

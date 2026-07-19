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

    public PageInstanceCompJarInJar()
    {
        InitializeComponent();
        BtnBack.Click += (_, _) => GoBack();
        BtnEmptyBack.Click += (_, _) => GoBack();
        BtnRefresh.Click += (_, _) => RefreshList();
        Loaded += (_, _) => RefreshList();
    }

    private static void GoBack()
        => ModMain.frmInstanceLeft?.PageChange(FormMain.PageSubType.VersionMod);

    #region 列表构建

    private void RefreshList()
    {
        var allMods = ModLocalComp.compResourceListLoader.output.Where(m => !m.IsFolder).ToList();
        _index = new ModJarInJarIndex(allMods, PageInstanceLeft.McInstance?.Info?.VanillaName);
        PanLoad.Visibility = Visibility.Collapsed;

        PanWarnList.Children.Clear();
        foreach (var mod in allMods)
        {
            var missing = mod.Dependencies.Keys
                .Where(k => !ModJarInJarIndex.IsPlatform(k) && _index.Analyze(mod, k) == JijDepStatus.Missing)
                .ToList();
            if (missing.Count > 0)
                PanWarnList.Children.Add(_Text(
                    Lang.Text("Instance.Resource.Mod.JarInJar.Warning.Missing", _DisplayName(mod),
                        string.Join(", ", missing)), _BrushError));
        }

        var hasWarning = PanWarnList.Children.Count > 0;
        CardWarnings.Visibility = hasWarning ? Visibility.Visible : Visibility.Collapsed;

        PanRelationList.Children.Clear();
        var relationMods = allMods
            .Where(m => m.Dependencies.Keys.Any(k => !ModJarInJarIndex.IsPlatform(k))).ToList();
        foreach (var mod in relationMods)
            PanRelationList.Children.Add(_MakeCard(mod, _BuildRelationContent));
        SectionRelations.Visibility = relationMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        PanBundledList.Children.Clear();
        var bundledMods = allMods.Where(m => m.EmbeddedMods is { Count: > 0 }).ToList();
        foreach (var mod in bundledMods)
            PanBundledList.Children.Add(_MakeCard(mod, _BuildBundledContent));
        SectionBundled.Visibility = bundledMods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        PanEmpty.Visibility = hasWarning || relationMods.Count > 0 || bundledMods.Count > 0
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

    private void _BuildRelationContent(StackPanel stack, CompFile mod)
    {
        var deps = mod.Dependencies.Keys.Where(k => !ModJarInJarIndex.IsPlatform(k)).ToList();
        foreach (var dep in deps)
            stack.Children.Add(BuildDependencyRow(mod, dep, mod.Dependencies[dep]));
    }

    // 内嵌模组：仅展示内嵌树（禁用/删除请在模组列表进行，级联在那里统一处理）
    private void _BuildBundledContent(StackPanel stack, CompFile mod)
    {
        AppendTreeRows(stack, mod.EmbeddedMods, 0);
    }

    private void AppendTreeRows(StackPanel stack, List<CompFile> embedded, int depth)
    {
        if (embedded is null) return;
        foreach (var e in embedded)
        {
            stack.Children.Add(BuildTreeRow(e, depth));
            AppendTreeRows(stack, e.EmbeddedMods, depth + 1);
        }
    }

    private Panel BuildTreeRow(CompFile e, int depth)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(8 + depth * 20, 2, 0, 2)
        };
        row.Children.Add(_Text("• " + _DisplayName(e), _BrushMain, true));
        if (!string.IsNullOrEmpty(e.Version))
            row.Children.Add(_Text("  " + e.Version, _BrushGray));
        if (!string.IsNullOrEmpty(e.JijLoader))
            row.Children.Add(_Text("  [" + e.JijLoader + "]", _BrushGray));
        if (!string.IsNullOrEmpty(e.JijTargetMcVersion))
            row.Children.Add(_Text("  MC " + e.JijTargetMcVersion, _BrushGray));
        return row;
    }

    private Panel BuildDependencyRow(CompFile mod, string depId, string versionReq)
    {
        var (label, brush) = _StatusText(_index.Analyze(mod, depId));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 0, 2) };
        var text = versionReq is null ? depId : depId + " " + versionReq;
        row.Children.Add(_Text("• " + text, _BrushMain));
        row.Children.Add(_Text("  (" + label + ")", brush));
        return row;
    }

    private static (string, Brush) _StatusText(JijDepStatus status) => status switch
    {
        JijDepStatus.Installed => (Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Installed"), _BrushOk),
        JijDepStatus.Disabled => (Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Disabled"), _BrushWarn),
        JijDepStatus.Bundled => (Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Bundled"), _BrushGray),
        _ => (Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Missing"), _BrushError)
    };

    #endregion

    #region 辅助

    // 与模组列表卡片一致的名称显示：有在线工程信息时用 译名 | 原名，否则回退本地名/文件名
    private static string _DisplayName(CompFile m)
    {
        if (m.Comp is not null)
        {
            var t = m.Comp.GetControlTitle(false);
            return t.Key + t.Value;
        }

        return string.IsNullOrWhiteSpace(m.Name) ? m.FileName : m.Name;
    }

    private static readonly Brush _BrushMain = (Brush)Application.Current.Resources["ColorBrush1"];
    private static readonly Brush _BrushGray = (Brush)Application.Current.Resources["ColorBrush2"];
    private static readonly Brush _BrushOk = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly Brush _BrushWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
    private static readonly Brush _BrushError = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    private static TextBlock _Text(string text, Brush brush, bool bold = false) => new()
    {
        Text = text, Foreground = brush, VerticalAlignment = VerticalAlignment.Center,
        FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap
    };

    #endregion
}

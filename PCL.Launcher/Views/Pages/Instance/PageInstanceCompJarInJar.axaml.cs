using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;
using CompFile = PCL.LocalComp.LocalCompFile;
using Path = System.IO.Path;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     内嵌依赖（Jar-in-Jar）页：读取资源列表加载器解析出的模组内嵌副本，
///     展示依赖缺失/版本不匹配/禁用警告、内嵌副本之间的冲突、依赖关系与内嵌模组树。
/// </summary>
public class PageInstanceCompJarInJar : MyPageRight, ICompLocalPage
{
    private static readonly IBrush BrushOk = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
    private static readonly IBrush BrushWarn = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

    private static readonly IBrush
        BrushError = new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));

    private List<CompFile> _allMods = [];
    private MyCard _cardConflicts = null!;
    private MyCard _cardWarnings = null!;
    private JarInJarIndex? _index;
    private McInstance? _instance;
    private string _instanceMc = string.Empty;
    private MyLoading _load = null!;
    private Loading.LoaderBase.OnStateChangedUiEventHandler? _loaderStateHandler;
    private StackPanel _panBundledList = null!;
    private StackPanel _panConflictList = null!;

    private StackPanel _panContent = null!;
    private MyCard _panEmpty = null!;
    private MyCard _panLoad = null!;
    private StackPanel _panRelationList = null!;
    private StackPanel _panWarnList = null!;
    private MySearchBox _searchBox = null!;
    private string _searchText = string.Empty;
    private StackPanel _sectionBundled = null!;
    private StackPanel _sectionRelations = null!;
    private MyLoadingStateSimulator? _simulator;
    private TextBlock _txtEmptyMessage = null!;
    private TextBlock _txtEmptyTitle = null!;

    public PageInstanceCompJarInJar()
    {
        BuildUi();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        Reload();
    }

    public override void OnNavigatedFrom()
    {
        DetachLoaderHandler();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var root = new Grid();
        var panBack = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _panContent = new StackPanel
        {
            Margin = new Thickness(25d, 10d, 25d, 10d)
        };

        var manageCard = new MyCard
        {
            Margin = new Thickness(0d, 15d, 0d, 15d)
        };
        var manageBody = new WrapPanel
        {
            Margin = new Thickness(15d, 8d, 0d, 15d)
        };
        var back = NewButton("Instance.Resource.BackToParent", true);
        back.Click += (_, _) => GoBack();
        manageBody.Children.Add(back);
        var refresh = NewButton("Common.Action.Refresh", false);
        refresh.Click += (_, _) => Reload();
        manageBody.Children.Add(refresh);
        manageBody.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Message"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0d, 7d, 15d, 0d),
                Opacity = 0.7d
            });
        manageCard.BorderChild = manageBody;
        _panContent.Children.Add(manageCard);

        _searchBox = new MySearchBox
        {
            HintText = Lang.Text("Instance.Resource.Mod.JarInJar.Search.Hint"),
            Margin = new Thickness(0d, 0d, 0d, 15d),
            Text = _searchText
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _searchText = (_searchBox.Text ?? string.Empty).Trim();
            RebuildList();
        };
        _panContent.Children.Add(_searchBox);

        _panWarnList = new StackPanel
        {
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        _cardWarnings = CreateSectionCard(
            Lang.Text("Instance.Resource.Mod.JarInJar.Section.Warnings"),
            _panWarnList,
            null);
        _panContent.Children.Add(_cardWarnings);

        _panConflictList = new StackPanel
        {
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        _cardConflicts = CreateSectionCard(
            Lang.Text("Instance.Resource.Mod.JarInJar.Section.Conflicts"),
            _panConflictList,
            Lang.Text("Instance.Resource.Mod.JarInJar.Conflict.Tooltip"));
        _panContent.Children.Add(_cardConflicts);

        _sectionRelations = new StackPanel
        {
            IsVisible = false
        };
        _sectionRelations.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Section.Relations"),
                FontSize = 16d,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(2d, 0d, 0d, 8d)
            });
        _panRelationList = new StackPanel();
        _sectionRelations.Children.Add(_panRelationList);
        _panContent.Children.Add(_sectionRelations);

        _sectionBundled = new StackPanel
        {
            IsVisible = false
        };
        _sectionBundled.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Instance.Resource.Mod.JarInJar.Section.Bundled"),
                FontSize = 16d,
                FontWeight = FontWeight.Bold,
                Margin = new Thickness(2d, 4d, 0d, 8d)
            });
        _panBundledList = new StackPanel();
        _sectionBundled.Children.Add(_panBundledList);
        _panContent.Children.Add(_sectionBundled);

        panBack.Content = _panContent;
        root.Children.Add(panBack);

        _txtEmptyTitle = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(_txtEmptyTitle, TextBlock.ForegroundProperty, "ColorBrush3");
        _txtEmptyMessage = new TextBlock
        {
            Text = Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        var emptyBack = new MyButton
        {
            Text = Lang.Text("Instance.Resource.BackToParent"),
            Height = 35d,
            MinWidth = 130d,
            Padding = new Thickness(12d, 0d),
            Margin = new Thickness(5d, 20d, 5d, 5d),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButton.ColorState.Highlight
        };
        emptyBack.Click += (_, _) => GoBack();
        var emptyBody = new StackPanel
        {
            Margin = new Thickness(20d, 17d)
        };
        emptyBody.Children.Add(_txtEmptyTitle);
        emptyBody.Children.Add(divider);
        emptyBody.Children.Add(_txtEmptyMessage);
        emptyBody.Children.Add(emptyBack);
        _panEmpty = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false,
            BorderChild = emptyBody
        };
        root.Children.Add(_panEmpty);

        _load = new MyLoading
        {
            Text = Lang.Text("Instance.Resource.Loading"),
            Margin = new Thickness(20d, 20d, 20d, 17d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _panLoad = new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d, 0d),
            IsVisible = false,
            BorderChild = _load
        };
        root.Children.Add(_panLoad);

        PanScroll = panBack;
        Child = root;
    }

    private void GoBack()
    {
        PageRouter.Navigate(
            new PageStackData(
                PageType.InstanceSetup,
                PageSubType.VersionMod,
                _instance ?? PageInstanceLeft.ResolveInstance()));
    }

    private static MyButton NewButton(string textKey, bool highlight)
    {
        return new MyButton
        {
            Text = Lang.Text(textKey),
            MinWidth = 110d,
            Padding = new Thickness(13d, 7d),
            Margin = new Thickness(0d, 7d, 15d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = highlight ? MyButton.ColorState.Highlight : MyButton.ColorState.Normal
        };
    }

    private static MyCard CreateSectionCard(string title, Control content, string? tooltip)
    {
        var card = new MyCard
        {
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        var body = new StackPanel
        {
            Margin = new Thickness(15d, 10d, 15d, 12d)
        };
        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 16d,
            FontWeight = FontWeight.Bold
        };
        ContainerTheme.BindDynamic(titleBlock, TextBlock.ForegroundProperty, "ColorBrush3");
        if (!string.IsNullOrEmpty(tooltip))
            ToolTip.SetTip(titleBlock, tooltip);
        body.Children.Add(titleBlock);
        body.Children.Add(content);
        card.BorderChild = body;
        return card;
    }

    #region 数据与列表

    public void Reload()
    {
        _instance ??= PageInstanceLeft.ResolveInstance();
        if (_instance is null)
        {
            ShowEmpty(
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"));
            return;
        }

        var output = LocalComp.CompResourceListLoader.output;
        if (output is null || !IsCurrentInstanceModOutput(output))
        {
            StartLoader();
            return;
        }

        BuildFromOutput(output);
    }

    private void StartLoader()
    {
        if (_instance is null)
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
                        ShowEmpty(
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

        var folder = GetModFolder(_instance);
        var started = Loading.LoaderFolderRun(
            LocalComp.CompResourceListLoader,
            folder,
            Loading.LoaderFolderRunType.ForceRun,
            loaderInput: new LocalComp.CompLocalLoaderData
            {
                gameVersion = _instance,
                compType = Comp.CompType.Mod,
                compPath = folder + Path.DirectorySeparatorChar,
                loaders = LocalComp.GetCurrentVersionModLoader(),
                frm = this
            });
        if (!started && LocalComp.CompResourceListLoader.State == LoadState.Finished)
        {
            var output = LocalComp.CompResourceListLoader.output;
            if (output is not null)
                BuildFromOutput(output);
        }
    }

    private void DetachLoaderHandler()
    {
        if (_loaderStateHandler is null)
            return;
        LocalComp.CompResourceListLoader.OnStateChangedUi -= _loaderStateHandler;
        _loaderStateHandler = null;
    }

    private static string GetModFolder(McInstance instance)
    {
        return instance.PathIndie
            + (instance.Info.HasLabyMod
                ? Path.Combine("labymod-neo", "fabric", instance.Info.VanillaName)
                : string.Empty)
            + LocalComp.GetPathNameByCompType(Comp.CompType.Mod);
    }

    private bool IsCurrentInstanceModOutput(List<CompFile> output)
    {
        if (_instance is null || output.Count == 0)
            return false;
        var input = LocalComp.CompResourceListLoader.input;
        if (input is null || input.compType != Comp.CompType.Mod || input.gameVersion is null)
            return false;

        try
        {
            var instancePath = Path
                .GetFullPath(_instance.PathInstance)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var loaderPath = Path
                .GetFullPath(input.gameVersion.PathInstance)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(instancePath, loaderPath, StringComparison.OrdinalIgnoreCase))
                return false;
            var root = Path
                    .GetFullPath(_instance.PathIndie)
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

    private void BuildFromOutput(List<CompFile> output)
    {
        if (_instance is null)
            return;
        var allMods = output
            .Where(entry => !entry.IsFolder && entry.State != CompFile.LocalFileStatus.Unavailable)
            .ToList();
        if (allMods.Count == 0)
        {
            ShowEmpty(
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Title"),
                Lang.Text("Instance.Resource.Mod.JarInJar.Empty.Message"));
            return;
        }

        _instanceMc = _instance.Info.VanillaName;
        _index = new JarInJarIndex(allMods, _instanceMc);
        _allMods = allMods;
        HideOverlays();
        RebuildList();
    }

    private void RebuildList()
    {
        if (_index is null)
            return;

        _panWarnList.Children.Clear();
        foreach (var host in _allMods)
        {
            if (host.State != CompFile.LocalFileStatus.Fine)
                continue;
            if (Match(host))
                AppendMissingWarning(host, null, host.DetectedLoader);
            foreach (var node in _index.GetLoadableEmbedded(host))
                if (Match(node))
                    AppendMissingWarning(node, host, node.JijLoader);
        }

        _cardWarnings.IsVisible = _panWarnList.Children.Count > 0;

        _panConflictList.Children.Clear();
        foreach (var (a, b, hard) in _index.FindActiveConflicts())
        {
            if (!Match(a) && !Match(b))
                continue;
            _panConflictList.Children.Add(MakeConflictRow(a, b, hard));
        }

        _cardConflicts.IsVisible = _panConflictList.Children.Count > 0;

        _panRelationList.Children.Clear();
        foreach (var host in _allMods)
        {
            var hostDeps = JarInJarIndex
                .BuildOwnDependencies(host, host.DetectedLoader ?? string.Empty)
                .Where(dep => !DependencyIds.IsPlatform(dep.DepId))
                .ToList();
            if (hostDeps.Count > 0 && (Match(host) || hostDeps.Any(dep => Contains(dep.DepId))))
                _panRelationList.Children.Add(MakeRelationCard(host, null, hostDeps));

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
                _panRelationList.Children.Add(MakeRelationCard(node, host, nodeDeps));
            }
        }

        _sectionRelations.IsVisible = _panRelationList.Children.Count > 0;

        _panBundledList.Children.Clear();
        foreach (var mod in _allMods.Where(entry => entry.EmbeddedMods is { Count: > 0 }
                     && (Match(entry) || AnyEmbeddedMatch(entry.EmbeddedMods))))
            _panBundledList.Children.Add(MakeBundledCard(mod));
        _sectionBundled.IsVisible = _panBundledList.Children.Count > 0;

        _panContent.IsVisible = true;
    }

    private bool Match(CompFile mod)
    {
        if (_searchText.Length == 0)
            return true;
        return Contains(DisplayName(mod)) || Contains(mod.ModId) || Contains(mod.FileName);
    }

    private bool Contains(string? text)
    {
        return !string.IsNullOrEmpty(text)
            && text.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
    }

    private bool AnyEmbeddedMatch(List<CompFile>? embedded)
    {
        return embedded is not null
            && embedded.Any(entry => Match(entry) || AnyEmbeddedMatch(entry.EmbeddedMods));
    }

    private void AppendMissingWarning(CompFile mod, CompFile? parent, string? loader)
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
            _panWarnList.Children.Add(
                MakeText(
                    Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.Missing",
                        name,
                        string.Join(", ", missing)),
                    BrushError));
        if (mismatch.Count > 0)
            _panWarnList.Children.Add(
                MakeText(
                    Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.VersionMismatch",
                        name,
                        string.Join(", ", mismatch)),
                    BrushWarn));
        if (disabled.Count > 0)
            _panWarnList.Children.Add(
                MakeText(
                    Lang.Text(
                        "Instance.Resource.Mod.JarInJar.Warning.Disabled",
                        name,
                        string.Join(", ", disabled)),
                    BrushWarn));
    }

    private static TextBlock MakeText(string text, IBrush? foreground)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (foreground is not null)
            block.Foreground = foreground;
        return block;
    }

    private Control MakeConflictRow(CompFile a, CompFile b, bool hard)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 2d, 0d, 2d)
        };
        panel.Children.Add(
            MakeText(
                Lang.Text(
                    hard
                        ? "Instance.Resource.Mod.JarInJar.Conflict.Incompatible"
                        : "Instance.Resource.Mod.JarInJar.Conflict.Discouraged"),
                hard ? BrushError : BrushWarn));
        panel.Children.Add(
            MakeText(
                $"  {Lang.Text("Instance.Resource.Mod.JarInJar.Conflict.Pair", DisplayName(a), DisplayName(b))}",
                null));
        return panel;
    }

    private MyCard MakeRelationCard(CompFile mod, CompFile? parent, List<JarInJarIndex.DepRow> deps)
    {
        var title = parent is null
            ? DisplayName(mod)
            : $"{DisplayName(mod)}  {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.From", DisplayName(parent))}";
        var body = new StackPanel();
        foreach (var dep in deps)
            body.Children.Add(MakeDependencyRow(mod, dep));
        return CreateCollapsibleCard(title, body);
    }

    private static MyCard CreateCollapsibleCard(string title, Control content)
    {
        var card = new MyCard
        {
            Title = title,
            CanSwap = true,
            Margin = new Thickness(0d, 0d, 0d, 10d)
        };
        content.Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 24d);
        card.Children.Add(content);
        card.SwapControl = content;
        card.IsSwapped = true;
        return card;
    }

    private Control MakeDependencyRow(CompFile mod, JarInJarIndex.DepRow dep)
    {
        if (_index is null)
            return new StackPanel();
        var status = _index.Analyze(mod, dep);
        var optional = dep.Optional;
        var text = dep.Raw is null ? dep.DepId ?? string.Empty : $"{dep.DepId} {dep.Raw}";
        var label = $"  ({StatusLabel(status)})";
        var useThemeBrush = optional || status == JijDepStatus.Bundled;

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8d, 2d, 0d, 2d)
        };
        panel.Children.Add(MakeText($"• {text}", null));
        if (optional)
            panel.Children.Add(
                MakeText(
                    $"  {Lang.Text("Instance.Resource.Mod.JarInJar.Dep.Optional")}",
                    null));
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (useThemeBrush)
            ContainerTheme.BindDynamic(labelBlock, TextBlock.ForegroundProperty, "ColorBrush2");
        else
            labelBlock.Foreground = StatusBrush(status);
        panel.Children.Add(labelBlock);
        return panel;
    }

    private string StatusLabel(JijDepStatus status)
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

    private static IBrush StatusBrush(JijDepStatus status)
    {
        return status switch
        {
            JijDepStatus.Installed => BrushOk,
            JijDepStatus.Disabled => BrushWarn,
            JijDepStatus.VersionMismatch => BrushWarn,
            _ => BrushError
        };
    }

    private MyCard MakeBundledCard(CompFile mod)
    {
        var body = new StackPanel();
        if (_index is null)
            return CreateCollapsibleCard(DisplayName(mod), body);
        var selected = mod.State == CompFile.LocalFileStatus.Fine
            ? _index.GetLoadableEmbedded(mod).ToHashSet()
            : [];
        AppendTreeRows(body, mod.EmbeddedMods, 0, selected);
        return CreateCollapsibleCard(DisplayName(mod), body);
    }

    private void AppendTreeRows(
        StackPanel panel,
        List<CompFile>? embedded,
        int depth,
        HashSet<CompFile> selected)
    {
        if (embedded is null || embedded.Count == 0)
            return;

        foreach (var ungrouped in embedded.Where(entry => string.IsNullOrEmpty(entry.ModId)))
        {
            panel.Children.Add(
                MakeTreeRow(ungrouped, depth, 0, McConstraintMatcher.MatchKind.None, null, false));
            AppendTreeRows(panel, ungrouped.EmbeddedMods, depth + 1, selected);
        }

        foreach (var group in embedded
                     .Where(entry => !string.IsNullOrEmpty(entry.ModId))
                     .GroupBy(entry => entry.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group.ToList();
            if (versions.Count == 1)
            {
                panel.Children.Add(
                    MakeTreeRow(
                        versions[0],
                        depth,
                        0,
                        McConstraintMatcher.MatchKind.None,
                        null,
                        false));
                AppendTreeRows(panel, versions[0].EmbeddedMods, depth + 1, selected);
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
            panel.Children.Add(
                MakeTreeRow(
                    representative,
                    depth,
                    versions.Count,
                    kind,
                    tooltip,
                    selectedRepresentative is not null));
            AppendTreeRows(panel, representative.EmbeddedMods, depth + 1, selected);
        }
    }

    private Control MakeTreeRow(
        CompFile entry,
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

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8d + depth * 20d, 2d, 0d, 2d)
        };
        var name = MakeText($"• {DisplayName(entry)}", null);
        name.FontWeight = FontWeight.Bold;
        panel.Children.Add(name);
        if (version is not null)
            panel.Children.Add(MakeText($"  ({version})", null));
        if (!string.IsNullOrEmpty(entry.JijLoader))
            panel.Children.Add(MakeText($"  [{entry.JijLoader}]", null));
        if (mc is not null)
            panel.Children.Add(MakeText($"  (MC {mc})", null));
        if (showVersions)
        {
            var versionsText = MakeText(
                $"  {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Versions", versionCount)}",
                null);
            if (!string.IsNullOrEmpty(versionsTooltip))
                ToolTip.SetTip(versionsText, versionsTooltip);
            panel.Children.Add(versionsText);
        }

        if (isCurrent)
            panel.Children.Add(
                MakeText(
                    $"  {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Current")}",
                    BrushOk));
        else if (isIncompatible)
            panel.Children.Add(
                MakeText(
                    $"  {Lang.Text("Instance.Resource.Mod.JarInJar.Bundled.Incompatible")}",
                    BrushWarn));
        return panel;
    }

    private static string? CleanPlaceholder(string? value)
    {
        return string.IsNullOrEmpty(value) || value.Contains("${", StringComparison.Ordinal)
            ? null
            : value;
    }

    private static string DisplayName(CompFile mod)
    {
        if (mod.Comp is not null)
        {
            var title = mod.Comp.GetControlTitle(false);
            return title.Key + title.Value;
        }

        return string.IsNullOrWhiteSpace(mod.Name) ? mod.FileName : mod.Name;
    }

    #endregion

    #region 状态切换

    private void ShowEmpty(string title, string message)
    {
        HideOverlays();
        _txtEmptyTitle.Text = title;
        _txtEmptyMessage.Text = message;
        _panEmpty.IsVisible = true;
        _panContent.IsVisible = false;
    }

    private void ShowLoading()
    {
        _panContent.IsVisible = false;
        _panEmpty.IsVisible = false;
        _panLoad.IsVisible = true;
        _simulator = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        _load.State = _simulator;
    }

    private void HideOverlays()
    {
        _panEmpty.IsVisible = false;
        _panLoad.IsVisible = false;
        _simulator?.LoadingState = LoadingState.Stop;
    }

    #endregion

    #region ICompLocalPage

    public bool LoadProgressVisible
    {
        get => _load.ShowProgress;
        set => _load.ShowProgress = value;
    }

    public void SetLoadText(string key)
    {
        _load.Text = Lang.Text(key);
    }

    public void UpdateOnly()
    {
        RebuildList();
    }

    public string? CurrentFolderPath => null;

    public void RefreshModList()
    {
        if (!ReferenceEquals(PageRouter.CurrentPage, this))
            return;
        var output = LocalComp.CompResourceListLoader.output;
        if (output is null || !IsCurrentInstanceModOutput(output))
            return;
        BuildFromOutput(output);
    }

    #endregion
}
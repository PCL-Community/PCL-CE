using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

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

    private MyCard _cardConflicts = null!;
    private MyCard _cardWarnings = null!;
    private MyLoading _load = null!;
    private StackPanel _panBundledList = null!;
    private StackPanel _panConflictList = null!;
    private StackPanel _panContent = null!;
    private MyCard _panEmpty = null!;
    private MyCard _panLoad = null!;
    private StackPanel _panRelationList = null!;
    private StackPanel _panWarnList = null!;
    private MySearchBox _searchBox = null!;
    private StackPanel _sectionBundled = null!;
    private StackPanel _sectionRelations = null!;
    private MyLoadingStateSimulator? _simulator;
    private TextBlock _txtEmptyMessage = null!;
    private TextBlock _txtEmptyTitle = null!;

    public PageInstanceCompJarInJar()
    {
        BuildUi();
        ViewModel.StateChanged += ApplyState;
    }

    public PageInstanceCompJarInJarViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        ViewModel.IsCurrentPage = true;
        Reload();
    }

    public override void OnNavigatedFrom()
    {
        ViewModel.IsCurrentPage = false;
        ViewModel.DetachLoaderHandler();
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
            Text = ViewModel.SearchText
        };
        _searchBox.TextChanged += (_, _) =>
        {
            ViewModel.SearchText = (_searchBox.Text ?? string.Empty).Trim();
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
                ViewModel.Instance ?? PageInstanceLeft.ResolveInstance()));
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
        ViewModel.Reload(this);
    }

    private void ApplyState()
    {
        if (ViewModel.IsLoading)
        {
            _panContent.IsVisible = false;
            _panEmpty.IsVisible = false;
            _panLoad.IsVisible = true;
            if (_simulator is null)
                _simulator = new MyLoadingStateSimulator
                {
                    LoadingState = LoadingState.Run
                };
            else
                _simulator.LoadingState = LoadingState.Run;

            _load.State = _simulator;
            return;
        }

        if (ViewModel.ShowEmpty)
        {
            _panEmpty.IsVisible = true;
            _panContent.IsVisible = false;
            _panLoad.IsVisible = false;
            _simulator?.LoadingState = LoadingState.Stop;
            _txtEmptyTitle.Text = ViewModel.EmptyTitle;
            _txtEmptyMessage.Text = ViewModel.EmptyMessage;
            return;
        }

        _panEmpty.IsVisible = false;
        _panLoad.IsVisible = false;
        _simulator?.LoadingState = LoadingState.Stop;
        _panContent.IsVisible = ViewModel.ShowContent;
        if (!ViewModel.ShowContent)
            return;

        _panWarnList.Children.Clear();
        foreach (var row in ViewModel.WarningRows)
            _panWarnList.Children.Add(MakeText(row.Text, BrushForKind(row.Kind)));
        _cardWarnings.IsVisible = ViewModel.CardWarningsVisible;

        _panConflictList.Children.Clear();
        foreach (var row in ViewModel.ConflictRows)
            _panConflictList.Children.Add(MakeConflictRow(row));
        _cardConflicts.IsVisible = ViewModel.CardConflictsVisible;

        _panRelationList.Children.Clear();
        foreach (var row in ViewModel.RelationRows)
            _panRelationList.Children.Add(MakeRelationCard(row));
        _sectionRelations.IsVisible = ViewModel.SectionRelationsVisible;

        _panBundledList.Children.Clear();
        foreach (var row in ViewModel.BundledRows)
            _panBundledList.Children.Add(MakeBundledCard(row));
        _sectionBundled.IsVisible = ViewModel.SectionBundledVisible;
    }

    private static IBrush? BrushForKind(JijRowKind kind)
    {
        return kind switch
        {
            JijRowKind.Ok => BrushOk,
            JijRowKind.Warn => BrushWarn,
            JijRowKind.Error => BrushError,
            _ => null
        };
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

    private static Control MakeConflictRow(JijConflictRow row)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 2d, 0d, 2d)
        };
        panel.Children.Add(MakeText(row.Severity, BrushForKind(row.SeverityKind)));
        panel.Children.Add(MakeText($"  {row.Pair}", null));
        return panel;
    }

    private static MyCard MakeRelationCard(JijRelationRow row)
    {
        var body = new StackPanel();
        foreach (var dep in row.Dependencies)
            body.Children.Add(MakeDependencyRow(dep));
        var card = CreateCollapsibleCard(row.Title, body, row.IsExpanded);
        card.Swap += (_, _) => row.IsExpanded = card.IsSwapped;
        return card;
    }

    private static MyCard CreateCollapsibleCard(string title, Control content, bool isExpanded)
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
        card.IsSwapped = isExpanded;
        return card;
    }

    private static Control MakeDependencyRow(JijDependencyRow dep)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8d, 2d, 0d, 2d)
        };
        panel.Children.Add(MakeText($"• {dep.Primary}", null));
        if (!string.IsNullOrEmpty(dep.Optional))
            panel.Children.Add(MakeText($"  {dep.Optional}", null));

        var label = MakeText($"  ({dep.Status})", null);
        if (dep.UseThemeBrush)
            ContainerTheme.BindDynamic(label, TextBlock.ForegroundProperty, "ColorBrush2");
        else
            label.Foreground = BrushForKind(dep.StatusKind);
        panel.Children.Add(label);
        return panel;
    }

    private static MyCard MakeBundledCard(JijBundledRow row)
    {
        var body = new StackPanel();
        foreach (var treeRow in row.Rows)
            body.Children.Add(MakeTreeRow(treeRow));
        var card = CreateCollapsibleCard(row.Title, body, row.IsExpanded);
        card.Swap += (_, _) => row.IsExpanded = card.IsSwapped;
        return card;
    }

    private static Control MakeTreeRow(JijTreeRow row)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8d + row.Depth * 20d, 2d, 0d, 2d)
        };
        var name = MakeText($"• {row.Name}", null);
        name.FontWeight = FontWeight.Bold;
        panel.Children.Add(name);
        if (!string.IsNullOrEmpty(row.Version))
            panel.Children.Add(MakeText($"  ({row.Version})", null));
        if (!string.IsNullOrEmpty(row.Loader))
            panel.Children.Add(MakeText($"  [{row.Loader}]", null));
        if (!string.IsNullOrEmpty(row.McVersion))
            panel.Children.Add(MakeText($"  (MC {row.McVersion})", null));
        if (!string.IsNullOrEmpty(row.Versions))
        {
            var versionsText = MakeText($"  {row.Versions}", null);
            if (!string.IsNullOrEmpty(row.VersionsTooltip))
                ToolTip.SetTip(versionsText, row.VersionsTooltip);
            panel.Children.Add(versionsText);
        }

        if (!string.IsNullOrEmpty(row.CurrentText))
            panel.Children.Add(MakeText($"  {row.CurrentText}", BrushOk));
        else if (!string.IsNullOrEmpty(row.IncompatibleText))
            panel.Children.Add(MakeText($"  {row.IncompatibleText}", BrushWarn));
        return panel;
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
        ViewModel.UpdateOnly();
    }

    public string? CurrentFolderPath => null;

    public void RefreshModList()
    {
        ViewModel.IsCurrentPage = ReferenceEquals(PageRouter.CurrentPage, this);
        ViewModel.RefreshModList();
    }

    #endregion
}
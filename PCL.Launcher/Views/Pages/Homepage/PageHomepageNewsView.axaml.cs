using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Core.App.Localization;
using PCL.Core.Model.Tools.News;
using PCL.Core.ViewModel.Homepage;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Homepage;

/// <summary>
///     Minecraft 信息流视图：从官方新闻接口读取标题、作者、发布日期、配图与摘要，以卡片流展示。
///     数据由 <see cref="NewsViewModel" /> 提供，点击“查看详情”打开对应网页（仅允许官方域名）。
/// </summary>
public partial class PageHomepageNewsView : UserControl
{
    private const double EstimatedCardHeight = 360d;
    private readonly TextBlock _labError;
    private readonly StackPanel _panError;
    private readonly MyCard _panLoad;
    private readonly WaterfallPanel _panNews;
    private readonly NewsViewModel _viewModel;
    private bool _isLocalizationHooked;
    private bool _rebuildPending;
    private bool _reloaded;
    private ScrollViewer? _scrollViewer;

    public PageHomepageNewsView()
    {
        InitializeComponent();
        _panNews = this.FindControl<WaterfallPanel>("PanNews")!;
        _panError = this.FindControl<StackPanel>("PanError")!;
        _labError = this.FindControl<TextBlock>("LabError")!;
        _panLoad = this.FindControl<MyCard>("PanLoad")!;
        this.FindControl<MyTextButton>("BtnRetry")!.Click += (_, _) => Reload();

        _viewModel = new NewsViewModel();
        _viewModel.NewsItems.CollectionChanged += (_, _) => ScheduleRebuild();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(NewsViewModel.IsLoading)
                or nameof(NewsViewModel.ErrorMessage))
                Dispatcher.UIThread.Post(RefreshState);
        };

        AttachedToVisualTree += (_, _) =>
        {
            if (!_isLocalizationHooked)
            {
                _isLocalizationHooked = true;
                LocalizationService.LanguageChanged += OnLanguageChanged;
            }

            _scrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (_scrollViewer is not null)
                _scrollViewer.ScrollChanged += OnScrollChanged;
            if (_reloaded) return;
            _reloaded = true;
            RefreshState();
            ScheduleRebuild();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_scrollViewer is not null)
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer = null;
        };
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _panNews.InvalidateMeasure();
    }

    public void Reload()
    {
        _reloaded = true;
        if (!_viewModel.IsLoading)
        {
            _viewModel.NewsItems.Clear();
            _viewModel.ErrorMessage = null;
            _viewModel._LoadDataCommand.Execute(null);
        }

        RefreshState();
    }

    private void OnLanguageChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Rebuild);
            return;
        }

        Rebuild();
    }

    private void RefreshState()
    {
        var error = _viewModel.ErrorMessage;
        _panLoad.IsVisible = _viewModel.IsLoading;
        _panError.IsVisible = !string.IsNullOrEmpty(error);
        _labError.Text = error ?? "";
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
            return;
        _rebuildPending = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _rebuildPending = false;
                Rebuild();
            },
            DispatcherPriority.Background);
    }

    private void Rebuild()
    {
        _panNews.Children.Clear();
        foreach (var item in _viewModel.NewsItems)
        {
            var newsItem = item;
            _panNews.Children.Add(
                new MyVirtualizingElement<MyCard>(() => CreateNewsCard(newsItem))
                {
                    Height = EstimatedCardHeight
                });
        }

        RefreshState();
    }

    private IBrush? FindBrush(string key)
    {
        return this.TryFindResource(key, out var value) ? value as IBrush : null;
    }

    private MyCard CreateNewsCard(NewsItem item)
    {
        var card = new MyCard();

        var grid = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto")
        };

        var image = new MyImage
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Stretch = Stretch.UniformToFill,
            CornerRadius = new CornerRadius(6),
            Source = item.Image
        };
        if (!string.IsNullOrEmpty(item.ImageAltText))
            ToolTip.SetTip(image, item.ImageAltText);
        grid.Children.Add(image);

        var titleBorder = new Border
        {
            Background = FindBrush("ColorBrushBackground"),
            ClipToBounds = true,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 3, 5, 3),
            Margin = new Thickness(0, 5, 0, 0),
            Child = new TextBlock
            {
                Text = item.Title,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = FindBrush("ColorBrush1"),
                TextWrapping = TextWrapping.Wrap
            }
        };
        Grid.SetRow(titleBorder, 1);
        grid.Children.Add(titleBorder);

        var meta = new Grid
        {
            Margin = new Thickness(0, 5, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        var author = new TextBlock
        {
            Text = item.Author ?? "",
            Foreground = FindBrush("ColorBrushGray2")
        };
        var date = new TextBlock
        {
            Text = item.PublishDate.ToString("g", LocalizationService.CurrentFormatCulture),
            Foreground = FindBrush("ColorBrushGray2")
        };
        Grid.SetColumn(date, 1);
        meta.Children.Add(author);
        meta.Children.Add(date);
        Grid.SetRow(meta, 2);
        grid.Children.Add(meta);

        var description = new TextBlock
        {
            Text = item.Description,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = FindBrush("ColorBrushGray4")
        };
        Grid.SetRow(description, 3);
        grid.Children.Add(description);

        var readMore = new MyTextButton
        {
            Text = Lang.Text("Launch.Homepage.News.ViewDetail"),
            Margin = new Thickness(0, 5, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = FindBrush("ColorBrushGray2")
        };
        readMore.Click += (_, _) => _viewModel._OpenReadCommand.Execute(item.Url);
        Grid.SetRow(readMore, 4);
        grid.Children.Add(readMore);

        card.Children.Add(grid);
        return card;
    }
}
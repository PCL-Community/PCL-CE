using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
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
    private readonly TextBlock _labError;
    private readonly MyCard _panLoad;
    private readonly WaterfallPanel _panNews;
    private readonly NewsViewModel _viewModel;
    private bool _rebuildPending;
    private bool _reloaded;

    public PageHomepageNewsView()
    {
        InitializeComponent();
        _panNews = this.FindControl<WaterfallPanel>("PanNews")!;
        _labError = this.FindControl<TextBlock>("LabError")!;
        _panLoad = this.FindControl<MyCard>("PanLoad")!;

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
            if (_reloaded) return;
            _reloaded = true;
            RefreshState();
            ScheduleRebuild();
        };
    }

    public void Reload()
    {
        _reloaded = true;
        _viewModel._LoadDataCommand.Execute(null);
        RefreshState();
    }

    private void RefreshState()
    {
        var isLoading = _viewModel.IsLoading;
        _panLoad.IsVisible = isLoading;
        _labError.Text = _viewModel.ErrorMessage ?? "";
        _labError.IsVisible = !string.IsNullOrEmpty(_viewModel.ErrorMessage);
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
            _panNews.Children.Add(CreateNewsCard(item));
        RefreshState();
    }

    private IBrush? FindBrush(string key)
    {
        return this.TryFindResource(key, out var value) ? value as IBrush : null;
    }

    private Control CreateNewsCard(NewsItem item)
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
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 3, 5, 3),
            Margin = new Thickness(0, 5, 0, 0),
            Child = new TextBlock
            {
                Text = item.Title,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
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
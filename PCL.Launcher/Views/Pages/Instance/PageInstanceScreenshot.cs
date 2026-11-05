using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例截图页：以 MyCard 图片墙展示 <c>screenshots</c> 目录中的截图缩略图，
///     滚动到底部时按批追加；每张卡片提供打开、复制路径与删除。删除优先移入系统回收站。
/// </summary>
public class PageInstanceScreenshot : MyPageRight, IRefreshable
{
    private const int AppendCount = 20;
    private readonly MyScrollViewer _panBack;
    private readonly StackPanel _panContent;
    private readonly MyCard _panEmpty;
    private readonly WrapPanel _panList;
    private readonly MyCard _panQuickActions;

    public PageInstanceScreenshot()
    {
        var root = new Grid();
        _panBack = new MyScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        _panContent = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10d, 25d, 10d, 10d)
        };
        _panQuickActions = new MyCard
        {
            Title = Lang.Text("Instance.Screenshot.QuickActions"),
            Margin = new Thickness(15d, 0d, 15d, 15d)
        };
        _panList = new WrapPanel
        {
            Margin = new Thickness(8d, 0d, 8d, 25d),
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Horizontal
        };
        _panContent.Children.Add(_panQuickActions);
        _panContent.Children.Add(_panList);

        _panEmpty = CreateEmptyCard();
        _panBack.Content = new Grid
        {
            Children =
            {
                _panContent,
                _panEmpty
            }
        };
        root.Children.Add(_panBack);
        PanScroll = _panBack;
        Child = root;
        _panBack.ScrollChanged += (_, _) => RequireAppend();
        BuildUi();
    }

    public PageInstanceScreenshotViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        ViewModel.Refresh();
        RebuildList();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    public void Reload()
    {
        ViewModel.Reload();
        RebuildList();
    }

    private void RebuildList()
    {
        _panList.Children.Clear();
        UpdateEmptyState();
        if (ViewModel.HasScreenshots)
            _ = AppendAsync(AppendCount);
    }

    private void BuildUi()
    {
        var body = new Grid
        {
            Height = 35d,
            Margin = new Thickness(25d, 40d, 15d, 20d)
        };
        var openFolder = new MyButton
        {
            Text = Lang.Text("Instance.Screenshot.OpenFolder"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = MyButton.ColorState.Highlight
        };
        openFolder.Click += (_, _) => ViewModel.OpenFolder();
        body.Children.Add(openFolder);
        _panQuickActions.BorderChild = body;
        _panEmpty.BorderChild = CreateEmptyBody();
    }

    private Grid CreateEmptyBody()
    {
        var grid = new Grid
        {
            Margin = new Thickness(20d, 17d),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*")
        };
        var title = new TextBlock
        {
            Text = Lang.Text("Instance.Screenshot.Empty.Title"),
            FontSize = 19d,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 0d, 0d, 9d)
        };
        ContainerTheme.BindDynamic(title, TextBlock.ForegroundProperty, "ColorBrush3");
        Grid.SetColumnSpan(title, 3);
        grid.Children.Add(title);
        var divider = new Rectangle
        {
            Height = 2d,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        ContainerTheme.BindDynamic(divider, Shape.FillProperty, "ColorBrush3");
        Grid.SetRow(divider, 1);
        Grid.SetColumnSpan(divider, 3);
        grid.Children.Add(divider);
        var message = new TextBlock
        {
            Text = Lang.Text("Instance.Screenshot.Empty.Message"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10d, 15d, 10d, 5d)
        };
        Grid.SetRow(message, 2);
        Grid.SetColumnSpan(message, 3);
        grid.Children.Add(message);
        var openFolder = new MyButton
        {
            Text = Lang.Text("Instance.Screenshot.OpenFolder"),
            Height = 35d,
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(10d, 10d, 10d, 0d),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButton.ColorState.Highlight
        };
        openFolder.Click += (_, _) => ViewModel.OpenFolder();
        Grid.SetRow(openFolder, 3);
        Grid.SetColumn(openFolder, 1);
        grid.Children.Add(openFolder);
        return grid;
    }

    private static MyCard CreateEmptyCard()
    {
        return new MyCard
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(40d),
            IsVisible = false
        };
    }

    private void UpdateEmptyState()
    {
        var hasContent = ViewModel.HasScreenshots;
        _panContent.IsVisible = hasContent;
        _panEmpty.IsVisible = !hasContent;
    }

    private void RequireAppend()
    {
        if (!ViewModel.HasScreenshots)
            return;
        if (_panBack.Offset.Y + _panBack.Viewport.Height < _panBack.Extent.Height - 40d)
            return;
        _ = AppendAsync(AppendCount);
    }

    private async Task AppendAsync(int count)
    {
        foreach (var item in ViewModel.AppendNext(count))
            _panList.Children.Add(await CreateScreenshotCardAsync(item));
    }

    private async Task<MyCard> CreateScreenshotCardAsync(ScreenshotItemViewModel item)
    {
        var card = new MyCard
        {
            Width = 150d,
            Margin = new Thickness(7d),
            Tag = item.FilePath
        };
        ToolTip.SetTip(card, item.DisplayName);
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("9,120,Auto")
        };
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(4d, 0d)
        };
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(item.FilePath);
                return Bitmap.DecodeToWidth(stream, 400);
            });
            image.Source = bitmap;
            var ratio = bitmap.PixelSize.Height <= 0
                ? 1d
                : (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;
            card.Width = Math.Clamp(120d * ratio + 16d, 120d, 420d);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"加载截图缩略图失败：{item.FilePath}", LauncherLogLevel.Developer);
        }

        image.PointerPressed += (_, _) =>
        {
            ViewModel.SelectedScreenshot = item;
            item.IsSelected = true;
            PageInstanceScreenshotViewModel.OpenScreenshot(item);
        };
        Grid.SetRow(image, 1);
        grid.Children.Add(image);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(3d, 5d, 3d, 5d)
        };
        Grid.SetRow(buttons, 2);
        var open = CreateActionButton("Common.Action.Open", "lucide/folder-open");
        open.Click += (_, _) => PageInstanceScreenshotViewModel.OpenScreenshot(item);
        buttons.Children.Add(open);
        var delete = CreateActionButton("Common.Action.Delete", "lucide/trash-2");
        delete.Click += (_, _) => _ = DeleteAsync(item, card);
        buttons.Children.Add(delete);
        var copy = CreateActionButton("Common.Action.Copy", "lucide/copy");
        copy.Click += (_, _) => _ = CopyAsync(item);
        buttons.Children.Add(copy);
        grid.Children.Add(buttons);
        card.BorderChild = grid;
        card.Opacity = 0d;
        Animations.AniStart(
            new[]
            {
                Animations.AaOpacity(card, 1d, 200)
            });
        return card;
    }

    private static MyIconTextButton CreateActionButton(string textKey, string icon)
    {
        return new MyIconTextButton
        {
            Text = Lang.Text(textKey),
            SvgIcon = icon,
            LogoScale = 0.8d,
            Margin = new Thickness(2d, 0d)
        };
    }

    private Task CopyAsync(ScreenshotItemViewModel item)
    {
        return PageInstanceScreenshotViewModel.CopyAsync(
            item,
            TopLevel.GetTopLevel(this)?.Clipboard);
    }

    private async Task DeleteAsync(ScreenshotItemViewModel item, MyCard card)
    {
        if (!await ViewModel.DeleteAsync(item))
            return;
        _panList.Children.Remove(card);
        UpdateEmptyState();
    }
}
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例截图页：以 MyCard 图片墙展示 <c>screenshots</c> 目录中的截图缩略图，
///     滚动到底部时按批追加；每张卡片提供打开、复制路径与删除。删除优先移入系统回收站。
/// </summary>
public partial class PageInstanceScreenshot : MyPageRight, IRefreshable
{
    private const int AppendCount = 20;

    public PageInstanceScreenshot()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;

        BtnOpenFolder.Click += (_, _) => ViewModel.OpenFolder();
        BtnOpenFolderTop.Click += (_, _) => ViewModel.OpenFolder();
        PanBack.ScrollChanged += (_, _) => RequireAppend();
        PanLoad.PropertyChanged += (_, change) =>
        {
            if (change.Property == IsVisibleProperty)
                Load.State.LoadingState = PanLoad.IsVisible ? LoadingState.Run : LoadingState.Stop;
        };
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
        PanBack.ScrollToHome();
        Reload();
    }

    protected override void RefreshLocalization()
    {
        Reload();
    }

    public void Reload()
    {
        ViewModel.Reload();
        RebuildList();
    }

    private void RebuildList()
    {
        PanScreenshots.Children.Clear();
        if (ViewModel.HasScreenshots)
            AppendBatch(AppendCount);
    }

    private void RequireAppend()
    {
        if (!ViewModel.HasScreenshots)
            return;
        if (PanBack.Offset.Y + PanBack.Viewport.Height < PanBack.Extent.Height - 40d)
            return;
        AppendBatch(AppendCount);
    }

    private void AppendBatch(int count)
    {
        foreach (var item in ViewModel.AppendNext(count))
        {
            var placeholder = new MyVirtualizingElement<MyCard>(() => CreateScreenshotCard(item))
            {
                Width = 150d,
                Height = 170d,
                RecycleOffscreen = true
            };
            placeholder.Recycled += control =>
            {
                if (control is MyCard card)
                    UnloadScreenshotCard(card);
            };
            PanScreenshots.Children.Add(placeholder);
        }
    }

    private MyCard CreateScreenshotCard(ScreenshotItemViewModel item)
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
        _ = LoadScreenshotImageAsync(card, image, item);

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

    private static async Task LoadScreenshotImageAsync(
        MyCard card,
        Image image,
        ScreenshotItemViewModel item)
    {
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(item.FilePath);
                return Bitmap.DecodeToWidth(stream, 400);
            });

            if (card.Tag is not string path || path != item.FilePath)
            {
                bitmap.Dispose();
                return;
            }

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
    }

    private static void UnloadScreenshotCard(MyCard card)
    {
        card.Tag = null;
        foreach (var image in card.GetVisualDescendants().OfType<Image>())
        {
            if (image.Source is IDisposable disposable)
                disposable.Dispose();
            image.Source = null;
        }
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
        PanScreenshots.Children.Remove(card);
    }
}
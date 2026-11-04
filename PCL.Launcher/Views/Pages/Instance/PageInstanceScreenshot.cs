using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例截图页：以 MyCard 图片墙展示 <c>screenshots</c> 目录中的截图缩略图，
///     滚动到底部时按批追加；每张卡片提供打开、复制路径与删除。删除优先移入系统回收站。
/// </summary>
public class PageInstanceScreenshot : MyPageRight, IRefreshable
{
    private static readonly string[] AllowedSuffix = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];

    private const int AppendCount = 20;

    private readonly StackPanel _panContent;
    private readonly WrapPanel _panList;
    private readonly MyCard _panEmpty;
    private readonly MyCard _panQuickActions;
    private readonly List<string> _fileList = [];
    private MyScrollViewer _panBack = null!;
    private McInstance? _instance;
    private string _screenshotPath = string.Empty;
    private int _appendOffset;
    private bool _appendLock;

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
        _panBack.Content = new Grid { Children = { _panContent, _panEmpty } };
        root.Children.Add(_panBack);
        PanScroll = _panBack;
        Child = root;
        _panBack.ScrollChanged += (_, _) => RequireAppend();
        BuildUi();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _instance = InstancePageHelper.ResolveInstance(parameter);
        _screenshotPath = _instance is null ? string.Empty : InstancePageHelper.ScreenshotsPath(_instance);
        if (!string.IsNullOrEmpty(_screenshotPath))
            InstancePageHelper.EnsureDirectory(_screenshotPath);
        Reload();
    }

    protected override void RefreshLocalization()
    {
        BuildUi();
        Reload();
    }

    public void Refresh()
    {
        Reload();
    }

    public void Reload()
    {
        _fileList.Clear();
        _appendOffset = 0;
        _appendLock = false;
        _panList.Children.Clear();
        if (string.IsNullOrEmpty(_screenshotPath))
        {
            UpdateEmptyState();
            return;
        }

        try
        {
            if (Directory.Exists(_screenshotPath))
                _fileList.AddRange(
                    AllowedSuffix
                        .SelectMany(suffix => Directory.EnumerateFiles(_screenshotPath, suffix, SearchOption.TopDirectoryOnly))
                        .OrderByDescending(File.GetCreationTime));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
        }

        UpdateEmptyState();
        if (_fileList.Count == 0)
            return;
        _ = AppendAsync(AppendCount);
    }

    private void BuildUi()
    {
        var body = new Grid { Height = 35d, Margin = new Thickness(25d, 40d, 15d, 20d) };
        var openFolder = new MyButton
        {
            Text = Lang.Text("Instance.Screenshot.OpenFolder"),
            MinWidth = 140d,
            Padding = new Thickness(13d, 0d),
            Margin = new Thickness(0d, 0d, 20d, 0d),
            HorizontalAlignment = HorizontalAlignment.Left,
            ColorType = MyButton.ColorState.Highlight
        };
        openFolder.Click += (_, _) => OpenFolder();
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
        var divider = new Avalonia.Controls.Shapes.Rectangle { Height = 2d, HorizontalAlignment = HorizontalAlignment.Stretch };
        ContainerTheme.BindDynamic(divider, Avalonia.Controls.Shapes.Shape.FillProperty, "ColorBrush3");
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
        openFolder.Click += (_, _) => OpenFolder();
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
        var hasContent = _fileList.Count > 0;
        _panContent.IsVisible = hasContent;
        _panEmpty.IsVisible = !hasContent;
    }

    private void RequireAppend()
    {
        if (_fileList.Count == 0 || _appendLock)
            return;
        if (_panBack.Offset.Y + _panBack.Viewport.Height < _panBack.Extent.Height - 40d)
            return;
        _ = AppendAsync(AppendCount);
    }

    private async Task AppendAsync(int count)
    {
        if (_appendLock || _appendOffset * count > _fileList.Count)
            return;
        _appendLock = true;
        try
        {
            var start = _appendOffset * count;
            _appendOffset++;
            for (var i = start; i < Math.Min(start + count, _fileList.Count); i++)
            {
                var path = _fileList[i];
                try
                {
                    if (!File.Exists(path))
                        continue;
                    if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
                        continue;
                    if (new FileInfo(path).Length == 0L)
                        continue;
                    _panList.Children.Add(await CreateScreenshotCardAsync(path));
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(ex, "创建截图预览失败：" + path, LauncherLogLevel.Developer);
                }
            }
        }
        finally
        {
            _appendLock = false;
        }
    }

    private async Task<MyCard> CreateScreenshotCardAsync(string path)
    {
        var card = new MyCard
        {
            Width = 150d,
            Height = 200d,
            Margin = new Thickness(7d),
            Tag = path
        };
        ToolTip.SetTip(card, path.Replace(_screenshotPath, string.Empty));
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("9,120,*")
        };
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            Cursor = new Cursor(StandardCursorType.Hand),
            Margin = new Thickness(4d, 0d)
        };
        try
        {
            image.Source = await Task.Run(
                () =>
                {
                    using var stream = File.OpenRead(path);
                    return (Bitmap)Bitmap.DecodeToWidth(stream, 400);
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "加载截图缩略图失败：" + path, LauncherLogLevel.Developer);
        }

        image.PointerPressed += (_, _) =>
        {
            try
            {
                Basics.OpenPath(path);
            }
            catch (Exception ex)
            {
                InstancePageHelper.ReportError("Instance.Screenshot.OpenFailed", ex);
            }
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
        open.Click += (_, _) => Basics.OpenPath(path);
        buttons.Children.Add(open);
        var delete = CreateActionButton("Common.Action.Delete", "lucide/trash-2");
        delete.Click += (_, _) => _ = DeleteAsync(path, card);
        buttons.Children.Add(delete);
        var copy = CreateActionButton("Common.Action.Copy", "lucide/copy");
        copy.Click += (_, _) => _ = CopyPathAsync(path);
        buttons.Children.Add(copy);
        grid.Children.Add(buttons);
        card.BorderChild = grid;
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

    private async Task CopyPathAsync(string path)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(path);
            HintService.Show(Hints.Success(Lang.Text("Instance.Screenshot.CopiedToClipboard")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.CopyFailed", ex);
        }
    }

    private async Task DeleteAsync(string path, MyCard card)
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Event.Safety.ConfirmMessage", Lang.Text("Common.Action.Delete") + " " + Path.GetFileName(path)),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!result.IsConfirmed)
                return;

            var canRecycle = RecycleBin.IsSupported(path);
            if (canRecycle)
            {
                var outcome = RecycleBin.Move(path, out var error);
                if (outcome == RecycleBin.Outcome.Failed)
                {
                    LauncherCommon.Log("删除截图失败：" + error, LauncherLogLevel.Feedback);
                    HintService.Show(Hints.Error(Lang.Text("Instance.Screenshot.DeleteFailed")));
                    return;
                }

                if (outcome == RecycleBin.Outcome.Unsupported)
                    canRecycle = false;
            }

            if (!canRecycle)
                File.Delete(path);

            _panList.Children.Remove(card);
            _fileList.Remove(path);
            UpdateEmptyState();
            HintService.Show(Hints.Info(Lang.Text("Instance.Screenshot.Deleted")));
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Screenshot.DeleteFailed", ex);
        }
    }

    private void OpenFolder()
    {
        if (string.IsNullOrEmpty(_screenshotPath))
            return;
        InstancePageHelper.EnsureDirectory(_screenshotPath);
        InstancePageHelper.OpenFolder(_screenshotPath);
    }
}

using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SearchOption = System.IO.SearchOption;

namespace PCL;

public partial class PageInstanceScreenshot : IRefreshable, IPageView
{
    /// <summary>缩略图并行解码的数量上限。</summary>
    private const int DecodeConcurrency = 4;

    /// <summary>滚动装填的防抖时长（毫秒）。</summary>
    private const int AppendDebounceMilliseconds = 100;

    private readonly SemaphoreSlim _decodeSemaphore = new(DecodeConcurrency);

    private readonly PageInstanceScreenshotViewModel _vm = new();

    /// <summary>下一次增量装填从 fileList 的哪个下标开始；删除文件后也不会重复或跳过条目。</summary>
    private int _nextFileIndex;
    private bool _allLoaded;
    private int _appendDebounceVersion;
    private CancellationTokenSource? _loadCts;
    private int _loadGeneration;
    private Task? _appendTask;

    private List<string> _fileList = [];

    private bool _isLoad;
    private string _screenshotPath;
    private bool _refreshed;

    public PageInstanceScreenshot()
    {
        InitializeComponent();
        DataContext = _vm;
        // 等待动画只在覆盖层可见时运行，避免隐藏时仍然空转
        PanLoad.IsVisibleChanged += (_, _) =>
            Load.State.LoadingState = PanLoad.IsVisible ? LoadingState.Run : LoadingState.Stop;
        Loaded += PageSetupLaunch_Loaded;
        Unloaded += (_, _) =>
        {
            _refreshed = false;
            CancelLoad();
            _vm.PanLoadVisibility = Visibility.Collapsed;
        };
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
        CancelLoad();
        _vm.PanLoadVisibility = Visibility.Collapsed;
    }

    public PageInstanceScreenshotViewModel ViewModel => _vm;

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    private void RefreshSelf()
    {
        var ignore = RefreshAsync();
    }

    public static async Task RefreshAsync()
    {
        if (Launcher.frmInstanceScreenshot is not null)
            await Launcher.frmInstanceScreenshot.ReloadAsync().ConfigureAwait(true);
        Launcher.frmInstanceLeft!.ItemScreenshot.Checked = true;
        HintService.Show(Hints.Info(Lang.Text("Instance.Saves.Status.Refreshing"), log: false));
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        // 重复加载部分
        PanBack.ScrollToHome();
        _screenshotPath = PageInstanceLeft.McInstance.PathIndie + @"screenshots\";
        if (!Directory.Exists(_screenshotPath))
            Directory.CreateDirectory(_screenshotPath);
        Dispatcher.BeginInvoke(new Func<Task>(ReloadAsync));

        // 非重复加载部分
        if (_isLoad)
            return;
        _isLoad = true;
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public async Task ReloadAsync()
    {
        CancelLoad();
        var generation = _loadGeneration;
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;
        Animations.AniBeginControl();
        try
        {
            PanBack.ScrollToHome();
            _vm.PanLoadVisibility = Visibility.Visible;
            await LoadFileListAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 页面已退出或已重新加载，丢弃本次加载的结果
        }
        finally
        {
            if (generation == _loadGeneration)
                _vm.PanLoadVisibility = Visibility.Collapsed;
            Animations.AniEndControl();
        }
    }

    /// <summary>取消当前进行中的加载与装填。</summary>
    private void CancelLoad()
    {
        _loadGeneration += 1;
        _loadCts?.Cancel();
        _appendDebounceVersion += 1;
    }

    /// <summary>获取用于增量装填的取消令牌，页面重新进入时自动恢复可用。</summary>
    private CancellationToken GetAppendToken()
    {
        if (_loadCts is null || _loadCts.IsCancellationRequested)
            _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    private void RefreshTip()
    {
        _vm.RefreshTip(_fileList.Count);
    }

    private static readonly string[] AllowedSuffix = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff"];

    private async Task LoadFileListAsync(CancellationToken token)
    {
        Common.Log("[Screenshot] 刷新截图文件");
        var path = _screenshotPath;
        var list = new List<string>();
        if (Directory.Exists(path))
        {
            list = await Task.Run(
                () => AllowedSuffix
                    .SelectMany(suffix => Directory.EnumerateFiles(path, suffix, SearchOption.TopDirectoryOnly))
                    .OrderByDescending(File.GetCreationTime)
                    .ToList(),
                token).ConfigureAwait(true);
        }

        token.ThrowIfCancellationRequested();

        _fileList = list;
        _nextFileIndex = 0;
        _allLoaded = false;
        _appendDebounceVersion += 1;
        _vm.Screenshots.Clear();
        RefreshTip();
        //FileList = FileList.Where(e => !e.ContainsF(@"\debug\")).ToList(); // 排除资源包调试输出
        //FileList.Sort((a, b) => new FileInfo(a).CreationTime > new FileInfo(b).CreationTime);
        Common.Log($"[Screenshot] 共发现 {_fileList.Count} 个截图文件");
        if (_fileList.Count == 0)
        {
            _allLoaded = true;
            return;
        }

        await ListAppendAsync(token).ConfigureAwait(true);
    }

    private void RequireAppend(object sender, ScrollChangedEventArgs e)
    {
        if (_fileList.Count == 0 || _allLoaded || _appendTask is { IsCompleted: false })
            return;
        // 预留半屏的预取余量，让装填在滚动到底部之前就开始
        if (PanBack.VerticalOffset + PanBack.ViewportHeight < PanBack.ExtentHeight - PanBack.ViewportHeight * 0.5)
            return;
        var version = ++_appendDebounceVersion;
        _ = AppendDebouncedAsync(version, GetAppendToken());
    }

    /// <summary>滚动装填的防抖入口：100ms 内多次滚动只会真正装填一次。</summary>
    private async Task AppendDebouncedAsync(int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(AppendDebounceMilliseconds, token).ConfigureAwait(true);
            if (version != _appendDebounceVersion)
                return;
            await ListAppendAsync(token);
        }
        catch (OperationCanceledException)
        {
            // 页面已退出或已重新加载，忽略本次装填
        }
        catch (Exception ex)
        {
            Common.Log(ex, "[Screenshot] 增量加载截图列表失败");
        }
    }

    private async Task ListAppendAsync(CancellationToken token, int count = 20)
    {
        if (_appendTask is { IsCompleted: false })
            return; // 已有装填在进行中
        var task = AppendBatchAsync(token, count);
        _appendTask = task;
        await task.ConfigureAwait(true);
    }

    private async Task AppendBatchAsync(CancellationToken token, int count)
    {
        token.ThrowIfCancellationRequested();
        var files = _fileList;
        var start = _nextFileIndex;
        if (start >= files.Count)
        {
            _allLoaded = true;
            return;
        }

        var batch = await Task.Run(() => CreateBatch(files, start, count, _screenshotPath, token), token).ConfigureAwait(true);
        token.ThrowIfCancellationRequested();

        var thumbnails = await DecodeThumbnailsAsync(batch, token).ConfigureAwait(true);
        token.ThrowIfCancellationRequested();

        for (var i = 0; i < batch.Count; i++)
        {
            var item = batch[i];
            if (thumbnails[i] is null)
                continue;
            item.Thumbnail = thumbnails[i];
            _vm.Screenshots.Add(item);
        }

        _nextFileIndex = Math.Min(files.Count, start + count);
        if (_nextFileIndex >= files.Count)
            _allLoaded = true;
    }

    private static List<ScreenshotItemViewModel> CreateBatch(List<string> files, int start, int count,
        string rootPath, CancellationToken token)
    {
        var batch = new List<ScreenshotItemViewModel>(Math.Min(count, files.Count - start));
        for (var j = start; j < start + count && j < files.Count; j++)
        {
            token.ThrowIfCancellationRequested();
            var path = files[j];
            try
            {
                if (!File.Exists(path))
                    continue; // 文件在加载途中消失了
                if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
                    continue; // 隐藏文件
                if (new FileInfo(path).Length == 0L)
                    continue; // 空文件
                var displayName = string.IsNullOrEmpty(rootPath) ? path : path.Replace(rootPath, "");
                batch.Add(new ScreenshotItemViewModel(path, displayName)); // 适配高清截图模组
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"[Screenshot] 创建 {path} 截图预览失败，图像可能损坏");
            }
        }

        return batch;
    }

    private Task<BitmapImage?[]> DecodeThumbnailsAsync(List<ScreenshotItemViewModel> batch,
        CancellationToken token)
    {
        var tasks = new Task<BitmapImage?>[batch.Count];
        for (var i = 0; i < batch.Count; i++)
            tasks[i] = DecodeThumbnailAsync(batch[i].FilePath, token);
        return Task.WhenAll(tasks);
    }

    private async Task<BitmapImage?> DecodeThumbnailAsync(string path, CancellationToken token)
    {
        await _decodeSemaphore.WaitAsync(token).ConfigureAwait(true);
        try
        {
            return await Task.Run(() =>
            {
                var bitmapImage = new BitmapImage();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    bitmapImage.BeginInit();
                    bitmapImage.DecodePixelHeight = 200;
                    bitmapImage.DecodePixelWidth = 400;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = fs;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }

                return bitmapImage;
            }, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Common.Log(ex, $"[Screenshot] 创建 {path} 截图预览失败，图像可能损坏");
            return null;
        }
        finally
        {
            _decodeSemaphore.Release();
        }
    }

    /// <summary>截图卡片首次进入可视树时播放入场淡入动画。</summary>
    private void ScreenshotCard_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MyCard card)
            return;
        if (card.Tag is true)
            return; // 已经播放过
        card.Tag = true;
        card.Opacity = 0d;
        Animations.AniStart(new[] { Animations.AaOpacity(card, 1d, 200) });
    }

    /// <summary>MyIconTextButton 的 SvgIcon 不是依赖属性，只能在 Loaded 时按名称映射。</summary>
    private void ScreenshotIconButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MyIconTextButton button)
            return;
        if (!string.IsNullOrEmpty(button.SvgIcon))
            return;
        button.SvgIcon = button.Name switch
        {
            "BtnOpen" => "lucide/folder-open",
            "BtnDelete" => "lucide/trash-2",
            "BtnCopy" => "lucide/copy",
            _ => string.Empty
        };
    }

    private void ScreenshotImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ScreenshotItemViewModel item)
            return;
        try
        {
            Basics.OpenPath(item.FilePath);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Instance.Screenshot.OpenFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Screenshot.OpenFailed"));
        }
    }

    private void BtnDelete_Click(object sender, Common.RouteEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ScreenshotItemViewModel item)
            return;
        var path = item.FilePath;
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            var fileIndex = _fileList.IndexOf(path);
            if (fileIndex >= 0)
            {
                _fileList.RemoveAt(fileIndex);
                if (fileIndex < _nextFileIndex)
                    _nextFileIndex = Math.Max(0, _nextFileIndex - 1);
            }

            _vm.Screenshots.Remove(item);
            RefreshTip();
            HintService.Show(Hints.Info(Lang.Text("Instance.Screenshot.Deleted")));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Instance.Screenshot.DeleteFailed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Instance.Screenshot.DeleteFailed"));
        }
    }
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.UI.Controls;

/// <summary>
///     带网络回退与本地缓存的图片控件。
///     <see cref="Source" /> 为字符串：本地路径 / <c>avares://</c> / <c>file://</c> 直接显示；
///     以 <c>http</c> 开头时先查缓存，再下载到 <see cref="Paths.Temp" /> 下的图片缓存目录，
///     下载期间显示 <see cref="LoadingSource" />，失败时尝试 <see cref="FallbackSource" />。
/// </summary>
public class MyImage : Image
{
    /// <summary>
    ///     与 <see cref="Image.Source" /> 类似，但以字符串形式描述图片地址。
    /// </summary>
    public new static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<MyImage, string?>(nameof(Source));

    public static readonly StyledProperty<bool> EnableCacheProperty =
        AvaloniaProperty.Register<MyImage, bool>(nameof(EnableCache), true);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyImage, CornerRadius>(
            nameof(CornerRadius),
            new CornerRadius(-1d));

    private static readonly ConcurrentDictionary<string, Task<string>> DownloadTasks = new();

    private string? _actualSource;
    private bool _isAttached;

    /// <summary>
    ///     网络图片缓存的有效期。
    /// </summary>
    public TimeSpan fileCacheExpiredTime = TimeSpan.FromDays(14d);

    public MyImage()
    {
        FeedbackTheme.AttachStyles(this);
    }

    /// <summary>
    ///     实际被呈现的图片地址（本地文件、<c>avares://</c> 或下载后的缓存路径）。
    /// </summary>
    public string? ActualSource
    {
        get => _actualSource;
        set
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if (string.Equals(
                    _actualSource ?? string.Empty,
                    value ?? string.Empty,
                    StringComparison.Ordinal))
                return;

            _actualSource = value;
            _ = LoadActualSourceAsync(value);
        }
    }

    public bool EnableCache
    {
        get => GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
    }

    /// <summary>
    ///     与 Image 的 Source 类似。
    ///     若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    /// </summary>
    public new string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    ///     当 Source 首次下载失败时，会从该备用地址加载图片。
    /// </summary>
    public string FallbackSource { get; set; } = string.Empty;

    /// <summary>
    ///     正在下载网络图片时显示的本地占位图，默认使用内置的 <c>NoIcon.png</c>。
    /// </summary>
    public string LoadingSource { get; set; } =
        "avares://PCL.Launcher/Assets/Images/Icons/NoIcon.png";

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
            Load();
        else if (change.Property == CornerRadiusProperty || change.Property == BoundsProperty)
            UpdateClip();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Load(); // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void Load()
    {
        if (!_isAttached)
            return;

        var source = Source;
        if (string.IsNullOrEmpty(source))
        {
            ActualSource = null;
            return;
        }

        if (!source.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            ActualSource = source;
            return;
        }

        var tempPath = GetTempPath(source);
        var tempFile = new FileInfo(tempPath);
        var enableCache = EnableCache;
        if (enableCache && tempFile.Exists)
        {
            ActualSource = tempPath;
            if (DateTime.Now - tempFile.LastWriteTime < fileCacheExpiredTime)
                return; // 无需刷新缓存
        }

        _ = LoadFromNetworkAsync(source, enableCache);
    }

    private async Task LoadFromNetworkAsync(string url, bool enableCache)
    {
        try
        {
            // 下载
            var loadingSource = LoadingSource;
            if (!string.IsNullOrEmpty(loadingSource))
                ActualSource = loadingSource;

            var result = await DownloadImageAsync(url).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                SetActualSourceOnUi(result);
                return;
            }

            var fallback = FallbackSource;
            if (!string.IsNullOrEmpty(fallback))
            {
                result = await DownloadImageAsync(fallback).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(result)) SetActualSourceOnUi(result);
            }
        }
        catch (Exception ex)
        {
            // 更换备用地址
            LogWrapper.Debug(
                ex,
                "MyImage",
                $"在线图片获取失败（source = {url}, fallback = {FallbackSource}）");
            var tempPath = GetTempPath(url);
            var tempFile = new FileInfo(tempPath);
            if (enableCache && tempFile.Exists)
                SetActualSourceOnUi(tempPath);
        }
    }

    private void SetActualSourceOnUi(string? source)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ActualSource = source;
        else
            Dispatcher.UIThread.Post(() => ActualSource = source);
    }

    private async Task LoadActualSourceAsync(string? source)
    {
        if (source is null)
        {
            SetImageSource(null);
            return;
        }

        try
        {
            var bitmap = await Task.Run(() => DecodeBitmap(source)).ConfigureAwait(false);
            if (!string.Equals(_actualSource, source, StringComparison.Ordinal))
                return; // 期间地址已改变，丢弃过期结果

            SetImageSource(bitmap);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "MyImage", $"加载图片失败（{source}）");
            try
            {
                if (source.StartsWith(Paths.Temp, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(source))
                    File.Delete(source);
            }
            catch
            {
                // 缓存文件删除失败不影响显示
            }

            SetImageSource(null);
        }
    }

    private void SetImageSource(IImage? image)
    {
        if (Dispatcher.UIThread.CheckAccess())
            base.Source = image;
        else
            Dispatcher.UIThread.Post(() => base.Source = image);
    }

    private static IImage DecodeBitmap(string source)
    {
        using var stream = OpenSource(source);
        return new Bitmap(stream);
    }

    private static Stream OpenSource(string source)
    {
        if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return AssetLoader.Open(new Uri(source, UriKind.Absolute));

        if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return File.OpenRead(new Uri(source, UriKind.Absolute).LocalPath);

        return File.OpenRead(source);
    }

    private void UpdateClip()
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        var cornerRadius = CornerRadius;
        if (width > 0d
            && height > 0d
            && cornerRadius is { TopLeft: >= 0d, TopRight: >= 0d })
            Clip = new RectangleGeometry(
                new Rect(0d, 0d, width, height),
                cornerRadius.TopLeft,
                cornerRadius.TopRight);
    }

    // =======================================
    // 下载
    // =======================================

    public static Task<string> DownloadImageAsync(string url)
    {
        return DownloadTasks.GetOrAdd(
            url,
            key =>
            {
                var task = DownloadImageInternalAsync(key);
                _ = task.ContinueWith(_ => DownloadTasks.TryRemove(url, out var _));
                return task;
            });
    }

    public static string GetTempPath(string url)
    {
        return Path.Combine(Paths.Temp, "Cache", "Images", $"{GetStringMd5(url)}.png");
    }

    private static async Task<string> DownloadImageInternalAsync(string url)
    {
        var tempPath = GetTempPath(url);
        var tempDownloadingPath = tempPath + RandomUtils.NextInt(0, 1000000);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!); // 重新实现下载，以避免携带 Header
            await using (var fileStream = new FileStream(
                             tempDownloadingPath,
                             FileMode.Create,
                             FileAccess.ReadWrite,
                             FileShare.Read))
            {
                using var response = await HttpRequest
                    .Create(url)
                    .SendAsync(addMetedata: false)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var networkStream =
                    await response.AsStreamAsync().ConfigureAwait(false);
                fileStream.SetLength(0L);
                await networkStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            File.Move(tempDownloadingPath, tempPath, true);
            return tempPath;
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                if (File.Exists(tempDownloadingPath)) File.Delete(tempDownloadingPath);
            }
            catch
            {
                // 清理失败不影响后续流程
            }

            LogWrapper.Debug(ex, "MyImage", $"获取在线图片失败（url = {url}, dest = {tempPath}）");
            return string.Empty;
        }
    }

    private static string GetStringMd5(string text)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
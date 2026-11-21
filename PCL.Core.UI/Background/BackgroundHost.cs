using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Tools;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.UI.Animation.Core;
using PCL.Core.UI.Media;
using PCL.Core.UI.Theme;

namespace PCL.Core.UI.Background;

/// <summary>背景刷新结果状态。</summary>
public enum BackgroundRefreshStatus
{
    Success,
    NoContent,
    FfmpegMissing,
    Canceled,
    Failed
}

/// <summary>背景刷新结果。</summary>
public sealed record BackgroundRefreshResult(
    BackgroundRefreshStatus Status,
    string? Path = null,
    string? Detail = null);

/// <summary>主窗口背景入口：供设置页等外部调用方请求重新加载背景内容。</summary>
public static class BackgroundHost
{
    /// <summary>当前主窗口的背景层。</summary>
    public static BackgroundLayer? Current { get; private set; }

    /// <summary>背景播放状态变化时触发。</summary>
    public static event Action? StateChanged;

    /// <summary>请求重新加载背景内容。</summary>
    public static Task<BackgroundRefreshResult> RequestRefreshAsync()
    {
        return Current?.RefreshAsync()
            ?? Task.FromResult(
                new BackgroundRefreshResult(BackgroundRefreshStatus.NoContent));
    }

    public static void Attach(BackgroundLayer layer)
    {
        Current = layer;
    }

    public static void Detach(BackgroundLayer layer)
    {
        if (ReferenceEquals(Current, layer)) Current = null;
    }

    internal static void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}

/// <summary>主窗口背景层：绘制主题背景、图片或 FFmpeg 视频，并应用透明度、裁剪模式与模糊。</summary>
public sealed class BackgroundLayer
{
    private const int MaxVideoWidth = 1280;
    private const int MaxVideoHeight = 720;

    private readonly Panel _FormPan;
    private readonly Panel _Host;
    private readonly Image _LegacyImage;
    private readonly Border _Surface;

    private Bitmap? _bitmap;
    private bool _clearing;
    private FfmpegLocator? _ffmpegLocator;
    private ImageBrush? _imageBrush;
    private bool _isMinimized;
    private string? _lastError;
    private TaskCompletionSource<bool>? _refreshCompletion;
    private CancellationTokenSource? _refreshCts;
    private int _refreshGeneration;
    private bool _removed;
    private FfmpegVideoPlayer? _videoPlayer;
    private FfmpegVideoProbe? _videoProbe;
    private Window? _window;
    private bool _windowHooked;

    public BackgroundLayer(Panel formPan, Image image)
    {
        _FormPan = formPan;
        _LegacyImage = image;
        _LegacyImage.IsVisible = false;
        _Host = image.Parent as Panel ?? formPan;

        _Surface = new Border
        {
            IsVisible = false,
            IsHitTestVisible = false
        };
        _Host.Children.Add(_Surface);

        AttachWindow();
        _FormPan.AttachedToVisualTree += OnFormPanAttached;
        _FormPan.DetachedFromVisualTree += OnFormPanDetached;
        Config.Preference.Background.AutoPauseVideoConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => ApplyAutoPauseSetting()));
        Config.Preference.Background.FfmpegPathConfig.Observe(
            new ConfigObserver(ConfigEvent.Update, _ => InvalidateFfmpeg()));
        ThemeService.ColorModeChanged += _OnThemeChanged;
        ThemeService.ColorThemeChanged += _OnThemeChanged;
    }

    public bool HasContent { get; private set; }

    public bool IsGaming { get; private set; }

    public FfmpegProbeResult? LastFfmpegProbe { get; private set; }

    public void Refresh()
    {
        _ = RefreshAsync();
    }

    /// <summary>重新加载背景内容。</summary>
    public async Task<BackgroundRefreshResult> RefreshAsync()
    {
        if (_removed)
            return new BackgroundRefreshResult(BackgroundRefreshStatus.Canceled);
        if (!Dispatcher.UIThread.CheckAccess())
        {
            var completion = new TaskCompletionSource<BackgroundRefreshResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    completion.TrySetResult(await RefreshAsync());
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return await completion.Task;
        }

        if (_clearing)
            return new BackgroundRefreshResult(BackgroundRefreshStatus.Canceled);

        var generation = Interlocked.Increment(ref _refreshGeneration);
        var previousCts = _refreshCts;
        _refreshCts = null;
        try
        {
            previousCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var refreshCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _refreshCompletion = refreshCompletion;

        HasContent = false;
        RefreshColor();

        try
        {
            BackgroundRefreshResult result;
            try
            {
                result = await LoadContentAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return new BackgroundRefreshResult(BackgroundRefreshStatus.Canceled);
            }
            catch (Exception ex)
            {
                if (generation != Volatile.Read(ref _refreshGeneration))
                    return new BackgroundRefreshResult(BackgroundRefreshStatus.Canceled);

                _lastError = ex.Message;
                LogWrapper.Debug(ex, "UI", "刷新背景内容失败");
                ClearContent();
                return new BackgroundRefreshResult(
                    BackgroundRefreshStatus.Failed,
                    Detail: ex.Message);
            }

            if (generation != Volatile.Read(ref _refreshGeneration))
                return new BackgroundRefreshResult(BackgroundRefreshStatus.Canceled);

            try
            {
                HasContent = result.Status == BackgroundRefreshStatus.Success;
                ApplyPresentation();
                if (result.Status == BackgroundRefreshStatus.Success)
                    AnimateContentIn();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, "UI", "应用背景效果失败");
                return new BackgroundRefreshResult(
                    BackgroundRefreshStatus.Failed,
                    Detail: ex.Message);
            }

            BackgroundHost.NotifyStateChanged();
            return result;
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, cts)) _refreshCts = null;
            if (ReferenceEquals(_refreshCompletion, refreshCompletion))
                _refreshCompletion = null;
            refreshCompletion.TrySetResult(true);
            cts.Dispose();
        }
    }

    /// <summary>检测 FFmpeg 是否可用。</summary>
    public async Task<FfmpegProbeResult> ProbeFfmpegAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var locator = _ffmpegLocator ??= new FfmpegLocator();
        var result = await locator.DetectAsync(
            Config.Preference.Background.FfmpegPath,
            force,
            cancellationToken);
        LastFfmpegProbe = result;
        return result;
    }

    /// <summary>使 FFmpeg 检测缓存失效。</summary>
    public void InvalidateFfmpeg()
    {
        _ffmpegLocator?.Invalidate();
        LastFfmpegProbe = null;
    }

    /// <summary>设置游戏运行状态；自动暂停开启时暂停视频。</summary>
    public void SetGaming(bool gaming)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetGaming(gaming));
            return;
        }

        if (IsGaming == gaming) return;
        IsGaming = gaming;
        UpdatePlaybackState();
        BackgroundHost.NotifyStateChanged();
    }

    /// <summary>应用自动暂停设置。</summary>
    public void ApplyAutoPauseSetting()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyAutoPauseSetting);
            return;
        }

        UpdatePlaybackState();
        BackgroundHost.NotifyStateChanged();
    }

    private async Task<BackgroundRefreshResult> LoadContentAsync(
        CancellationToken cancellationToken)
    {
        await StopVideoAsync();
        cancellationToken.ThrowIfCancellationRequested();
        ClearImage();
        _lastError = null;

        var pictures = Path.Combine(Paths.Data, "Pictures");
        var files = await Task.Run(
            () => BackgroundContent.Collect(pictures, cancellationToken),
            cancellationToken);
        if (files.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClearContent();
            return new BackgroundRefreshResult(BackgroundRefreshStatus.NoContent);
        }

        FfmpegProbeResult? ffmpeg = null;
        var hasVideoContent = false;
        var requiresFfmpeg = false;
        string? lastError = null;

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (BackgroundContent.IsAnimatedImage(path))
            {
                hasVideoContent = true;
                ffmpeg ??= await ProbeFfmpegAsync(false, cancellationToken);
                if (ffmpeg.IsAvailable
                    && await TryLoadVideoAsync(path, ffmpeg, cancellationToken))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);
                cancellationToken.ThrowIfCancellationRequested();

                if (TryLoadImage(path))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);
            }
            else if (BackgroundContent.IsImage(path))
            {
                if (TryLoadImage(path))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);

                requiresFfmpeg = true;
                ffmpeg ??= await ProbeFfmpegAsync(false, cancellationToken);
                if (ffmpeg.IsAvailable
                    && await TryLoadVideoAsync(path, ffmpeg, cancellationToken))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);
                cancellationToken.ThrowIfCancellationRequested();
            }
            else if (BackgroundContent.IsVideo(path))
            {
                hasVideoContent = true;
                ffmpeg ??= await ProbeFfmpegAsync(false, cancellationToken);
                if (ffmpeg.IsAvailable
                    && await TryLoadVideoAsync(path, ffmpeg, cancellationToken))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);
                cancellationToken.ThrowIfCancellationRequested();

                if (!ffmpeg.IsAvailable) lastError = ffmpeg.Error;
            }

            if (!string.IsNullOrWhiteSpace(_lastError))
                lastError = _lastError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        ClearContent();
        return (hasVideoContent || requiresFfmpeg) && ffmpeg is { IsAvailable: false }
            ? new BackgroundRefreshResult(
                BackgroundRefreshStatus.FfmpegMissing,
                Detail: ffmpeg.Error ?? lastError)
            : new BackgroundRefreshResult(
                BackgroundRefreshStatus.Failed,
                Detail: lastError);
    }

    private async Task<bool> TryLoadVideoAsync(
        string path,
        FfmpegProbeResult ffmpeg,
        CancellationToken cancellationToken)
    {
        if (!ffmpeg.IsAvailable
            || string.IsNullOrWhiteSpace(ffmpeg.ExecutablePath)
            || _ffmpegLocator?.CommandRunner is not { } runner)
            return false;

        var suitMode = (VideoSuitMode)Math.Clamp(
            Config.Preference.Background.WallpaperSuitMode,
            0,
            8);
        var (targetWidth, targetHeight) = GetVideoTargetSize();
        VideoSize? sourceSize = null;
        try
        {
            sourceSize = await ProbeVideoSizeAsync(
                ffmpeg.ExecutablePath,
                path,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI", $"探测视频尺寸失败：{path}");
        }

        var effectiveMode = sourceSize is null ? VideoSuitMode.Fit : suitMode;
        var (width, height) = sourceSize is { } size
            ? GetVideoFrameSize(size)
            : (targetWidth, targetHeight);
        var player = new FfmpegVideoPlayer(
            runner,
            ffmpeg.ExecutablePath,
            path,
            width,
            height,
            effectiveMode);
        try
        {
            if (!await player.StartAsync(cancellationToken))
            {
                _lastError = player.LastError;
                InvalidateFfmpeg();
                player.Dispose();
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            player.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            InvalidateFfmpeg();
            player.Dispose();
            LogWrapper.Debug(ex, "UI", $"启动视频背景失败：{path}");
            return false;
        }

        if (cancellationToken.IsCancellationRequested || _removed)
        {
            player.Dispose();
            throw new OperationCanceledException(cancellationToken);
        }

        await StopVideoAsync();
        if (cancellationToken.IsCancellationRequested || _removed)
        {
            player.Dispose();
            throw new OperationCanceledException(cancellationToken);
        }

        ClearImage();

        _videoPlayer = player;
        _Surface.Background = null;
        _Surface.Child = player.View;
        _Surface.IsVisible = true;
        _lastError = null;
        UpdatePlaybackState();
        LogWrapper.Info("UI", $"已加载视频背景：{Path.GetFileName(path)}");
        return true;
    }

    private async Task<VideoSize?> ProbeVideoSizeAsync(
        string executablePath,
        string path,
        CancellationToken cancellationToken)
    {
        if (_ffmpegLocator?.CommandRunner is not { } runner) return null;
        _videoProbe ??= new FfmpegVideoProbe(runner);
        return await _videoProbe.ProbeAsync(
            executablePath,
            path,
            cancellationToken);
    }

    private static (int Width, int Height) GetVideoFrameSize(VideoSize source)
    {
        var scale = Math.Min(
            1d,
            Math.Min(
                MaxVideoWidth / (double)source.Width,
                MaxVideoHeight / (double)source.Height));
        var width = Math.Max(2, (int)Math.Round(source.Width * scale));
        var height = Math.Max(2, (int)Math.Round(source.Height * scale));
        return (_MakeEven(width), _MakeEven(height));
    }

    private bool TryLoadImage(string path)
    {
        if (_removed || !File.Exists(path)) return false;

        Bitmap? bitmap = null;
        try
        {
            bitmap = new Bitmap(path);
            var brush = new ImageBrush(bitmap);
            StopVideo();
            _Surface.Child = null;
            _Surface.Background = null;
            _imageBrush = null;
            _bitmap?.Dispose();
            _bitmap = bitmap;
            _imageBrush = brush;
            _Surface.Background = brush;
            _Surface.IsVisible = true;
            _lastError = null;
            ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);
            LogWrapper.Info("UI", $"已加载背景图片：{Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            _Surface.Background = null;
            _imageBrush = null;
            if (ReferenceEquals(_bitmap, bitmap)) _bitmap = null;
            bitmap?.Dispose();
            LogWrapper.Debug(ex, "UI", $"加载背景图片失败：{path}");
            return false;
        }
    }

    /// <summary>立即清除当前背景。</summary>
    public void Clear()
    {
        Interlocked.Increment(ref _refreshGeneration);
        var cts = _refreshCts;
        _refreshCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignored
        }

        ClearContent();
    }

    /// <summary>清除当前背景并等待视频进程退出。</summary>
    public async Task ClearAsync()
    {
        if (_clearing) return;
        _clearing = true;
        try
        {
            var generation = Interlocked.Increment(ref _refreshGeneration);
            var cts = _refreshCts;
            _refreshCts = null;
            var refreshCompletion = _refreshCompletion;
            try
            {
                cts?.Cancel();
            }
            catch
            {
                // ignored
            }

            if (refreshCompletion is not null)
                try
                {
                    await refreshCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // ignored
                }

            if (generation != Volatile.Read(ref _refreshGeneration)) return;
            await ClearContentAsync();
        }
        finally
        {
            _clearing = false;
        }
    }

    public void RefreshColor()
    {
        try
        {
            _FormPan.Background = Config.Preference.Background.BackgroundColorful
                ? CreateColorfulBrush()
                : FindThemeBrush("ColorBrushBackground") ?? Brushes.Transparent;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"刷新背景颜色失败：{ex.Message}");
        }
    }

    public void ApplyPresentation()
    {
        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        _Surface.Opacity = opacity;

        ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);

        var blur = Math.Max(Config.Preference.Background.WallpaperBlurRadius, 0);
        var effectRadius = blur > 0 ? blur + 1d : 0d;
        _Surface.Effect = effectRadius > 0
            ? new BlurEffect
            {
                Radius = effectRadius
            }
            : null;
        _Surface.Margin = blur > 0
            ? new Thickness(-(blur + 1d) / 1.8d)
            : new Thickness(0d);
    }

    public void Remove()
    {
        _removed = true;
        _FormPan.AttachedToVisualTree -= OnFormPanAttached;
        _FormPan.DetachedFromVisualTree -= OnFormPanDetached;
        DetachWindow();
        ThemeService.ColorModeChanged -= _OnThemeChanged;
        ThemeService.ColorThemeChanged -= _OnThemeChanged;

        Interlocked.Increment(ref _refreshGeneration);
        var cts = _refreshCts;
        _refreshCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }

        ClearContent();
        _Host.Children.Remove(_Surface);
        _LegacyImage.IsVisible = false;
        _LegacyImage.Source = null;
    }

    private void StopVideo()
    {
        var player = _videoPlayer;
        _videoPlayer = null;
        _Surface.Child = null;

        player?.Dispose();
    }

    private async Task StopVideoAsync()
    {
        var player = _videoPlayer;
        _videoPlayer = null;
        _Surface.Child = null;

        if (player is not null) await player.StopAsync();
    }

    private void ClearImage()
    {
        _Surface.Background = null;
        _imageBrush = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    private void ClearContent()
    {
        HasContent = false;
        StopVideo();
        ClearImage();
        _Surface.IsVisible = false;
        _lastError = null;
    }

    private async Task ClearContentAsync()
    {
        HasContent = false;
        await StopVideoAsync();
        ClearImage();
        _Surface.IsVisible = false;
        _lastError = null;
    }

    private void UpdatePlaybackState()
    {
        var shouldPause = _isMinimized
            || (IsGaming
                && Config.Preference.Background.AutoPauseVideo);
        if (shouldPause)
            _videoPlayer?.Pause();
        else
            _videoPlayer?.Resume();
    }

    private void OnFormPanAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachWindow();
    }

    private void OnFormPanDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachWindow();
    }

    private void AttachWindow()
    {
        if (_windowHooked) return;
        if (TopLevel.GetTopLevel(_FormPan) is not Window window) return;
        _window = window;
        _windowHooked = true;
        _isMinimized = window.WindowState == WindowState.Minimized;
        window.PropertyChanged += OnWindowPropertyChanged;
    }

    private void DetachWindow()
    {
        if (!_windowHooked) return;
        _windowHooked = false;
        if (_window is not null) _window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
        _isMinimized = false;
    }

    private void OnWindowPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Window.WindowStateProperty) return;
        if (_window is not { } window) return;
        var minimized = window.WindowState == WindowState.Minimized;
        if (_isMinimized == minimized) return;
        _isMinimized = minimized;
        UpdatePlaybackState();
    }

    private (int Width, int Height) GetVideoTargetSize()
    {
        var width = _Host.Bounds.Width > 0d
            ? _Host.Bounds.Width
            : _window?.Bounds.Width ?? 0d;
        var height = _Host.Bounds.Height > 0d
            ? _Host.Bounds.Height
            : _window?.Bounds.Height ?? 0d;
        if (width <= 0d) width = 850d;
        if (height <= 0d) height = 450d;

        var scaling = _window is { RenderScaling: > 0d } window ? window.RenderScaling : 1d;
        var targetWidth = Math.Max(2, (int)Math.Round(width * scaling));
        var targetHeight = Math.Max(2, (int)Math.Round(height * scaling));

        var scale = Math.Min(
            1d,
            Math.Min(
                MaxVideoWidth / (double)targetWidth,
                MaxVideoHeight / (double)targetHeight));
        targetWidth = Math.Max(2, (int)Math.Round(targetWidth * scale));
        targetHeight = Math.Max(2, (int)Math.Round(targetHeight * scale));
        return (_MakeEven(targetWidth), _MakeEven(targetHeight));
    }

    private static int _MakeEven(int value)
    {
        return (value & 1) == 0 ? value : value + 1;
    }

    private void AnimateContentIn()
    {
        if (!_Surface.IsVisible) return;

        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        _ = AnimationService.FadeAsync(_Surface, 0d, opacity, 260);
    }

    private void _OnThemeChanged(bool _, ColorTheme __)
    {
        RefreshColor();
    }

    private void _OnThemeChanged(ColorTheme _)
    {
        RefreshColor();
    }

    private void ApplySuitMode(int suitMode)
    {
        var videoMode = (VideoSuitMode)Math.Clamp(suitMode, 0, 8);
        _videoPlayer?.View.SuitMode = videoMode;
        if (_imageBrush is null || _bitmap is null) return;

        var brush = _imageBrush;
        var bitmap = _bitmap;

        if (suitMode == 0) suitMode = IsImageSmall(bitmap) ? 4 : 2;

        brush.TileMode = TileMode.None;
        brush.DestinationRect = RelativeRect.Fill;
        brush.AlignmentX = AlignmentX.Center;
        brush.AlignmentY = AlignmentY.Center;

        switch (suitMode)
        {
            case 1:
                brush.Stretch = Stretch.None;
                break;
            case 2:
                brush.Stretch = Stretch.UniformToFill;
                break;
            case 3:
                brush.Stretch = Stretch.Fill;
                break;
            case 4:
                brush.Stretch = Stretch.None;
                brush.TileMode = TileMode.Tile;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                brush.DestinationRect = new RelativeRect(
                    0d,
                    0d,
                    bitmap.Size.Width,
                    bitmap.Size.Height,
                    RelativeUnit.Absolute);
                break;
            case 5:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Top;
                break;
            case 6:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Right;
                brush.AlignmentY = AlignmentY.Top;
                break;
            case 7:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Left;
                brush.AlignmentY = AlignmentY.Bottom;
                break;
            case 8:
                brush.Stretch = Stretch.None;
                brush.AlignmentX = AlignmentX.Right;
                brush.AlignmentY = AlignmentY.Bottom;
                break;
            default:
                brush.Stretch = Stretch.UniformToFill;
                break;
        }
    }

    private bool IsImageSmall(Bitmap bitmap)
    {
        var width = _window is { Bounds.Width: > 0d } window ? window.Bounds.Width : 850d;
        var height = _window is { Bounds.Height: > 0d } window1 ? window1.Bounds.Height : 500d;
        return bitmap.PixelSize.Width < width / 2d
            && bitmap.PixelSize.Height < height / 2d;
    }

    private static LinearGradientBrush CreateColorfulBrush()
    {
        var tone = ThemeService.CurrentTone;
        var (hue, _, _) = ThemeService.GetCurrentThemeArgs();
        var darkLight = ThemeService.IsDarkMode ? 0.2d : 1d;
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.9d, 0d, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.1d, 1d, RelativeUnit.Relative)
        };
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue - 15d)),
                -0.1d));
        brush.GradientStops.Add(
            new GradientStop(ToColor(LabColor.FromLch(0.96d * darkLight, tone.C7, hue)), 0.4d));
        brush.GradientStops.Add(
            new GradientStop(
                ToColor(LabColor.FromLch(0.84d * darkLight, tone.C5, hue + 15d)),
                1.1d));
        return brush;
    }

    private static Color ToColor(LabColor lab)
    {
        var (alpha, red, green, blue) = lab.ToScRgb();
        return Color.FromArgb(SrgbByte(alpha), SrgbByte(red), SrgbByte(green), SrgbByte(blue));
    }

    private static byte SrgbByte(double linear)
    {
        var encoded = linear <= 0.0031308d
            ? 12.92d * linear
            : 1.055d * Math.Pow(linear, 1d / 2.4d) - 0.055d;
        return (byte)Math.Clamp(Math.Round(encoded * 255d), 0d, 255d);
    }

    private static IBrush? FindThemeBrush(string key)
    {
        return Application.Current is { } app && app.TryFindResource(key, out var resource)
            ? resource as IBrush
            : null;
    }
}
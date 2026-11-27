using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Tools;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.UI.Theme;

namespace PCL.Core.UI.Background;

/// <summary>主窗口背景层：组合内容控制器、播放状态和背景表面。</summary>
public sealed class BackgroundLayer
{
    private readonly ConfigObserver _autoPauseObserver;
    private readonly Panel _formPan;
    private readonly BackgroundImageController _image;
    private readonly BackgroundSurfaceHost _surface;
    private readonly BackgroundVideoController _video;

    private bool _clearing;
    private bool _isMinimized;
    private string? _lastError;
    private TaskCompletionSource<bool>? _refreshCompletion;
    private CancellationTokenSource? _refreshCts;
    private int _refreshGeneration;
    private bool _removed;
    private Window? _window;
    private bool _windowHooked;

    public BackgroundLayer(Panel formPan, Image image)
    {
        _formPan = formPan;
        _surface = new BackgroundSurfaceHost(formPan, image);
        _video = new BackgroundVideoController(GetViewport, GetSuitMode);
        _image = new BackgroundImageController(GetImageViewport);
        _video.ViewChanged += OnVideoViewChanged;
        _image.Changed += OnImageChanged;

        AttachWindow();
        _formPan.AttachedToVisualTree += OnFormPanAttached;
        _formPan.DetachedFromVisualTree += OnFormPanDetached;

        _autoPauseObserver = new ConfigObserver(
            ConfigEvent.Update,
            _ => ApplyAutoPauseSetting());
        Config.Preference.Background.AutoPauseVideoConfig.Observe(_autoPauseObserver);

        ThemeService.ColorModeChanged += _OnThemeChanged;
        ThemeService.ColorThemeChanged += _OnThemeChanged;
    }

    public bool HasContent => _surface.HasContent;

    public bool IsGaming { get; private set; }

    public FfmpegProbeResult? LastFfmpegProbe => _video.LastFfmpegProbe;

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
    public Task<FfmpegProbeResult> ProbeFfmpegAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        return _video.ProbeFfmpegAsync(force, cancellationToken);
    }

    /// <summary>使 FFmpeg 检测缓存失效。</summary>
    public void InvalidateFfmpeg()
    {
        _video.InvalidateFfmpeg();
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

    /// <summary>应用硬件加速设置；当前视频使用同一路径重新加载。</summary>
    public void ApplyHardwareAccelerationSetting()
    {
        _video.ApplyHardwareAccelerationSetting();
    }

    private async Task<BackgroundRefreshResult> LoadContentAsync(
        CancellationToken cancellationToken)
    {
        await _video.ResetAsync().ConfigureAwait(true);
        _image.Clear();
        _surface.Clear();
        cancellationToken.ThrowIfCancellationRequested();
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

                if (await TryLoadImageAsync(path, cancellationToken))
                    return new BackgroundRefreshResult(
                        BackgroundRefreshStatus.Success,
                        path);
            }
            else if (BackgroundContent.IsImage(path))
            {
                if (await TryLoadImageAsync(path, cancellationToken))
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
        if (await _video.LoadAsync(path, ffmpeg, cancellationToken))
            return true;

        _lastError = _video.LastError;
        return false;
    }

    private async Task<bool> TryLoadImageAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await _video.ResetAsync().ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        return _image.TryLoad(path);
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
        BackgroundPalette.RefreshColor(_formPan);
    }

    public void ApplyPresentation()
    {
        ApplySuitMode(Config.Preference.Background.WallpaperSuitMode);
        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        var blur = Math.Max(Config.Preference.Background.WallpaperBlurRadius, 0);
        _surface.ApplyPresentation(_video.View, opacity, blur);
    }

    public void Remove()
    {
        _removed = true;
        _formPan.AttachedToVisualTree -= OnFormPanAttached;
        _formPan.DetachedFromVisualTree -= OnFormPanDetached;
        DetachWindow();
        ThemeService.ColorModeChanged -= _OnThemeChanged;
        ThemeService.ColorThemeChanged -= _OnThemeChanged;
        Config.Preference.Background.AutoPauseVideoConfig.Unobserve(_autoPauseObserver);

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

        _surface.Clear();
        _video.ViewChanged -= OnVideoViewChanged;
        _video.Dispose();
        _image.Changed -= OnImageChanged;
        _image.Dispose();
        _surface.Remove();
    }

    private void ClearContent()
    {
        _video.Reset();
        _image.Clear();
        _surface.Clear();
        _lastError = null;
    }

    private async Task ClearContentAsync()
    {
        await _video.ResetAsync();
        _image.Clear();
        _surface.Clear();
        _lastError = null;
    }

    private void UpdatePlaybackState()
    {
        _video.SetPaused(ShouldPauseVideo());
    }

    private bool ShouldPauseVideo()
    {
        return _isMinimized
            || (IsGaming && Config.Preference.Background.AutoPauseVideo);
    }

    private void OnVideoViewChanged()
    {
        _surface.SetVideo(_video.View);
        ApplyPresentation();
        UpdatePlaybackState();
        BackgroundHost.NotifyStateChanged();
    }

    private void OnImageChanged()
    {
        _surface.SetImage(_image.Brush);
        ApplyPresentation();
        UpdatePlaybackState();
        BackgroundHost.NotifyStateChanged();
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
        if (TopLevel.GetTopLevel(_formPan) is not Window window) return;
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

    private BackgroundViewport GetViewport()
    {
        var bounds = _surface.HostBounds;
        var width = bounds.Width > 0d
            ? bounds.Width
            : _window?.Bounds.Width ?? 0d;
        var height = bounds.Height > 0d
            ? bounds.Height
            : _window?.Bounds.Height ?? 0d;
        var scaling = _window is { RenderScaling: > 0d } window
            ? window.RenderScaling
            : 1d;
        return new BackgroundViewport(width, height, scaling);
    }

    private BackgroundViewport GetImageViewport()
    {
        var window = _window;
        var width = window is { Bounds.Width: > 0d } ? window.Bounds.Width : 850d;
        var height = window is { Bounds.Height: > 0d } ? window.Bounds.Height : 500d;
        return new BackgroundViewport(width, height, 1d);
    }

    private static VideoSuitMode GetSuitMode()
    {
        return (VideoSuitMode)Math.Clamp(
            Config.Preference.Background.WallpaperSuitMode,
            0,
            8);
    }

    private void ApplySuitMode(int suitMode)
    {
        var videoMode = (VideoSuitMode)Math.Clamp(suitMode, 0, 8);
        _video.View?.SuitMode = videoMode;
        _image.ApplySuitMode(
            videoMode is VideoSuitMode.Smart
                ? 0
                : (int)videoMode);
    }

    private void AnimateContentIn()
    {
        if (!_surface.HasContent) return;

        var opacity = Math.Clamp(
            Config.Preference.Background.WallpaperOpacity / 1000d,
            0d,
            1d);
        _surface.AnimateIn(opacity);
    }

    private void _OnThemeChanged(bool _, ColorTheme __)
    {
        RefreshColor();
    }

    private void _OnThemeChanged(ColorTheme _)
    {
        RefreshColor();
    }
}
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.UI.Media;

namespace PCL.Core.UI.Background;

/// <summary>负责视频探测、解码 profile 选择、播放器生命周期和暂停恢复。</summary>
public sealed class BackgroundVideoController : IDisposable
{
    private const int MaxVideoWidth = 1280;
    private const int MaxVideoHeight = 720;
    private static readonly TimeSpan SuspendDelay = TimeSpan.FromSeconds(30);

    private readonly ConfigObserver _ffmpegPathObserver;
    private readonly ConfigObserver _hardwareAccelerationObserver;
    private readonly HashSet<string> _hardwareDecodeFailures = new(StringComparer.Ordinal);
    private readonly Func<VideoSuitMode> _suitModeProvider;
    private readonly Func<BackgroundViewport> _viewportProvider;

    private string? _currentPath;
    private bool _disposed;
    private int _generation;
    private FfmpegLocator? _locator;
    private bool _paused;
    private FfmpegVideoPlayer? _player;
    private FfmpegVideoProbe? _probe;
    private CancellationTokenSource? _reloadCts;
    private CancellationTokenSource? _suspendCts;
    private bool _yuvUnavailable;

    public BackgroundVideoController(
        Func<BackgroundViewport> viewportProvider,
        Func<VideoSuitMode> suitModeProvider)
    {
        _viewportProvider = viewportProvider
            ?? throw new ArgumentNullException(nameof(viewportProvider));
        _suitModeProvider = suitModeProvider
            ?? throw new ArgumentNullException(nameof(suitModeProvider));

        _ffmpegPathObserver = new ConfigObserver(
            ConfigEvent.Update,
            e =>
            {
                _hardwareDecodeFailures.Clear();
                InvalidateFfmpeg();
                _ = ReloadAsync();
            });
        _hardwareAccelerationObserver = new ConfigObserver(
            ConfigEvent.Update,
            e =>
            {
                _hardwareDecodeFailures.Clear();
                _ = ReloadAsync();
            });
        Config.Preference.Background.FfmpegPathConfig.Observe(_ffmpegPathObserver);
        Config.Preference.Background.VideoHardwareAccelerationConfig.Observe(
            _hardwareAccelerationObserver);
    }

    public FfmpegProbeResult? LastFfmpegProbe { get; private set; }

    public string LastError { get; private set; } = string.Empty;

    public VideoSurface? View => _player?.View;

    public bool HasView => _player is not null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        Config.Preference.Background.FfmpegPathConfig.Unobserve(_ffmpegPathObserver);
        Config.Preference.Background.VideoHardwareAccelerationConfig.Unobserve(
            _hardwareAccelerationObserver);
        CancelReload();
        CancelSuspend();

        var player = _player;
        _player = null;
        _currentPath = null;
        if (player is null) return;
        player.View.RendererUnavailable -= OnRendererUnavailable;
        player.Dispose();
    }

    public event Action? ViewChanged;

    public Task<FfmpegProbeResult> ProbeFfmpegAsync(
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var locator = _locator ??= new FfmpegLocator();
        return _ProbeFfmpegAsync(locator, force, cancellationToken);
    }

    public void InvalidateFfmpeg()
    {
        _locator?.Invalidate();
        LastFfmpegProbe = null;
    }

    public async Task<bool> LoadAsync(
        string path,
        FfmpegProbeResult ffmpeg,
        CancellationToken cancellationToken)
    {
        return await LoadAsync(path, ffmpeg, cancellationToken, null);
    }

    internal async Task<bool> LoadAsync(
        string path,
        FfmpegProbeResult ffmpeg,
        CancellationToken cancellationToken,
        int? expectedGeneration)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed
            || !ffmpeg.IsAvailable
            || string.IsNullOrWhiteSpace(ffmpeg.ExecutablePath)
            || _locator?.CommandRunner is not { } runner)
            return false;

        var generation = expectedGeneration
            ?? Volatile.Read(ref _generation);
        var viewport = _viewportProvider();
        var suitMode = _suitModeProvider();
        LastFfmpegProbe = ffmpeg;

        VideoInfo? videoInfo = null;
        try
        {
            videoInfo = await ProbeVideoInfoAsync(
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
            LogWrapper.Debug(ex, "UI", $"探测视频信息失败：{path}");
        }

        videoInfo = NormalizeVideoInfoForOutput(videoInfo);
        var sourceSize = videoInfo?.Size;
        var effectiveMode = sourceSize is null ? VideoSuitMode.Fit : suitMode;
        var (width, height) = sourceSize is { } size
            ? GetVideoFrameSize(size)
            : GetVideoTargetSize(viewport);
        var pixelFormat = _yuvUnavailable
            ? FfmpegPixelFormat.Bgra8888
            : FfmpegPixelFormat.Yuv420p;
        var profiles = FfmpegPlaybackProfiles.Create(
            Config.Preference.Background.VideoHardwareAcceleration,
            videoInfo,
            Environment.ProcessorCount,
            pixelFormat);
        var hardwareKey = GetVideoKey(path);
        var previous = _player;

        FfmpegVideoPlayer? player = null;
        string? lastError = null;
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (profile.Options.HardwareAcceleration
                && _hardwareDecodeFailures.Contains(hardwareKey))
                continue;

            var candidate = new FfmpegVideoPlayer(
                runner,
                ffmpeg.ExecutablePath,
                path,
                width,
                height,
                effectiveMode,
                profile,
                videoInfo);
            try
            {
                if (await candidate.StartAsync(
                        profile.FirstFrameTimeout,
                        cancellationToken))
                {
                    player = candidate;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                candidate.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                LogWrapper.Debug(ex, "UI", $"启动视频背景失败：{path}");
            }

            lastError ??= candidate.LastError;
            if (profile.Options.HardwareAcceleration)
            {
                _hardwareDecodeFailures.Add(hardwareKey);
                LogWrapper.Info(
                    "UI",
                    $"视频背景硬件解码不可用，回退软件解码：{candidate.LastError}");
            }

            candidate.Dispose();
        }

        if (player is null)
        {
            LastError = lastError ?? "视频解码失败";
            LogWrapper.Debug(
                "UI",
                $"视频背景所有解码 profile 均失败：{path}，{LastError}");
            return false;
        }

        if (generation != Volatile.Read(ref _generation)
            || cancellationToken.IsCancellationRequested
            || _disposed)
        {
            player.Dispose();
            return cancellationToken.IsCancellationRequested
                ? throw new OperationCanceledException(cancellationToken)
                : false;
        }

        player.View.RendererUnavailable += OnRendererUnavailable;
        _player = player;
        _currentPath = path;
        LastError = string.Empty;
        ViewChanged?.Invoke();

        if (previous is not null)
        {
            previous.View.RendererUnavailable -= OnRendererUnavailable;
            try
            {
                await previous.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        LogWrapper.Info(
            "UI",
            $"已加载视频背景：{Path.GetFileName(path)}（{player.Profile.Id}，{player.View.PixelFormat}）");
        return true;
    }

    public async Task ResetAsync()
    {
        Interlocked.Increment(ref _generation);
        CancelReload();
        CancelSuspend();
        var player = _player;
        _player = null;
        _currentPath = null;
        LastError = string.Empty;
        ViewChanged?.Invoke();

        if (player is not null)
        {
            player.View.RendererUnavailable -= OnRendererUnavailable;
            await player.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Reset()
    {
        if (_disposed) return;
        Interlocked.Increment(ref _generation);
        CancelReload();
        CancelSuspend();
        var player = _player;
        _player = null;
        _currentPath = null;
        LastError = string.Empty;
        ViewChanged?.Invoke();

        if (player is null) return;
        player.View.RendererUnavailable -= OnRendererUnavailable;
        player.Dispose();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        var player = _player;
        if (player is null)
        {
            CancelSuspend();
            return;
        }

        if (paused)
        {
            player.Pause();
            ScheduleSuspend();
            return;
        }

        CancelSuspend();
        if (player.IsSuspended)
            _ = ResumePlayerAsync(player);
        else
            player.Resume();
    }

    public void ApplyHardwareAccelerationSetting()
    {
        _hardwareDecodeFailures.Clear();
        _ = ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        if (_disposed || _currentPath is not { } path || _player is null)
            return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _ = ReloadAsync());
            return;
        }

        var generation = Interlocked.Increment(ref _generation);
        var previousCts = _reloadCts;
        var cts = new CancellationTokenSource();
        _reloadCts = cts;
        try
        {
            previousCts?.Cancel();
        }
        catch
        {
            // ignored
        }

        try
        {
            var ffmpeg = await ProbeFfmpegAsync(false, cts.Token);
            if (!ffmpeg.IsAvailable
                || string.IsNullOrWhiteSpace(ffmpeg.ExecutablePath))
                return;

            await LoadAsync(path, ffmpeg, cts.Token, generation);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI", $"重新加载视频背景失败：{path}");
        }
        finally
        {
            if (ReferenceEquals(_reloadCts, cts))
                _reloadCts = null;
            cts.Dispose();
        }
    }

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        Config.Preference.Background.FfmpegPathConfig.Unobserve(_ffmpegPathObserver);
        Config.Preference.Background.VideoHardwareAccelerationConfig.Unobserve(
            _hardwareAccelerationObserver);
        CancelReload();
        CancelSuspend();

        var player = _player;
        _player = null;
        _currentPath = null;
        if (player is not null)
        {
            player.View.RendererUnavailable -= OnRendererUnavailable;
            await player.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<FfmpegProbeResult> _ProbeFfmpegAsync(
        FfmpegLocator locator,
        bool force,
        CancellationToken cancellationToken)
    {
        var result = await locator
            .DetectAsync(
                Config.Preference.Background.FfmpegPath,
                force,
                cancellationToken)
            .ConfigureAwait(false);
        LastFfmpegProbe = result;
        return result;
    }

    private async Task<VideoInfo?> ProbeVideoInfoAsync(
        string executablePath,
        string path,
        CancellationToken cancellationToken)
    {
        if (_locator?.CommandRunner is not { } runner) return null;
        _probe ??= new FfmpegVideoProbe(runner);
        return await _probe
            .ProbeInfoAsync(executablePath, path, cancellationToken)
            .ConfigureAwait(false);
    }

    private void OnRendererUnavailable()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnRendererUnavailable);
            return;
        }

        if (_yuvUnavailable) return;
        _yuvUnavailable = true;
        if (_player?.View.PixelFormat == FfmpegPixelFormat.Bgra8888)
            return;
        LogWrapper.Warn("UI", "YUV 视频渲染不可用，回退到 BGRA");
        _ = ReloadAsync();
    }

    private void ScheduleSuspend()
    {
        CancelSuspend();
        var cts = new CancellationTokenSource();
        _suspendCts = cts;
        _ = SuspendAfterDelayAsync(cts);
    }

    private void CancelSuspend()
    {
        var cts = _suspendCts;
        _suspendCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }
    }

    private void CancelReload()
    {
        var cts = _reloadCts;
        _reloadCts = null;
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }
    }

    private async Task SuspendAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(SuspendDelay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cts.Dispose();
            return;
        }

        Dispatcher.UIThread.Post(() => _ = SuspendIfNeededAsync(cts));
    }

    private async Task SuspendIfNeededAsync(CancellationTokenSource cts)
    {
        if (cts.IsCancellationRequested
            || _disposed
            || !_paused
            || _player is not { } player)
        {
            if (ReferenceEquals(_suspendCts, cts))
                _suspendCts = null;
            cts.Dispose();
            return;
        }

        try
        {
            await player.SuspendAsync().ConfigureAwait(false);
            LogWrapper.Info("UI", "视频背景已挂起并释放解码进程");
            if (ReferenceEquals(_player, player) && !_paused)
                Dispatcher.UIThread.Post(() => SetPaused(false));
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI", "挂起视频背景失败");
        }
        finally
        {
            if (ReferenceEquals(_suspendCts, cts))
                _suspendCts = null;
            cts.Dispose();
        }
    }

    private async Task ResumePlayerAsync(FfmpegVideoPlayer player)
    {
        if (!ReferenceEquals(_player, player)) return;
        try
        {
            if (await player.ResumeAsync(player.FirstFrameTimeout).ConfigureAwait(false))
            {
                if (ReferenceEquals(_player, player) && _paused)
                    player.Pause();
                return;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI", "恢复视频背景失败");
        }

        if (!ReferenceEquals(_player, player)) return;
        if (player.Profile.Options.HardwareAcceleration
            && _currentPath is { } currentPath)
            _hardwareDecodeFailures.Add(GetVideoKey(currentPath));
        LogWrapper.Warn("UI", "视频背景恢复失败，尝试重新加载");
        await ReloadAsync().ConfigureAwait(false);
    }

    internal static VideoInfo? NormalizeVideoInfoForOutput(VideoInfo? videoInfo)
    {
        return videoInfo?.PixelFormat is not { } pixelFormat
            || !IsRgbPixelFormat(pixelFormat)
            || !string.Equals(videoInfo.ColorRange, "pc", StringComparison.OrdinalIgnoreCase)
                ? videoInfo
                : videoInfo with
                {
                    ColorRange = "tv",
                    ColorSpace = "bt601"
                };
    }

    private static bool IsRgbPixelFormat(string pixelFormat)
    {
        return StartsWith("rgb")
            || StartsWith("bgr")
            || StartsWith("gbr")
            || StartsWith("argb")
            || StartsWith("abgr")
            || StartsWith("rgba")
            || StartsWith("bgra")
            || StartsWith("0rgb")
            || StartsWith("0bgr");

        bool StartsWith(string prefix)
        {
            return pixelFormat.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static (int Width, int Height) GetVideoTargetSize(BackgroundViewport viewport)
    {
        var width = viewport.Width > 0d ? viewport.Width : 850d;
        var height = viewport.Height > 0d ? viewport.Height : 450d;
        var scaling = viewport.Scaling > 0d ? viewport.Scaling : 1d;
        var targetWidth = Math.Max(2, (int)Math.Round(width * scaling));
        var targetHeight = Math.Max(2, (int)Math.Round(height * scaling));

        var scale = Math.Min(
            1d,
            Math.Min(
                MaxVideoWidth / (double)targetWidth,
                MaxVideoHeight / (double)targetHeight));
        targetWidth = Math.Max(2, (int)Math.Round(targetWidth * scale));
        targetHeight = Math.Max(2, (int)Math.Round(targetHeight * scale));
        return (MakeEven(targetWidth), MakeEven(targetHeight));
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
        return (MakeEven(width), MakeEven(height));
    }

    private static string GetVideoKey(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return $"{path}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        }
        catch
        {
            return path;
        }
    }

    private static int MakeEven(int value)
    {
        return (value & 1) == 0 ? value : value + 1;
    }
}
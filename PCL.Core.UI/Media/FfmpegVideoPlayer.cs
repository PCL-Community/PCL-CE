using System.Text;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.Platform;

namespace PCL.Core.UI.Media;

/// <summary>通过 FFmpeg 解码视频，并将原始帧写入背景视图。</summary>
public sealed class FfmpegVideoPlayer : IDisposable
{
    private readonly string _executablePath;
    private readonly int _frameSize;
    private readonly int _height;
    private readonly IExternalCommandRunner _runner;
    private readonly StringBuilder _stderr = new();
    private readonly Lock _sync = new();
    private readonly string _videoPath;
    private readonly int _width;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _failureReported;
    private TaskCompletionSource<bool>? _firstFrame;
    private bool _hasPending;
    private string _lastError = string.Empty;
    private TaskCompletionSource<bool>? _pauseGate;
    private bool _paused;
    private VideoFrameBuffer? _pendingFrame;
    private int _renderQueued;
    private Task? _runTask;
    private IExternalCommandSession? _session;
    private bool _started;
    private bool _stopRequested;
    private bool _suspended;

    public FfmpegVideoPlayer(
        IExternalCommandRunner runner,
        string executablePath,
        string videoPath,
        int width,
        int height,
        VideoSuitMode suitMode,
        FfmpegPlaybackProfile profile,
        VideoInfo? videoInfo)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _executablePath = executablePath;
        _videoPath = videoPath;
        _width = _MakeEven(Math.Max(2, width));
        _height = _MakeEven(Math.Max(2, height));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _frameSize = profile.Options.PixelFormat == FfmpegPixelFormat.Yuv420p
            ? _width * _height * 3 / 2
            : _width * _height * 4;
        View = profile.Options.PixelFormat == FfmpegPixelFormat.Yuv420p
            ? new YuvVideoFrameView(
                _width,
                _height,
                suitMode,
                videoInfo?.ColorRange,
                videoInfo?.ColorSpace)
            : new VideoFrameView(_width, _height, suitMode);
    }

    /// <summary>用于显示视频帧的视图。</summary>
    public VideoSurface View { get; }

    /// <summary>当前播放使用的 profile。</summary>
    public FfmpegPlaybackProfile Profile { get; }

    /// <summary>当前 profile 的首帧等待时间。</summary>
    public TimeSpan FirstFrameTimeout => Profile.FirstFrameTimeout;

    /// <summary>最近一次播放错误；没有错误时为空字符串。</summary>
    public string LastError
    {
        get
        {
            lock (_sync)
            {
                return _lastError;
            }
        }
        private set
        {
            lock (_sync)
            {
                _lastError = value;
            }
        }
    }

    /// <summary>当前是否已暂停。</summary>
    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _paused;
            }
        }
    }

    /// <summary>解码进程是否已挂起；挂起时最后一帧仍保留。</summary>
    public bool IsSuspended
    {
        get
        {
            lock (_sync)
            {
                return _suspended;
            }
        }
    }

    private bool IsStarted
    {
        get
        {
            lock (_sync)
            {
                return _started;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopSessionAsync(false).GetAwaiter().GetResult();
        ClearPendingFrame();

        if (Dispatcher.UIThread.CheckAccess())
            View.Dispose();
        else
            Dispatcher.UIThread.Post(() => View.Dispose());
        GC.SuppressFinalize(this);
    }

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopSessionAsync(false).ConfigureAwait(false);
        ClearPendingFrame();

        if (Dispatcher.UIThread.CheckAccess())
            View.Dispose();
        else
            await Dispatcher.UIThread.InvokeAsync(() => View.Dispose());
        GC.SuppressFinalize(this);
    }

    /// <summary>启动解码并等待首帧。</summary>
    public Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        return StartAsync(Profile.FirstFrameTimeout, cancellationToken);
    }

    /// <summary>启动解码并等待首帧。</summary>
    public async Task<bool> StartAsync(
        TimeSpan firstFrameTimeout,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        if (IsSuspended) return await ResumeAsync(firstFrameTimeout, cancellationToken);
        if (IsStarted) return true;
        return await StartSessionAsync(firstFrameTimeout, cancellationToken);
    }

    /// <summary>暂停读取新帧。</summary>
    public void Pause()
    {
        lock (_sync)
        {
            if (_disposed || _suspended || _stopRequested || _paused) return;
            _paused = true;
            _pauseGate = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>恢复读取新帧。</summary>
    public void Resume()
    {
        TaskCompletionSource<bool>? gate;
        lock (_sync)
        {
            if (_disposed || _suspended || _stopRequested || !_paused) return;
            _paused = false;
            gate = _pauseGate;
            _pauseGate = null;
        }

        gate?.TrySetResult(true);
    }

    /// <summary>停止解码进程，但保留最后一帧和视图。</summary>
    public async Task SuspendAsync()
    {
        if (_disposed) return;
        if (IsSuspended) return;

        await StopSessionAsync(true).ConfigureAwait(false);
        LogWrapper.Info("Media", "视频背景已挂起并释放解码进程");
    }

    /// <summary>从挂起状态重新启动解码。</summary>
    public async Task<bool> ResumeAsync(
        TimeSpan firstFrameTimeout,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        if (!IsSuspended) return IsStarted;

        lock (_sync)
        {
            _suspended = false;
            _paused = false;
        }

        return await StartSessionAsync(firstFrameTimeout, cancellationToken);
    }

    /// <summary>立即终止解码进程，并等待其退出。</summary>
    public void Stop()
    {
        StopSessionAsync(false).GetAwaiter().GetResult();
    }

    /// <summary>终止解码进程并等待其退出。</summary>
    public async Task StopAsync()
    {
        await StopSessionAsync(false).ConfigureAwait(false);
    }

    private async Task<bool> StartSessionAsync(
        TimeSpan firstFrameTimeout,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource cts;
        TaskCompletionSource<bool> firstFrame;
        Task runTask;
        lock (_sync)
        {
            if (_disposed) return false;
            if (_started) return true;

            _stopRequested = false;
            _started = true;
            _paused = false;
            _pauseGate = null;
            _lastError = string.Empty;
            _failureReported = 0;
            _firstFrame = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            firstFrame = _firstFrame;
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;
            runTask = Task.Run(() => _RunAsync(cts), CancellationToken.None);
            _runTask = runTask;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            timeoutCts.CancelAfter(firstFrameTimeout);
            var timeoutTask = Task.Delay(Timeout.Infinite, timeoutCts.Token);
            var completed = await Task
                .WhenAny(firstFrame.Task, timeoutTask)
                .ConfigureAwait(false);
            if (completed == firstFrame.Task && await firstFrame.Task.ConfigureAwait(false))
            {
                timeoutCts.Cancel();
                return true;
            }

            if (!cancellationToken.IsCancellationRequested
                && !_stopRequested
                && string.IsNullOrWhiteSpace(LastError))
                _SetError("等待视频首帧超时");
            await StopSessionAsync(false).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            await StopSessionAsync(false).ConfigureAwait(false);
            return false;
        }
        finally
        {
            _ = runTask;
        }
    }

    private (CancellationTokenSource? Cts, IExternalCommandSession? Session,
        TaskCompletionSource<bool>? Gate, Task? RunTask)
        DetachSession(bool keepSuspended)
    {
        _stopRequested = true;
        lock (_sync)
        {
            var cts = _cts;
            _cts = null;
            var session = _session;
            _session = null;
            var gate = _pauseGate;
            _pauseGate = null;
            var runTask = _runTask;
            _runTask = null;
            _started = false;
            _paused = false;
            _suspended = keepSuspended;
            return (cts, session, gate, runTask);
        }
    }

    private async Task<bool> StopSessionAsync(bool keepSuspended)
    {
        var (cts, session, gate, runTask) = DetachSession(keepSuspended);
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }

        gate?.TrySetResult(true);
        if (session is not null)
        {
            session.Kill();
            try
            {
                await session
                    .WaitForExitAsync()
                    .WaitAsync(TimeSpan.FromSeconds(1))
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            session.Dispose();
        }

        if (runTask is not null)
        {
            try
            {
                await runTask
                    .WaitAsync(TimeSpan.FromSeconds(1))
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            return runTask.IsCompleted;
        }

        return true;
    }

    private async Task _RunAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_stopRequested)
            {
                IExternalCommandSession? session = null;
                try
                {
                    session = _runner.Start(
                        _executablePath,
                        FfmpegCommandBuilder.BuildPlaybackArguments(
                            _videoPath,
                            _width,
                            _height,
                            Profile.Options));

                    lock (_sync)
                    {
                        if (_stopRequested)
                        {
                            session.Kill();
                            return;
                        }

                        _session = session;
                        _stderr.Clear();
                    }

                    var stderrTask = Task.Run(
                        () => _ReadStderrAsync(session, cancellationToken),
                        CancellationToken.None);
                    var frames = await _ReadFramesAsync(session, cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        await session.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    try
                    {
                        await stderrTask
                            .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    var exitCode = _TryGetExitCode(session);
                    lock (_sync)
                    {
                        if (ReferenceEquals(_session, session)) _session = null;
                    }

                    if (cancellationToken.IsCancellationRequested || _stopRequested)
                        return;

                    if (frames == 0)
                    {
                        _SetError(_ReadStderr());
                        if (string.IsNullOrWhiteSpace(LastError))
                            _SetError("视频解码失败");
                        return;
                    }

                    if (exitCode != 0)
                    {
                        _SetError(_ReadStderr());
                        if (string.IsNullOrWhiteSpace(LastError))
                            _SetError($"FFmpeg 进程异常退出：{exitCode}");
                        return;
                    }

                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested && !_stopRequested)
                        _SetError(ex.Message);
                    return;
                }
                finally
                {
                    session?.Dispose();
                }
            }
        }
        finally
        {
            try
            {
                cts.Dispose();
            }
            catch
            {
            }
        }
    }

    private async Task<int> _ReadFramesAsync(
        IExternalCommandSession session,
        CancellationToken cancellationToken)
    {
        var frames = 0;
        var stream = session.StandardOutput;
        while (!cancellationToken.IsCancellationRequested && !_stopRequested)
        {
            try
            {
                await _WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var frame = VideoSurface.RentFrameBuffer(_frameSize);
            var transferred = false;
            try
            {
                var total = 0;
                while (total < _frameSize)
                {
                    var read = await stream
                        .ReadAsync(
                            frame.Data.AsMemory(total, _frameSize - total),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                }

                if (total < _frameSize) break;

                lock (_sync)
                {
                    if (_disposed || _stopRequested) break;

                    var previous = _pendingFrame;
                    _pendingFrame = frame;
                    _hasPending = true;
                    transferred = true;
                    previous?.Dispose();
                }

                frames++;
                _firstFrame?.TrySetResult(true);
                _QueueRender();
            }
            catch (Exception ex) when (
                ex is OperationCanceledException
                    or IOException
                    or ObjectDisposedException)
            {
                break;
            }
            finally
            {
                if (!transferred)
                    frame.Dispose();
            }
        }

        return frames;
    }

    private async Task _WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask;
        lock (_sync)
        {
            if (!_paused)
                return;

            if (_firstFrame is { Task.IsCompleted: false })
                return;

            waitTask = _pauseGate?.Task;
        }

        if (waitTask is not null)
            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task _ReadStderrAsync(
        IExternalCommandSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(
                session.StandardError,
                Encoding.UTF8,
                false,
                1024,
                true);
            var text = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return;
            lock (_sync)
            {
                if (_stderr.Length > 6000) return;
                _stderr.Append(text);
            }
        }
        catch
        {
            // ignored
        }
    }

    private void _QueueRender()
    {
        if (_disposed || _stopRequested) return;
        if (Interlocked.Exchange(ref _renderQueued, 1) != 0) return;
        Dispatcher.UIThread.Post(_RenderLatest, DispatcherPriority.Render);
    }

    private void _RenderLatest()
    {
        VideoFrameBuffer? frame = null;
        try
        {
            if (_disposed) return;
            lock (_sync)
            {
                if (_disposed || _stopRequested || !_hasPending) return;
                _hasPending = false;
                frame = _pendingFrame;
                _pendingFrame = null;
            }

            if (frame is not null)
                View.Present(frame);
        }
        catch (Exception ex)
        {
            frame?.Dispose();
            _SetError(ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            bool again;
            lock (_sync)
            {
                again = _hasPending && !_disposed && !_stopRequested;
            }

            if (again) _QueueRender();
        }
    }

    private void ClearPendingFrame()
    {
        VideoFrameBuffer? frame;
        lock (_sync)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
            _hasPending = false;
        }

        frame?.Dispose();
    }

    private void _SetError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        LastError = message.Trim();
        _firstFrame?.TrySetResult(false);
        if (Interlocked.Exchange(ref _failureReported, 1) != 0) return;

        LogWrapper.Info("Media", $"视频背景播放失败：{message.Trim()}");
    }

    private string _ReadStderr()
    {
        lock (_sync)
        {
            return _stderr.ToString().Trim();
        }
    }

    private static int _TryGetExitCode(IExternalCommandSession session)
    {
        try
        {
            return session.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static int _MakeEven(int value)
    {
        return (value & 1) == 0 ? value : value + 1;
    }
}
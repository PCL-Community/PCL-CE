using System.Text;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.Platform;

namespace PCL.Core.UI.Media;

/// <summary>通过 FFmpeg 解码视频，并将原始帧写入背景视图。</summary>
public sealed class FfmpegVideoPlayer : IDisposable
{
    private const int TargetFps = 30;
    private const int FrameTimeoutSeconds = 15;

    private readonly string _executablePath;

    private readonly TaskCompletionSource<bool> _firstFrame =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly int _frameSize;
    private readonly int _height;

    private readonly TaskCompletionSource<bool> _runCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly IExternalCommandRunner _runner;
    private readonly StringBuilder _stderr = new();
    private readonly Lock _sync = new();
    private readonly string _videoPath;
    private readonly int _width;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private int _failureReported;
    private bool _hasPending;
    private TaskCompletionSource<bool>? _pauseGate;
    private bool _paused;
    private byte[] _pendingBuffer;
    private byte[] _readBuffer;
    private int _renderQueued;
    private IExternalCommandSession? _session;
    private bool _started;
    private volatile bool _stopRequested;

    public FfmpegVideoPlayer(
        IExternalCommandRunner runner,
        string executablePath,
        string videoPath,
        int width,
        int height,
        VideoSuitMode suitMode)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _executablePath = executablePath;
        _videoPath = videoPath;
        _width = _MakeEven(Math.Max(2, width));
        _height = _MakeEven(Math.Max(2, height));
        _frameSize = _width * _height * 4;
        _readBuffer = new byte[_frameSize];
        _pendingBuffer = new byte[_frameSize];
        View = new VideoFrameView(_width, _height, suitMode);
    }

    /// <summary>用于显示视频帧的视图。</summary>
    public VideoFrameView View { get; }

    /// <summary>最近一次播放错误；没有错误时为空字符串。</summary>
    public string LastError { get; private set; } = string.Empty;

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        View.Dispose();
    }

    /// <summary>启动解码并等待首帧。</summary>
    /// <returns>在超时前成功取得首帧时返回 <see langword="true" />。</returns>
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cts;
        lock (_sync)
        {
            if (_disposed || _stopRequested) return false;
            if (_started)
                return _firstFrame.Task is { IsCompletedSuccessfully: true, Result: true };

            _started = true;
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;
        }

        _ = Task.Run(() => _RunAsync(cts), CancellationToken.None);

        try
        {
            var timeout = Task.Delay(
                TimeSpan.FromSeconds(FrameTimeoutSeconds),
                cts.Token);
            var completed = await Task.WhenAny(_firstFrame.Task, timeout);
            if (completed == _firstFrame.Task && await _firstFrame.Task)
                return true;

            if (!cancellationToken.IsCancellationRequested
                && !_stopRequested
                && string.IsNullOrWhiteSpace(LastError))
                _SetError("等待视频首帧超时");
            Stop();
            return false;
        }
        catch (OperationCanceledException)
        {
            Stop();
            return false;
        }
    }

    /// <summary>暂停读取新帧。</summary>
    public void Pause()
    {
        lock (_sync)
        {
            if (_disposed || _stopRequested || _paused) return;
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
            if (_disposed || _stopRequested || !_paused) return;
            _paused = false;
            gate = _pauseGate;
            _pauseGate = null;
        }

        gate?.TrySetResult(true);
    }

    /// <summary>立即终止解码进程。</summary>
    public void Stop()
    {
        var (cts, session, gate) = _Detach();
        try
        {
            cts?.Cancel();
        }
        catch
        {
        }

        gate?.TrySetResult(true);
        session?.Kill();
        session?.Dispose();
    }

    /// <summary>终止解码进程并等待其退出。</summary>
    public async Task StopAsync()
    {
        var (cts, session, gate) = _Detach();
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

        if (!_started) return;
        try
        {
            await _runCompleted
                .Task
                .WaitAsync(TimeSpan.FromSeconds(1))
                .ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private (CancellationTokenSource? Cts, IExternalCommandSession? Session,
        TaskCompletionSource<bool>? Gate)
        _Detach()
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
            _paused = false;
            return (cts, session, gate);
        }
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
                            TargetFps));

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
                            .WaitAsync(
                                TimeSpan.FromSeconds(1),
                                cancellationToken)
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
            lock (_sync)
            {
                if (ReferenceEquals(_cts, cts)) _cts = null;
            }

            _runCompleted.TrySetResult(true);
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

            var total = 0;
            try
            {
                while (total < _frameSize)
                {
                    var read = await stream
                        .ReadAsync(
                            _readBuffer.AsMemory(total, _frameSize - total),
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (total == 0) break;
            if (total < _frameSize) break;

            lock (_sync)
            {
                (_pendingBuffer, _readBuffer) = (_readBuffer, _pendingBuffer);
                _hasPending = true;
            }

            frames++;
            _firstFrame.TrySetResult(true);
            _QueueRender();
        }

        return frames;
    }

    private async Task _WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask;
        lock (_sync)
        {
            if (!_paused) return;
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
        try
        {
            if (_disposed) return;
            lock (_sync)
            {
                if (_disposed || !_hasPending) return;
                _hasPending = false;
                View.Present(_pendingBuffer);
            }
        }
        catch (Exception ex)
        {
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

    private void _SetError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        LastError = message.Trim();
        _firstFrame.TrySetResult(false);
        if (Interlocked.Exchange(ref _failureReported, 1) != 0) return;

        LogWrapper.Info("Media", $"视频背景播放失败：{LastError}");
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
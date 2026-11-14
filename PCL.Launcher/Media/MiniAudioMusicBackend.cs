using Avalonia.Threading;
using MiniAudioEx.Core.StandardAPI;

namespace PCL.Media;

/// <summary>单个音频后端的最小能力集。</summary>
internal interface IMusicBackend : IDisposable
{
    MusicPlaybackState State { get; }

    event EventHandler? PlaybackEnded;
    event EventHandler<Exception>? PlaybackFailed;

    bool Play(string path, int volume, bool startPaused);
    void Pause();
    void Resume();
    void Stop();
    void SetVolume(int volume);
}

/// <summary>
///     MiniAudioEx 音乐后端：Windows/Linux/macOS 共用，支持 WAV/MP3/FLAC/OGG。
///     MiniAudio 的解码/混音在 native 层完成，AudioContext.Update 负责把结束回调送回主线程。
/// </summary>
internal sealed class MiniAudioMusicBackend : IMusicBackend
{
    private const int SampleRate = 44100;
    private const int Channels = 2;
    private const int UpdateIntervalMilliseconds = 50;

    private static readonly Lock EngineLock = new();
    private static int _engineReferences;
    private readonly AudioSource _source;

    private readonly DispatcherTimer _updateTimer;
    private AudioClip? _clip;
    private bool _disposed;
    private ulong _pausedCursor;
    private MusicPlaybackState _state = MusicPlaybackState.Stop;

    public MiniAudioMusicBackend()
    {
        lock (EngineLock)
        {
            if (_engineReferences == 0)
                AudioContext.Initialize(SampleRate, Channels);
            _engineReferences++;
        }

        _source = new AudioSource();
        _source.End += OnPlaybackEnded;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(UpdateIntervalMilliseconds)
        };
        _updateTimer.Tick += (_, _) => AudioContext.Update();
        if (Dispatcher.UIThread.CheckAccess())
            _updateTimer.Start();
        else
            Dispatcher.UIThread.Post(() => _updateTimer.Start());
    }

    public MusicPlaybackState State
    {
        get
        {
            if (_disposed) return MusicPlaybackState.Stop;
            if (_state == MusicPlaybackState.Play && !_source.IsPlaying)
                _state = MusicPlaybackState.Stop;
            return _state;
        }
    }

    public event EventHandler? PlaybackEnded;
    public event EventHandler<Exception>? PlaybackFailed;

    public bool Play(string path, int volume, bool startPaused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            StopInternal();
            _clip = new AudioClip(path);
            _source.Volume = Math.Clamp(volume / 1000f, 0f, 1f);
            _source.Play(_clip);
            _state = MusicPlaybackState.Play;

            if (startPaused)
            {
                _pausedCursor = 0;
                _source.Stop();
                _state = MusicPlaybackState.Pause;
            }

            return true;
        }
        catch
        {
            StopInternal();
            return false;
        }
    }

    public void Pause()
    {
        if (_disposed || _state != MusicPlaybackState.Play) return;
        try
        {
            _pausedCursor = _source.Cursor;
            _source.Stop();
            _state = MusicPlaybackState.Pause;
        }
        catch
        {
            // 设备切换失败时由下一次刷新兜底。
        }
    }

    public void Resume()
    {
        if (_disposed || _state != MusicPlaybackState.Pause || _clip is null) return;
        try
        {
            _source.Play(_clip);
            _source.Cursor = _pausedCursor;
            _state = MusicPlaybackState.Play;
        }
        catch (Exception ex)
        {
            PlaybackFailed?.Invoke(this, ex);
        }
    }

    public void Stop()
    {
        if (_disposed) return;
        StopInternal();
    }

    public void SetVolume(int volume)
    {
        if (_disposed) return;
        try
        {
            _source.Volume = Math.Clamp(volume / 1000f, 0f, 1f);
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _updateTimer.Stop();
        _source.End -= OnPlaybackEnded;
        StopInternal();

        try
        {
            _source.Dispose();
        }
        catch
        {
            // ignored
        }

        lock (EngineLock)
        {
            if (_engineReferences == 0) return;
            _engineReferences--;
            if (_engineReferences == 0)
                AudioContext.Deinitialize();
        }
    }

    private void OnPlaybackEnded()
    {
        if (_disposed || _state != MusicPlaybackState.Play) return;
        _state = MusicPlaybackState.Stop;
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    private void StopInternal()
    {
        try
        {
            _source.Stop();
        }
        catch
        {
            // ignored
        }

        _state = MusicPlaybackState.Stop;
        _pausedCursor = 0;
        _clip?.Dispose();
        _clip = null;
    }
}
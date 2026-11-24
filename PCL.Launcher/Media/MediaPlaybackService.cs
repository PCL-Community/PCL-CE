using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Hints;

namespace PCL.Media;

/// <summary>
///     背景音乐服务：负责播放列表、播放状态、音量与失败回退。
///     使用 MiniAudioEx 作为跨平台音频后端。
/// </summary>
public sealed class MediaPlaybackService : IMediaService, IDisposable
{
    private const string Module = "Media";

    private static readonly string[] IgnoredExtensions =
        [".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png"];

    private readonly Lock _backendOperation = new();

    private readonly Lock _sync = new();

    private List<string>? _all;
    private IMusicBackend? _backend;
    private string? _current;
    private bool _disposed;
    private long _generation;
    private bool _handlingFailure;
    private bool _volumeObserved;
    private ConfigObserver? _volumeObserver;
    private List<string>? _waiting;

    /// <summary>播放列表是否非空；用于音乐快捷按钮显隐，不会初始化音频后端。</summary>
    public bool IsMusicVisible
    {
        get
        {
            lock (_sync)
            {
                EnsureListLocked();
                return _all is { Count: > 0 };
            }
        }
    }

    public void Dispose()
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _generation++;
            _all = null;
            _waiting = null;
            _current = null;
            backend = DetachBackendLocked();
        }

        StopAndDisposeBackend(backend);

        if (_volumeObserver is not { } observer)
            return;

        try
        {
            Config.Preference.Music.VolumeConfig.Unobserve(observer);
        }
        catch
        {
            // ignored
        }
    }

    public bool IsAvailable => IsMusicAvailable;

    public bool IsMusicAvailable
    {
        get
        {
            lock (_sync)
            {
                return EnsureBackendLocked() is not null;
            }
        }
    }

    public MusicPlaybackState MusicState
    {
        get
        {
            IMusicBackend? backend;
            lock (_sync)
            {
                backend = _backend;
            }

            return backend?.State ?? MusicPlaybackState.Stop;
        }
    }

    public int MusicCount
    {
        get
        {
            lock (_sync)
            {
                EnsureListLocked();
                return _all?.Count ?? 0;
            }
        }
    }

    public string? CurrentMusicPath
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public IReadOnlyList<string> MusicList
    {
        get
        {
            lock (_sync)
            {
                EnsureListLocked();
                return _all is null ? Array.Empty<string>() : _all.ToArray();
            }
        }
    }

    public void PauseMusic()
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            backend = _backend;
        }

        if (backend?.State != MusicPlaybackState.Play)
        {
            LogWrapper.Info(Module, $"[Music] 无需暂停播放，当前状态为 {MusicState}");
            return;
        }

        lock (_backendOperation)
        {
            backend.Pause();
        }

        LogWrapper.Info(Module, "[Music] 已暂停播放");
    }

    public void ResumeMusic()
    {
        IMusicBackend? backend;
        string? current;
        long generation;
        lock (_sync)
        {
            backend = _backend;
            current = _current;
            generation = _generation;
        }

        if (current is null || backend is null)
        {
            LogWrapper.Info(Module, "[Music] 无需继续播放：当前没有音乐");
            return;
        }

        switch (backend.State)
        {
            case MusicPlaybackState.Pause:
            {
                lock (_backendOperation)
                {
                    backend.Resume();
                }

                LogWrapper.Info(Module, "[Music] 已恢复播放");
                return;
            }

            case MusicPlaybackState.Stop:
            {
                lock (_sync)
                {
                    generation = ++_generation;
                }

                _ = Task.Run(() => StartPlay(current, false, generation));
                break;
            }

            case MusicPlaybackState.Play:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void ToggleMusic()
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            backend = _backend;
        }

        if (backend is null)
        {
            HintService.Show(Hints.Error(Lang.Text("Music.Error.NotStarted")));
            return;
        }

        switch (backend.State)
        {
            case MusicPlaybackState.Pause:
                ResumeMusic();
                break;

            case MusicPlaybackState.Play:
                PauseMusic();
                break;

            case MusicPlaybackState.Stop:
            default:
                LogWrapper.Debug(Module, "[Music] 音乐处于停止状态，尝试重新开始播放");
                RefreshMusic(false);
                break;
        }
    }

    public void PlayNextMusic()
    {
        _ = Task.Run(() => PlayNextInternal(true));
    }

    public void RefreshMusic(bool showHint, bool isFirstLoad = false)
    {
        long generation;
        lock (_sync)
        {
            if (_disposed) return;
            generation = ++_generation;
        }

        _ = Task.Run(() => RefreshMusicCore(generation, showHint, isFirstLoad));
    }

    public void ClearMusic()
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            _generation++;
            _all = [];
            _waiting = [];
            _current = null;
            backend = DetachBackendLocked();
        }

        StopAndDisposeBackend(backend);
    }

    public void SetMusicVolume(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 1000);
        IMusicBackend? backend;
        lock (_sync)
        {
            backend = _backend;
        }

        if (backend is null) return;
        lock (_backendOperation)
        {
            backend.SetVolume(clamped);
        }
    }

    private void RefreshMusicCore(long generation, bool showHint, bool isFirstLoad)
    {
        try
        {
            int count;
            bool hadBackend;
            string? next;

            lock (_sync)
            {
                if (_disposed || generation != _generation) return;
                hadBackend = _backend is not null;
                EnsureListLocked(true);
                count = _all?.Count ?? 0;
                if (count == 0)
                {
                    _waiting = [];
                    _current = null;
                    next = null;
                }
                else
                {
                    next = DequeueNextLocked();
                }
            }

            if (count == 0)
            {
                StopBackend();
                if (showHint)
                    HintService.Show(
                        hadBackend
                            ? Hints.Success(Lang.Text("Music.Hint.Cleared"))
                            : Hints.Error(Lang.Text("Music.Error.NotDetected")));
                return;
            }

            if (next is null)
            {
                if (showHint) HintService.Show(Hints.Error(Lang.Text("Music.Error.NoAvailable")));
                return;
            }

            if (StartPlay(next, isFirstLoad, generation) && showHint)
                HintService.Show(
                    Hints.Success(
                        Lang.Text("Music.Hint.Refreshed", Path.GetFileName(next)),
                        false));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, Module, "[Music] 刷新背景音乐播放失败");
            if (showHint)
                HintService.Show(Hints.Error(Lang.Text("Music.Error.OperationFailed")));
        }
    }

    private void PlayNextInternal(bool showHint)
    {
        try
        {
            string? next;
            bool single;
            long generation;

            lock (_sync)
            {
                if (_disposed) return;
                EnsureListLocked();
                var count = _all?.Count ?? 0;
                if (count == 0)
                {
                    if (showHint)
                        HintService.Show(Hints.Error(Lang.Text("Music.Error.NoAvailable")));
                    return;
                }

                single = count == 1;
                next = single ? _current ?? _all![0] : DequeueNextLocked();
                if (next is null)
                {
                    if (showHint)
                        HintService.Show(Hints.Error(Lang.Text("Music.Error.NoAvailable")));
                    return;
                }

                generation = ++_generation;
            }

            if (StartPlay(next, false, generation) && showHint)
                HintService.Show(
                    Hints.Success(
                        single
                            ? Lang.Text("Music.Hint.Replaying", Path.GetFileName(next))
                            : Lang.Text("Music.Hint.Playing", Path.GetFileName(next))));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, Module, "[Music] 播放下一首失败");
            if (showHint)
                HintService.Show(Hints.Error(Lang.Text("Music.Error.OperationFailed")));
        }
    }

    private bool StartPlay(string path, bool isFirstLoad, long generation)
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            if (_disposed || generation != _generation) return false;
            backend = EnsureBackendLocked();
            if (backend is null)
            {
                _current = null;
                return false;
            }

            _current = path;
        }

        var volume = ReadVolume();
        var startPaused = isFirstLoad && !ReadStartOnStartup();
        bool started;
        lock (_backendOperation)
        {
            started = backend.Play(path, volume, startPaused);
        }

        if (!started)
        {
            LogWrapper.Warn(Module, $"[Music] 播放失败：{path}");
            lock (_sync)
            {
                if (generation == _generation
                    && string.Equals(_current, path, StringComparison.Ordinal))
                    _current = null;
                RemoveTrackLocked(path);
            }

            HintService.Show(
                Hints.Error(Lang.Text("Music.Error.PlaybackFailed", Path.GetFileName(path))));

            _ = Task.Run(() => PlayNextInternal(false));
            return false;
        }

        LogWrapper.Info(Module, $"[Music] 播放开始：{path}");
        return true;
    }

    private IMusicBackend? EnsureBackendLocked()
    {
        if (_disposed) return null;
        if (_backend is not null) return _backend;

        EnsureVolumeObserverLocked();

        try
        {
            var miniAudio = new MiniAudioMusicBackend();
            AttachBackendLocked(miniAudio);
            _backend = miniAudio;
            LogWrapper.Info(Module, "[Music] 使用 MiniAudioEx 音频后端");
            return miniAudio;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, Module, "[Music] MiniAudioEx 音频后端初始化失败");
            return null;
        }
    }

    private void AttachBackendLocked(IMusicBackend backend)
    {
        backend.PlaybackEnded += OnBackendPlaybackEnded;
        backend.PlaybackFailed += OnBackendPlaybackFailed;
    }

    private void OnBackendPlaybackEnded(object? sender, EventArgs e)
    {
        if (sender is not IMusicBackend backend) return;
        lock (_sync)
        {
            if (_disposed || !ReferenceEquals(_backend, backend) || _handlingFailure) return;
            _handlingFailure = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                PlayNextInternal(false);
            }
            finally
            {
                lock (_sync)
                {
                    _handlingFailure = false;
                }
            }
        });
    }

    private void OnBackendPlaybackFailed(object? sender, Exception exception)
    {
        if (sender is not IMusicBackend backend) return;

        string? path;
        lock (_sync)
        {
            if (_disposed || !ReferenceEquals(_backend, backend) || _handlingFailure) return;
            _handlingFailure = true;
            path = _current;
            if (path is not null) RemoveTrackLocked(path);
            _current = null;
        }

        LogWrapper.Warn(
            exception,
            Module,
            $"[Music] 播放出现内部错误（{path ?? "未知文件"}）");
        if (path is not null)
            HintService.Show(
                Hints.Error(Lang.Text("Music.Error.PlaybackFailed", Path.GetFileName(path))));

        _ = Task.Run(() =>
        {
            try
            {
                PlayNextInternal(false);
            }
            finally
            {
                lock (_sync)
                {
                    _handlingFailure = false;
                }
            }
        });
    }

    private void EnsureVolumeObserverLocked()
    {
        if (_volumeObserved) return;
        _volumeObserved = true;

        try
        {
            _volumeObserver = new ConfigObserver(
                ConfigEvent.Update,
                _ => SetMusicVolume(ReadVolume()));
            Config.Preference.Music.VolumeConfig.Observe(_volumeObserver);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, Module, "[Music] 订阅音量设置失败");
            _volumeObserver = null;
            _volumeObserved = false;
        }
    }

    private void EnsureListLocked(bool forceReload = false)
    {
        if (!forceReload && _all is not null) return;
        _all = EnumerateMusicFiles();
        _waiting = BuildWaitingList(_all, null);
    }

    private List<string> ReloadListLocked(bool forceReload, string? preventFirst = null)
    {
        if (forceReload || _all is null) _all = EnumerateMusicFiles();
        _waiting = BuildWaitingList(_all, preventFirst);
        return _all;
    }

    private static List<string> BuildWaitingList(List<string> all, string? preventFirst)
    {
        var waiting = ReadShuffle()
            ? all.OrderBy(_ => Random.Shared.Next()).ToList()
            : new List<string>(all);

        if (preventFirst is null
            || waiting.Count <= 0
            || !string.Equals(
                waiting[0],
                preventFirst,
                StringComparison.OrdinalIgnoreCase))
            return waiting;
        waiting.RemoveAt(0);
        waiting.Add(preventFirst);

        return waiting;
    }

    private string? DequeueNextLocked()
    {
        if (_all is null)
            ReloadListLocked(true);

        if (_all is null || _all.Count == 0)
            return null;

        if (_waiting is null || _waiting.Count == 0)
            ReloadListLocked(false, _current);

        if (_waiting is not { Count: > 0 })
            return null;

        var next = _waiting[0];
        _waiting.RemoveAt(0);
        if (_waiting.Count == 0) ReloadListLocked(false, next);
        return next;
    }

    private void RemoveTrackLocked(string path)
    {
        _all?.RemoveAll(file => string.Equals(file, path, StringComparison.Ordinal));
        _waiting?.RemoveAll(file => string.Equals(file, path, StringComparison.Ordinal));
    }

    private IMusicBackend? DetachBackendLocked()
    {
        var backend = _backend;
        _backend = null;
        _handlingFailure = false;
        if (backend is null) return null;

        backend.PlaybackEnded -= OnBackendPlaybackEnded;
        backend.PlaybackFailed -= OnBackendPlaybackFailed;
        return backend;
    }

    private void StopBackend()
    {
        IMusicBackend? backend;
        lock (_sync)
        {
            backend = _backend;
        }

        if (backend is null) return;

        lock (_backendOperation)
        {
            try
            {
                backend.Stop();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, "[Music] 停止音频后端失败");
            }
        }
    }

    private void StopAndDisposeBackend(IMusicBackend? backend)
    {
        if (backend is null) return;

        lock (_backendOperation)
        {
            try
            {
                backend.Stop();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, "[Music] 停止音频后端失败");
            }

            try
            {
                backend.Dispose();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, "[Music] 释放音频后端失败");
            }
        }
    }

    private static List<string> EnumerateMusicFiles()
    {
        var directory = Path.Combine(Paths.Data, "Musics");
        try
        {
            Directory.CreateDirectory(directory);
            return
            [
                .. Directory
                    .EnumerateFiles(directory)
                    .Where(file =>
                        !IgnoredExtensions.Contains(
                            Path.GetExtension(file),
                            StringComparer.OrdinalIgnoreCase))
            ];
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, Module, "[Music] 初始化音乐列表失败");
            return [];
        }
    }

    private static int ReadVolume()
    {
        try
        {
            return Math.Clamp(Config.Preference.Music.Volume, 0, 1000);
        }
        catch
        {
            return 500;
        }
    }

    private static bool ReadShuffle()
    {
        try
        {
            return Config.Preference.Music.ShufflePlayback;
        }
        catch
        {
            return true;
        }
    }

    private static bool ReadStartOnStartup()
    {
        try
        {
            return Config.Preference.Music.StartOnStartup;
        }
        catch
        {
            return true;
        }
    }
}
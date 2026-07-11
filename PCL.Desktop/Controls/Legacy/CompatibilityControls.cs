// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace PCL.Desktop.Controls.Legacy;

public class BlurBorder : Border
{
}

public sealed class MediaFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public class MediaElement : Image, IDisposable
{
    public static readonly StyledProperty<string?> LoadedBehaviorProperty =
        AvaloniaProperty.Register<MediaElement, string?>(nameof(LoadedBehavior));

    public static readonly StyledProperty<string?> UnloadedBehaviorProperty =
        AvaloniaProperty.Register<MediaElement, string?>(nameof(UnloadedBehavior));

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<MediaElement, double>(nameof(Volume));

    public event EventHandler? MediaEnded;
    public event EventHandler<MediaFailedEventArgs>? MediaFailed;

    private LibVLC? _libVlc;
    private LibVLCSharp.Shared.MediaPlayer? _player;
    private Media? _media;
    private readonly object _frameGate = new();
    private WriteableBitmap? _frameBitmap;
    private IntPtr _frameBuffer;
    private byte[]? _frameCopy;
    private int _frameSize;
    private int _frameUpdatePending;
    private uint _frameWidth;
    private uint _frameHeight;
    private Uri? _sourceUri;
    private bool _disposed;

    public string? LoadedBehavior
    {
        get => GetValue(LoadedBehaviorProperty);
        set => SetValue(LoadedBehaviorProperty, value);
    }

    public string? UnloadedBehavior
    {
        get => GetValue(UnloadedBehaviorProperty);
        set => SetValue(UnloadedBehaviorProperty, value);
    }

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public new Uri? Source
    {
        get => _sourceUri;
        set
        {
            if (_sourceUri == value)
                return;
            Stop();
            _sourceUri = value;
            if (_sourceUri is not null && string.Equals(LoadedBehavior, "Play", StringComparison.OrdinalIgnoreCase))
                Play();
        }
    }

    public bool IsPlaying => _player?.IsPlaying == true;

    public bool Play()
    {
        if (_disposed || Source is null)
            return false;

        try
        {
            EnsurePlayer();
            if (_player is null || _libVlc is null)
                return false;

            if (_media is null)
            {
                _media = new Media(_libVlc, Source);
                _player.Media = _media;
            }

            _player.Volume = (int)Math.Round(Math.Clamp(Volume, 0d, 1d) * 100d);
            _player.Mute = Volume <= 0d;
            return _player.Play();
        }
        catch (Exception ex) when (ex is VLCException or DllNotFoundException or FileNotFoundException or InvalidOperationException)
        {
            RaiseMediaFailed(ex);
            return false;
        }
    }

    public void Pause() => _player?.Pause();

    public void Stop()
    {
        _player?.Stop();
        _media?.Dispose();
        _media = null;
        if (_player is not null)
            _player.Media = null;
    }

    public void Close()
    {
        Stop();
        Source = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VolumeProperty && _player is not null)
        {
            _player.Volume = (int)Math.Round(Math.Clamp(Volume, 0d, 1d) * 100d);
            _player.Mute = Volume <= 0d;
        }
    }

    private void EnsurePlayer()
    {
        if (_player is not null)
            return;

        string? libVlcDirectory = ResolveLibVlcDirectory();
        if (libVlcDirectory is null)
            LibVLCSharp.Shared.Core.Initialize();
        else
            LibVLCSharp.Shared.Core.Initialize(libVlcDirectory);
        _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        _player = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
        _player.SetVideoFormatCallbacks(ConfigureVideoFormat, CleanupVideoFormat);
        _player.SetVideoCallbacks(LockVideoFrame, null, DisplayVideoFrame);
        _player.EndReached += Player_EndReached;
        _player.EncounteredError += Player_EncounteredError;
    }

    private static string? ResolveLibVlcDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
            System.Runtime.InteropServices.Architecture.X86 => "win-x86",
            _ => "win-x64"
        };
        IEnumerable<string> roots = [AppContext.BaseDirectory];
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeDirectories)
        {
            roots = roots.Concat(nativeDirectories.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string candidate = Path.Combine(root, "libvlc", architecture);
            if (File.Exists(Path.Combine(candidate, "libvlc.dll")))
                return candidate;
        }
        return null;
    }

    private uint ConfigureVideoFormat(
        ref IntPtr opaque,
        IntPtr chroma,
        ref uint width,
        ref uint height,
        ref uint pitches,
        ref uint lines)
    {
        if (width == 0 || height == 0 || width > 7680 || height > 4320)
            return 0;

        Marshal.WriteByte(chroma, 0, (byte)'R');
        Marshal.WriteByte(chroma, 1, (byte)'V');
        Marshal.WriteByte(chroma, 2, (byte)'3');
        Marshal.WriteByte(chroma, 3, (byte)'2');
        pitches = checked(width * 4);
        lines = height;
        int frameSize = checked((int)(pitches * lines));

        lock (_frameGate)
        {
            ReleaseFrameBuffer();
            _frameBuffer = Marshal.AllocHGlobal(frameSize);
            _frameCopy = new byte[frameSize];
            _frameSize = frameSize;
            _frameWidth = width;
            _frameHeight = height;
        }

        Dispatcher.UIThread.Post(CreateFrameBitmap);
        return 1;
    }

    private IntPtr LockVideoFrame(IntPtr opaque, IntPtr planes)
    {
        lock (_frameGate)
        {
            Marshal.WriteIntPtr(planes, _frameBuffer);
            return _frameBuffer;
        }
    }

    private void DisplayVideoFrame(IntPtr opaque, IntPtr picture)
    {
        lock (_frameGate)
        {
            if (_frameBuffer == IntPtr.Zero || _frameCopy is null || _frameSize == 0)
                return;
            Marshal.Copy(_frameBuffer, _frameCopy, 0, _frameSize);
        }

        if (Interlocked.Exchange(ref _frameUpdatePending, 1) == 0)
            Dispatcher.UIThread.Post(UpdateFrameBitmap, DispatcherPriority.Render);
    }

    private void CreateFrameBitmap()
    {
        uint width;
        uint height;
        lock (_frameGate)
        {
            width = _frameWidth;
            height = _frameHeight;
        }
        if (_disposed || width == 0 || height == 0)
            return;

        _frameBitmap?.Dispose();
        _frameBitmap = new WriteableBitmap(
            new PixelSize((int)width, (int)height),
            new Vector(96d, 96d),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        base.Source = _frameBitmap;
    }

    private void UpdateFrameBitmap()
    {
        try
        {
            lock (_frameGate)
            {
                if (_disposed || _frameBitmap is null || _frameCopy is null || _frameSize == 0)
                    return;
                using ILockedFramebuffer framebuffer = _frameBitmap.Lock();
                Marshal.Copy(_frameCopy, 0, framebuffer.Address, _frameSize);
            }
            InvalidateVisual();
        }
        finally
        {
            Interlocked.Exchange(ref _frameUpdatePending, 0);
        }
    }

    private void CleanupVideoFormat(ref IntPtr opaque)
    {
        lock (_frameGate)
            ReleaseFrameBuffer();
    }

    private void ReleaseFrameBuffer()
    {
        if (_frameBuffer != IntPtr.Zero)
            Marshal.FreeHGlobal(_frameBuffer);
        _frameBuffer = IntPtr.Zero;
        _frameCopy = null;
        _frameSize = 0;
        _frameWidth = 0;
        _frameHeight = 0;
    }

    private void Player_EndReached(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => MediaEnded?.Invoke(this, EventArgs.Empty));

    private void Player_EncounteredError(object? sender, EventArgs e) =>
        RaiseMediaFailed(new InvalidOperationException("LibVLC 无法播放该媒体文件。"));

    private void RaiseMediaFailed(Exception exception) =>
        Dispatcher.UIThread.Post(() => MediaFailed?.Invoke(this, new MediaFailedEventArgs(exception)));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_player is not null)
        {
            _player.EndReached -= Player_EndReached;
            _player.EncounteredError -= Player_EncounteredError;
        }
        Stop();
        _player?.Dispose();
        _libVlc?.Dispose();
        lock (_frameGate)
            ReleaseFrameBuffer();
        _frameBitmap?.Dispose();
        _frameBitmap = null;
        base.Source = null;
        _player = null;
        _libVlc = null;
        GC.SuppressFinalize(this);
    }
}

using System;
using System.Collections.Generic;

namespace PCL.Core.Media;

/// <summary>视频背景原始帧的像素格式。</summary>
public enum FfmpegPixelFormat
{
    Bgra8888,
    Yuv420p
}

/// <summary>一次 FFmpeg 视频背景播放使用的参数。</summary>
public sealed record FfmpegPlaybackOptions(
    bool HardwareAcceleration,
    int DecoderThreads,
    int FilterThreads,
    bool LowDelay,
    FfmpegPixelFormat PixelFormat,
    double? FpsCap);

/// <summary>一个可尝试的播放 profile。</summary>
public sealed record FfmpegPlaybackProfile(
    string Id,
    FfmpegPlaybackOptions Options,
    TimeSpan FirstFrameTimeout);

/// <summary>按硬件开关、源视频信息和 CPU 核数生成播放 profile 列表。</summary>
public static class FfmpegPlaybackProfiles
{
    private static readonly TimeSpan _HardwareTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan _SoftwareTimeout = TimeSpan.FromSeconds(15);

    public static IReadOnlyList<FfmpegPlaybackProfile> Create(
        bool hardwareEnabled,
        VideoInfo? source,
        int processorCount,
        FfmpegPixelFormat pixelFormat)
    {
        var highResolution = source != null
            && (long)source.Size.Width * source.Size.Height > 1920L * 1080L;
        var decoderThreads = highResolution
            ? Math.Clamp(processorCount, 2, 4)
            : 2;
        double? fpsCap = 30d;
        if (source?.FrameRate is > 0d and <= 30.5d)
            fpsCap = null;

        var profiles = new List<FfmpegPlaybackProfile>(4);
        if (hardwareEnabled)
            profiles.Add(
                new FfmpegPlaybackProfile(
                    "hardware",
                    new FfmpegPlaybackOptions(
                        true,
                        decoderThreads,
                        1,
                        false,
                        pixelFormat,
                        fpsCap),
                    _HardwareTimeout));

        profiles.Add(
            new FfmpegPlaybackProfile(
                "software-low-delay",
                new FfmpegPlaybackOptions(
                    false,
                    decoderThreads,
                    1,
                    true,
                    pixelFormat,
                    fpsCap),
                _SoftwareTimeout));
        profiles.Add(
            new FfmpegPlaybackProfile(
                "software",
                new FfmpegPlaybackOptions(
                    false,
                    decoderThreads,
                    1,
                    false,
                    pixelFormat,
                    fpsCap),
                _SoftwareTimeout));
        profiles.Add(
            new FfmpegPlaybackProfile(
                "software-auto",
                new FfmpegPlaybackOptions(
                    false,
                    0,
                    1,
                    false,
                    pixelFormat,
                    fpsCap),
                _SoftwareTimeout));

        return profiles;
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PCL.Core.Media;

/// <summary>构建 FFmpeg 视频背景播放命令。</summary>
public static class FfmpegCommandBuilder
{
    /// <summary>构建循环输出原始帧的播放参数。</summary>
    /// <remarks>输出尺寸由调用方按视频原始比例计算，适配模式由界面层处理。</remarks>
    public static IReadOnlyList<string> BuildPlaybackArguments(
        string videoPath,
        int width,
        int height,
        FfmpegPlaybackOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        width = Math.Max(2, width);
        if ((width & 1) != 0) width++;
        height = Math.Max(2, height);
        if ((height & 1) != 0) height++;

        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel",
            "error",
            "-nostdin",
            "-protocol_whitelist",
            "file,pipe",
            "-filter_threads",
            Math
                .Max(1, options.FilterThreads)
                .ToString(
                    CultureInfo.InvariantCulture)
        };

        if (options.DecoderThreads > 0)
        {
            arguments.Add("-threads");
            arguments.Add(options.DecoderThreads.ToString(CultureInfo.InvariantCulture));
        }

        if (options.LowDelay)
        {
            arguments.Add("-flags");
            arguments.Add("+low_delay");
        }

        if (options.HardwareAcceleration)
        {
            arguments.Add("-hwaccel");
            arguments.Add("auto");
        }

        arguments.Add("-stream_loop");
        arguments.Add("-1");
        arguments.Add("-re");
        arguments.Add("-i");
        arguments.Add(videoPath);
        arguments.Add("-an");
        arguments.Add("-sn");
        arguments.Add("-dn");

        var filters = new List<string>(3);
        if (options.FpsCap is { } fpsCap)
            filters.Add(
                "fps=" + fpsCap.ToString("0.###", CultureInfo.InvariantCulture));

        filters.Add($"scale={width}:{height}:flags=bicubic");
        filters.Add("setsar=1");
        arguments.Add("-vf");
        arguments.Add(string.Join(',', filters));
        arguments.Add("-f");
        arguments.Add("rawvideo");
        arguments.Add("-pix_fmt");
        arguments.Add(
            options.PixelFormat == FfmpegPixelFormat.Yuv420p
                ? "yuv420p"
                : "bgra");
        arguments.Add("pipe:1");
        return arguments;
    }
}
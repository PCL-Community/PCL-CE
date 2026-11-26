using System;
using System.Collections.Generic;

namespace PCL.Core.Media;

/// <summary>构建 FFmpeg 视频背景播放命令。</summary>
public static class FfmpegCommandBuilder
{
    /// <summary>构建循环输出 BGRA 原始帧的播放参数。</summary>
    /// <remarks>输出尺寸由调用方按视频原始比例计算，适配模式由界面层处理。</remarks>
    public static IReadOnlyList<string> BuildPlaybackArguments(
        string videoPath,
        int width,
        int height,
        int fps)
    {
        width = Math.Max(2, width);
        if ((width & 1) != 0) width++;
        height = Math.Max(2, height);
        if ((height & 1) != 0) height++;
        fps = Math.Max(1, fps);
        return
        [
            "-hide_banner",
            "-loglevel", "error",
            "-nostdin",
            "-protocol_whitelist", "file,pipe",
            "-stream_loop", "-1",
            "-re",
            "-i", videoPath,
            "-an",
            "-sn",
            "-dn",
            "-vf",
            $"fps={fps},scale={width}:{height},setsar=1",
            "-f", "rawvideo",
            "-pix_fmt", "bgra",
            "pipe:1"
        ];
    }
}
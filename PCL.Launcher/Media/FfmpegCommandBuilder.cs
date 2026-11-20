namespace PCL.Media;

/// <summary>构建 FFmpeg 视频背景播放命令。</summary>
public static class FfmpegCommandBuilder
{
    /// <summary>构建循环输出 BGRA 原始帧的播放参数。</summary>
    public static IReadOnlyList<string> BuildPlaybackArguments(
        string videoPath,
        int width,
        int height,
        int fps)
    {
        int value = Math.Max(2, width);
        width = (value & 1) == 0 ? value : value + 1;
        int value1 = Math.Max(2, height);
        height = (value1 & 1) == 0 ? value1 : value1 + 1;
        fps = Math.Max(1, fps);
        return
        [
            "-hide_banner",
            "-loglevel", "error",
            "-nostdin",
            "-stream_loop", "-1",
            "-re",
            "-i", videoPath,
            "-an",
            "-sn",
            "-dn",
            "-vf",
            $"fps={fps},scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}",
            "-f", "rawvideo",
            "-pix_fmt", "bgra",
            "pipe:1"
        ];
    }
}
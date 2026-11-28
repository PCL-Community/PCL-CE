namespace PCL.Core.Media;

/// <summary>FFmpeg 的检测状态。</summary>
public enum FfmpegAvailability
{
    /// <summary>未找到可用的 FFmpeg。</summary>
    NotFound,

    /// <summary>指定路径的 FFmpeg 无法运行。</summary>
    Invalid,

    /// <summary>FFmpeg 可用。</summary>
    Available
}

/// <summary>FFmpeg 检测结果。</summary>
public sealed record FfmpegProbeResult(
    FfmpegAvailability Availability,
    string? ExecutablePath = null,
    string? Version = null,
    string? Error = null)
{
    /// <summary>FFmpeg 是否可用。</summary>
    public bool IsAvailable => Availability == FfmpegAvailability.Available;
}
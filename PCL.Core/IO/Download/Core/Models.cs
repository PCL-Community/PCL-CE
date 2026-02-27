namespace PCL.Core.IO.Download.Core;

/// <summary>
/// 分块信息
/// </summary>
/// <param name="StartOffset">开始偏移</param>
/// <param name="Length">分块长度</param>
/// <param name="ChunkIndex">分块索引</param>
public record struct ChunkInfo(
    long StartOffset,
    long Length,
    int ChunkIndex
);

/// <summary>
/// 镜像信息 - 存储镜像源的静态属性和初始评估结果
/// </summary>
public record MirrorInfo
{
    /// <summary>
    /// 镜像URL
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// 是否存活
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// 探测延迟 (ms)
    /// </summary>
    public long LatencyMilliseconds { get; set; }

    /// <summary>
    /// 估算带宽 (bytes/s)，基于探测阶段的小数据传输测量
    /// </summary>
    public double EstimatedBandwidthBps { get; set; }

    /// <summary>
    /// 动态健康分数 [0-100]，运行时根据表现调整
    /// </summary>
    public int HealthScore { get; set; } = 100;
}
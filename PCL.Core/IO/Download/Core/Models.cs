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
/// 镜像信息
/// </summary>
public record MirrorInfo
{
    /// <summary>
    /// 链接
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// 是否存活（能够连接）
    /// </summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// 延迟（毫秒）
    /// </summary>
    public long LatencyMilliseconds { get; set; }

    /// <summary>
    /// 健康度
    /// </summary>
    public int HealthScore { get; set; } = 100;
}
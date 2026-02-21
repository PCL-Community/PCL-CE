namespace PCL.Core.Net.Downloader.Core;

public record struct ChunkInfo(
    long StartOffset,
    long Length,
    int ChunkIndex
);

public record MirrorInfo
{
    public required string Url { get; init; }
    public bool IsAlive { get; set; } = true;
    public long LatencyMs { get; set; }
    public int HealthScore { get; set; } = 100;
}
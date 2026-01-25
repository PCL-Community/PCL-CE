namespace PCL.Core.Net.NDownload;

/// <summary>
/// 下载源质量报告
/// </summary>
/// <param name="MaxSegmentCount">支持最大分块数量</param>
/// <param name="RetryCount">重试计数</param>
/// <param name="AverageSpeed">总体平均速度</param>
public record NDlSourceReport(
    int MaxSegmentCount = 1,
    int RetryCount = 0,
    long AverageSpeed = -1
);

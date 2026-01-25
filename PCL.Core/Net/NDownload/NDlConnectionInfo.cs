namespace PCL.Core.Net.NDownload;

/// <summary>
/// 下载连接信息。
/// </summary>
/// <param name="Length">内容长度，单位为字节</param>
/// <param name="BeginOffset">起始偏移</param>
/// <param name="EndOffset">结束偏移</param>
/// <param name="IsSupportSegment">是否支持分块</param>
public record NDlConnectionInfo(
    long Length,
    long BeginOffset,
    long EndOffset,
    bool IsSupportSegment
);

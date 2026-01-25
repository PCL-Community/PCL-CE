using System;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 不支持范围请求的异常
/// </summary>
/// <param name="uri">镜像 URI</param>
public class DownloadRangeNotSupportedException(Uri uri)
    : Exception($"Mirror {uri} does not support range requests.");
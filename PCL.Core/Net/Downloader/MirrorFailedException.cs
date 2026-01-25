using System;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 镜像下载失败异常
/// </summary>
/// <param name="uri">镜像 URI</param>
/// <param name="inner">内部异常</param>
public class MirrorFailedException(Uri uri, Exception inner)
    : Exception($"Mirror {uri} failed.", inner);
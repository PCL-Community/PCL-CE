using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

/// <summary>
/// 镜像选择器接口
/// </summary>
public interface IMirrorSelector
{
    /// <summary>
    /// 从给定的镜像列表中选择最佳镜像
    /// </summary>
    /// <param name="uris">镜像列表</param>
    /// <param name="token">取消令牌</param>
    /// <returns>最佳镜像的 URI</returns>
    Task<Uri> GetBestMirrorAsync(IEnumerable<Uri> uris, CancellationToken token);
}
using PCL.Core.Link.McPing.Model;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Link.McPing;

/// <summary>
/// Minecraft服务器探测服务接口
/// </summary>
public interface IMcPingService : IDisposable
{
    /// <summary>
    /// 异步探测Minecraft服务器信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服务器探测结果，如果探测失败则返回null</returns>
    Task<McPingResult?> PingAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取服务端点信息
    /// </summary>
    IPEndPoint Endpoint { get; }
    
    /// <summary>
    /// 获取主机地址
    /// </summary>
    string Host { get; }
    
    /// <summary>
    /// 获取超时时间（毫秒）
    /// </summary>
    int Timeout { get; }
}

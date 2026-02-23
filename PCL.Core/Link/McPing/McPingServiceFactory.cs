using System.Net;

namespace PCL.Core.Link.McPing;

/// <summary>
/// Minecraft服务器探测服务工厂
/// 提供统一的服务创建接口
/// </summary>
public static class McPingServiceFactory
{
    /// <summary>
    /// 创建现代协议探测服务
    /// </summary>
    /// <param name="endpoint">服务器端点</param>
    /// <param name="timeout">超时时间（毫秒）</param>
    /// <returns>IMcPingService实例</returns>
    public static IMcPingService CreateService(IPEndPoint endpoint, int timeout = 10000)
    {
        return new McPingService(endpoint, timeout);
    }

    /// <summary>
    /// 创建现代协议探测服务
    /// </summary>
    /// <param name="ip">服务器IP地址</param>
    /// <param name="port">服务器端口</param>
    /// <param name="timeout">超时时间（毫秒）</param>
    /// <returns>IMcPingService实例</returns>
    public static IMcPingService CreateService(string ip, int port = 25565, int timeout = 10000)
    {
        return new McPingService(ip, port, timeout);
    }

    /// <summary>
    /// 创建旧版协议探测服务
    /// </summary>
    /// <param name="endpoint">服务器端点</param>
    /// <param name="timeout">超时时间（毫秒）</param>
    /// <returns>IMcPingService实例</returns>
    public static IMcPingService CreateLegacyService(IPEndPoint endpoint, int timeout = 10000)
    {
        return new LegacyMcPingService(endpoint, timeout);
    }

    /// <summary>
    /// 创建旧版协议探测服务
    /// </summary>
    /// <param name="ip">服务器IP地址</param>
    /// <param name="port">服务器端口</param>
    /// <param name="timeout">超时时间（毫秒）</param>
    /// <returns>IMcPingService实例</returns>
    public static IMcPingService CreateLegacyService(string ip, int port = 25565, int timeout = 10000)
    {
        return new LegacyMcPingService(ip, port, timeout);
    }
}

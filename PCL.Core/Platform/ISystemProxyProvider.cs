using System;
using System.Collections.Generic;

namespace PCL.Core.Platform;

/// <summary>
///     系统代理查询。具体实现由 UI 层注册到 <see cref="PlatformServices.SystemProxy" />。
/// </summary>
public interface ISystemProxyProvider
{
    /// <summary>返回当前系统代理列表（协议名 + host:port）；未启用或不可用时返回空列表。</summary>
    IReadOnlyList<SystemProxyEntry> GetSystemProxies();

    /// <summary>系统代理设置发生变化。实现应自行防抖；事件可能在线程池触发。</summary>
    event EventHandler? Changed;
}

/// <summary>系统代理条目。<see cref="Protocol" /> 形如 <c>http</c>、<c>https</c>、<c>socks</c>。</summary>
public sealed record SystemProxyEntry(string Protocol, string Address);
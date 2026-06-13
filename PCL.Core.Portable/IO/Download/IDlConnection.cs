// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// 下载连接，负责与服务器进行通信。
/// </summary>
public interface IDlConnection
{
    /// <summary>
    /// 开始连接，发起与服务器的通信。
    /// </summary>
    ValueTask<NDlConnectionInfo> StartAsync(
        long beginOffset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止连接，同时停止服务器通信并释放资源。
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将数据读取到调用方提供的缓冲区，返回 0 表示已到流末尾。
    /// </summary>
    ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default);
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// 下载连接信息。
/// </summary>
/// <param name="Length">内容长度，单位为字节</param>
/// <param name="BeginOffset">起始偏移</param>
/// <param name="EndOffset">结束偏移</param>
/// <param name="IsSupportSegment">是否支持分块</param>
public readonly record struct NDlConnectionInfo(
    long Length,
    long BeginOffset,
    long EndOffset,
    bool IsSupportSegment);

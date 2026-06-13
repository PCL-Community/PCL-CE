// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// 下载写入器。
/// </summary>
public interface IDlWriter
{
    bool IsSupportParallel { get; }

    ValueTask<Stream> CreateStreamAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);

    ValueTask FinishAsync(CancellationToken cancellationToken = default);
}

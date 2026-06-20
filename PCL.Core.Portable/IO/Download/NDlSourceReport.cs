// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// Historical quality information for a download source.
/// </summary>
public sealed record NDlSourceReport(
    int MaxSegmentCount = 1,
    int RetryCount = 0,
    long AverageSpeed = -1);

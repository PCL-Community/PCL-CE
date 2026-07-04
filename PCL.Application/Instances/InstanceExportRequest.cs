// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Instances;

public sealed record InstanceExportRequest
{
    public required string InstanceDirectory { get; init; }

    public required string GameDirectory { get; init; }

    public required string TargetArchivePath { get; init; }

    public IReadOnlyList<string> Rules { get; init; } = [];
}

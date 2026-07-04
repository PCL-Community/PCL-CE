// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Downloads;

public sealed record MinecraftRepairRequest
{
    public required string VersionId { get; init; }

    public required string VersionJsonPath { get; init; }

    public required string MinecraftRootDirectory { get; init; }

    public required string InstanceDirectory { get; init; }

    public bool PreferOfficialSource { get; init; } = true;
}

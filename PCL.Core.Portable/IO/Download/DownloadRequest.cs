// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

public sealed record DownloadRequest
{
    public required IReadOnlyList<string> Sources { get; init; }

    public required string DestinationPath { get; init; }

    public required Func<string, IDlConnection?> ConnectionFactory { get; init; }

    public Func<string, IDlWriter?> WriterFactory { get; init; } =
        static path => new FileDlWriter(path);
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

public enum DownloadStage
{
    Connecting,
    Reading,
    Downloading,
    Committing,
    Retrying,
    Completed,
    Failed
}

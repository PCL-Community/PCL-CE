// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

/// <summary>
/// Maps a stable resource identifier to a connection or writer argument.
/// </summary>
public interface IDlResourceMapping<out TMappingValue>
{
    TMappingValue? Parse(string resourceId);
}

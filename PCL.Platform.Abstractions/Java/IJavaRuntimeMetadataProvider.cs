// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Java;

public interface IJavaRuntimeMetadataProvider
{
    ValueTask<string> GetRuntimeIndexAsync(CancellationToken cancellationToken);
    ValueTask<string> GetManifestAsync(string manifestUrl, CancellationToken cancellationToken);
}

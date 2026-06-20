// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Paths;

public interface IPlatformPathProvider
{
    string ApplicationDataDirectory { get; }
    string CacheDirectory { get; }
    string TemporaryDirectory { get; }
}

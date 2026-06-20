// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Registry;

public interface IRegistryService
{
    ValueTask<string?> GetStringAsync(string keyPath, string valueName, CancellationToken cancellationToken);
    ValueTask<int?> GetInt32Async(string keyPath, string valueName, CancellationToken cancellationToken);
}

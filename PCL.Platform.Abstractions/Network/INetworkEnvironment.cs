// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.Network;

public interface INetworkEnvironment
{
    ValueTask<NetworkEnvironmentInfo> GetCurrentAsync(CancellationToken cancellationToken);
}

public sealed record NetworkEnvironmentInfo(
    string? CountryCode,
    bool PreferRegionalMirror,
    bool AllowRegionalMirrorSwitch,
    string? RegulatoryNotice);

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Launching;

public static class AuthlibMetadataEndpointRegistry
{
    private static readonly AuthlibMetadataEndpoint[] DefaultEndpoints =
    [
        AuthlibMetadataEndpoint.Official,
        AuthlibMetadataEndpoint.BmclApiMirror
    ];

    public static ReadOnlySpan<AuthlibMetadataEndpoint> Defaults => DefaultEndpoints;
}

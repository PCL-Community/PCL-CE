// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Launching;

public readonly record struct AuthlibMetadataEndpoint
{
    public AuthlibMetadataEndpoint(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
    }

    public string Url { get; }

    public static AuthlibMetadataEndpoint Official { get; } =
        new("https://authlib-injector.yushi.moe/artifact/latest.json");

    public static AuthlibMetadataEndpoint BmclApiMirror { get; } =
        new("https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json");

    public override string ToString() => Url;
}

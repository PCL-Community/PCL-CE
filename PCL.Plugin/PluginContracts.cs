// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Plugin;

public sealed record PluginManifest(
    string Id,
    string Name,
    Version Version,
    string Author,
    string Description,
    IReadOnlyList<PluginCapability> Capabilities);

public sealed record PluginCapability(string Name, string MinimumHostVersion);

public interface IPclPlugin
{
    PluginManifest Manifest { get; }

    ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

public interface IPluginContext
{
    Version HostVersion { get; }

    string DataDirectory { get; }

    IServiceProvider Services { get; }
}

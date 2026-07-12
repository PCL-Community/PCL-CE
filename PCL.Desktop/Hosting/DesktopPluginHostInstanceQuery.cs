// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting.PluginPlatform;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Hosting;

/// <summary>Exposes discovered Minecraft instances to plugins via <c>pcl.instances.read</c>.</summary>
internal sealed class DesktopPluginHostInstanceQuery : IPluginHostInstanceQuery
{
    public static DesktopPluginHostInstanceQuery Instance { get; } = new();

    public IReadOnlyList<HostPluginInstanceInfo> ListInstances()
    {
        try
        {
            IReadOnlyList<LaunchInstanceInfo> discovered =
                LaunchInstanceDiscovery.Discover(LaunchInstanceDiscovery.GetCandidateRoots());
            return discovered
                .Select(static item => new HostPluginInstanceInfo(
                    item.Name,
                    item.Name,
                    item.InstanceDirectory,
                    item.VersionJsonPath))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}

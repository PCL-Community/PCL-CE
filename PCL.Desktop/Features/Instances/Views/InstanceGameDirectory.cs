// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Instances;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

/// <summary>
/// Resolves the Minecraft game directory for an instance, honoring per-version isolation
/// (WPF: indie version → version folder, else shared .minecraft root).
/// </summary>
internal static class InstanceGameDirectory
{
    public static async Task<string> ResolveAsync(
        LaunchInstanceInfo instance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        bool isolated = true;
        try
        {
            InstanceMetadata metadata = await InstanceMetadataStore
                .LoadAsync(instance.InstanceDirectory, cancellationToken)
                .ConfigureAwait(false);
            isolated = metadata.InstanceIsolation;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            isolated = true;
        }

        return isolated ? instance.InstanceDirectory : GetSharedMinecraftRoot(instance);
    }

    public static string GetSharedMinecraftRoot(LaunchInstanceInfo instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo? versionsDirectory = versionDirectory.Parent;
        if (versionsDirectory?.Parent is not null &&
            string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
        {
            return versionsDirectory.Parent.FullName;
        }

        return instance.InstanceDirectory;
    }
}

// Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using PCL.Application.Hosting.PluginPlatform;

namespace PCL.Desktop.Hosting;

internal sealed class DesktopPluginHostRawUiAccess : IPluginHostRawUiAccess
{
    public static DesktopPluginHostRawUiAccess Instance { get; } = new();

    public object Application => Avalonia.Application.Current
        ?? throw new InvalidOperationException("Avalonia application is not initialized.");

    public IReadOnlyList<object> TopLevels =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            ? lifetime.Windows.Cast<object>().ToArray()
            : [];

    public object? ResolveTarget(string surfaceId) =>
        DesktopPluginHostUiComposition.Instance.ResolveTarget(surfaceId);

    public long GetTargetGeneration(string surfaceId) =>
        DesktopPluginHostUiComposition.Instance.GetTargetGeneration(surfaceId);
}

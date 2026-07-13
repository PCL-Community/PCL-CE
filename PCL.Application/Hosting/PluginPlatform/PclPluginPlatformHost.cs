// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using PCL.Application.Settings;

namespace PCL.Application.Hosting.PluginPlatform;

/// <summary>Default host bridge: settings pages from <see cref="IPclHost"/> + immediate work queue.</summary>
internal sealed class PclPluginPlatformHost : IPclPluginPlatformHost
{
    public PclPluginPlatformHost(
        IHostSettingsPageGroupRegistry settingsPageGroups,
        IHostSettingsPageRegistry settingsPages,
        IPluginHostWorkQueue? workQueue = null,
        IPluginHostNotifications? notifications = null,
        IPluginHostInstanceQuery? instances = null,
        IPluginHostUiComposition? uiComposition = null)
    {
        SettingsPageGroups = settingsPageGroups ?? throw new ArgumentNullException(nameof(settingsPageGroups));
        SettingsPages = settingsPages ?? throw new ArgumentNullException(nameof(settingsPages));
        WorkQueue = workQueue ?? ImmediatePluginHostWorkQueue.Instance;
        Notifications = notifications ?? CapturingPluginHostNotifications.Instance;
        Instances = instances;
        UiComposition = uiComposition;
    }

    public IHostSettingsPageGroupRegistry SettingsPageGroups { get; }

    public IHostSettingsPageRegistry SettingsPages { get; }

    public IPluginHostWorkQueue WorkQueue { get; }

    public IPluginHostNotifications Notifications { get; }

    public IPluginHostInstanceQuery? Instances { get; }

    public IPluginHostUiComposition? UiComposition { get; }
}

internal sealed class ImmediatePluginHostWorkQueue : IPluginHostWorkQueue
{
    public static ImmediatePluginHostWorkQueue Instance { get; } = new();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(action());
    }
}

/// <summary>Captures notifications for diagnostics when no UI toast host is wired.</summary>
internal sealed class CapturingPluginHostNotifications : IPluginHostNotifications
{
    public static CapturingPluginHostNotifications Instance { get; } = new();

    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public void ShowInformation(string message) =>
        _messages.Enqueue("[info] " + (message ?? string.Empty));

    public void ShowWarning(string message) =>
        _messages.Enqueue("[warn] " + (message ?? string.Empty));
}

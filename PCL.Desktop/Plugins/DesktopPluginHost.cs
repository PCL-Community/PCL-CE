// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Plugins;

internal static partial class DesktopPluginHost
{
    private static readonly DesktopPluginRegistry Registry = new();
    private static bool _initialized;

    public static IReadOnlyList<PclPluginDescriptor> Plugins
    {
        get
        {
            Initialize();
            return Registry.Plugins;
        }
    }

    public static IReadOnlyList<PclPluginFeatureDescriptor> Features
    {
        get
        {
            Initialize();
            return Registry.Features;
        }
    }

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        RegisterInjectedPlugins(Registry);
    }

    static partial void RegisterInjectedPlugins(IPclPluginHost host);
}

internal sealed class DesktopPluginRegistry : IPclPluginHost, IPluginRegistrationContext
{
    private readonly List<PclPluginDescriptor> _plugins = [];
    private readonly List<PclPluginFeatureDescriptor> _features = [];

    public Version HostVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public IReadOnlyList<PclPluginDescriptor> Plugins => _plugins;

    public IReadOnlyList<PclPluginFeatureDescriptor> Features => _features;

    public void RegisterModule(IPclPluginModule pluginModule)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);

        PclPluginDescriptor descriptor = pluginModule.Descriptor;
        if (_plugins.Any(plugin => string.Equals(plugin.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"插件已注册：{descriptor.Id}");

        _plugins.Add(descriptor);
        pluginModule.Register(this);
    }

    public void RegisterFeature(PclPluginFeatureDescriptor feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        if (!_plugins.Any(plugin => string.Equals(plugin.Id, feature.PluginId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"插件功能 {feature.FeatureId} 所属的插件尚未注册。");

        if (_features.Any(existing =>
                string.Equals(existing.PluginId, feature.PluginId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.FeatureId, feature.FeatureId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"插件功能已注册：{feature.PluginId}/{feature.FeatureId}");
        }

        _features.Add(feature);
    }
}

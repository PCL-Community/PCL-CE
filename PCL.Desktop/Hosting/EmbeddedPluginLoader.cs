// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PCL.Desktop.Hosting;

internal static class EmbeddedPluginLoader
{
    internal const string ResourceName = "PCL.Desktop.Embedded.PCL.Plugin.dll";

    private static readonly object SyncRoot = new();
    private static Assembly? _loadedAssembly;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Injected plugin releases are bundled only in non-trimmed, non-AOT desktop publishes.")]
    public static Assembly? Load()
    {
        lock (SyncRoot)
        {
            if (_loadedAssembly is not null)
                return _loadedAssembly;

            Assembly hostAssembly = typeof(EmbeddedPluginLoader).Assembly;
            using Stream? resource = hostAssembly.GetManifestResourceStream(ResourceName);
            if (resource is null)
                return null;

            using MemoryStream buffer = new();
            resource.CopyTo(buffer);
            _loadedAssembly = Assembly.Load(buffer.ToArray());
            return _loadedAssembly;
        }
    }
}

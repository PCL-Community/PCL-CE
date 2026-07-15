// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using PCL.Application.Hosting;

namespace PCL.Desktop.Hosting;

internal static class EmbeddedRuntimeExtensionLoader
{
    internal const string ResourceName = "PCL.Desktop.Embedded.PCL.Plugin.dll";
    internal const string AbstractionsResourceName = "PCL.Desktop.Embedded.PCL.N.Plugin.Abstractions.dll";
    internal const string SdkResourceName = "PCL.Desktop.Embedded.PCL.N.Plugin.Sdk.dll";
    internal const string UiResourceName = "PCL.Desktop.Embedded.PCL.N.Plugin.UI.dll";
    internal const string UiAvaloniaResourceName = "PCL.Desktop.Embedded.PCL.N.Plugin.UI.Avalonia.dll";
    internal const string BouncyCastleResourceName = "PCL.Desktop.Embedded.BouncyCastle.Cryptography.dll";
    internal const string JsonCanonicalizerResourceName = "PCL.Desktop.Embedded.jsoncanonicalizer.dll";
    internal const string Es6NumberSerializerResourceName = "PCL.Desktop.Embedded.es6numberserializer.dll";

    private static readonly object SyncRoot = new();
    private static Assembly? _loadedAssembly;
    private static Assembly? _loadedAbstractionsAssembly;
    private static readonly List<Assembly> LoadedDependencyAssemblies = [];

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
            if (!HasResource(ResourceName))
                return null;

            _loadedAbstractionsAssembly ??= LoadResourceAssembly(AbstractionsResourceName);
            if (LoadedDependencyAssemblies.Count == 0)
            {
                LoadRequiredDependency(UiResourceName);
                LoadRequiredDependency(UiAvaloniaResourceName);
                LoadRequiredDependency(SdkResourceName);
                LoadRequiredDependency(Es6NumberSerializerResourceName);
                LoadRequiredDependency(JsonCanonicalizerResourceName);
                LoadRequiredDependency(BouncyCastleResourceName);
            }

            _loadedAssembly = LoadResourceAssembly(ResourceName);
            return _loadedAssembly;
        }
    }

    private static bool HasResource(string resourceName) =>
        typeof(EmbeddedRuntimeExtensionLoader).Assembly.GetManifestResourceInfo(resourceName) is not null;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Injected platform assemblies are bundled only in non-trimmed, non-AOT desktop publishes.")]
    private static Assembly? LoadResourceAssembly(string resourceName)
    {
        Assembly hostAssembly = typeof(EmbeddedRuntimeExtensionLoader).Assembly;
        using Stream? resource = hostAssembly.GetManifestResourceStream(resourceName);
        if (resource is null)
            return null;

        using MemoryStream buffer = new();
        resource.CopyTo(buffer);
        buffer.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(buffer);
    }

    private static void LoadRequiredDependency(string resourceName)
    {
        Assembly assembly = LoadResourceAssembly(resourceName)
            ?? throw new InvalidOperationException($"Embedded plugin dependency is missing: {resourceName}");
        LoadedDependencyAssemblies.Add(assembly);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Injected plugin releases are bundled only in non-trimmed, non-AOT desktop publishes.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Plugin module constructors are preserved in the separately built injected assembly.")]
    public static IReadOnlyList<IPclHostModule> LoadHostModules()
    {
        Assembly? assembly = Load();
        if (assembly is null)
            return [];

        List<IPclHostModule> modules = [];
#pragma warning disable IL2070, IL2067, IL2075
        foreach (Type type in assembly.GetTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract ||
                type.IsInterface ||
                !typeof(IPclHostModule).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            modules.Add((IPclHostModule)Activator.CreateInstance(type)!);
        }
#pragma warning restore IL2070, IL2067, IL2075
        return modules;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Injected runtime extensions are bundled only in non-trimmed, non-AOT desktop publishes.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072",
        Justification = "Runtime extension constructors are preserved in the separately built injected assembly.")]
    public static IReadOnlyList<IRuntimeExtension> LoadRuntimeExtensions()
    {
        Assembly? assembly = Load();
        if (assembly is null)
            return [];

        List<IRuntimeExtension> extensions = [];
#pragma warning disable IL2070, IL2067, IL2075
        foreach (Type type in assembly.GetTypes().OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract ||
                type.IsInterface ||
                !typeof(IRuntimeExtension).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            extensions.Add((IRuntimeExtension)Activator.CreateInstance(type)!);
        }
#pragma warning restore IL2070, IL2067, IL2075
        return extensions;
    }
}

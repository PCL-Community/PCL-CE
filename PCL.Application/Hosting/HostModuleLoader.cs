// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace PCL.Application.Hosting;

public sealed record HostModuleLoadFailure(
    string AssemblyPath,
    string Message,
    Exception? Exception = null);

public sealed record HostModuleLoadResult(
    IReadOnlyList<string> LoadedModuleIds,
    IReadOnlyList<HostModuleLoadFailure> Failures)
{
    public bool IsSuccessful => Failures.Count == 0;
}

public static class HostModuleLoader
{
    [RequiresDynamicCode("Runtime Host Module loading depends on reflection and is not available in Native AOT publish paths.")]
    [RequiresUnreferencedCode("Runtime Host Module loading scans dynamically loaded assemblies, which cannot be statically analyzed by the trimmer.")]
    public static HostModuleLoadResult LoadFromAssemblyPaths(
        IPclHostBuilder builder,
        IEnumerable<string> assemblyPaths,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblyPaths);

        List<string> loadedModuleIds = [];
        List<HostModuleLoadFailure> failures = [];
        string root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(baseDirectory);

        foreach (string assemblyPath in assemblyPaths)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                continue;

            string resolvedPath = ResolveAssemblyPath(root, assemblyPath);
            try
            {
                if (!File.Exists(resolvedPath))
                {
                    failures.Add(new HostModuleLoadFailure(
                        resolvedPath,
                        "Host Module 程序集不存在。"));
                    continue;
                }

                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(resolvedPath);
                List<IPclHostModule> modules = CreateModules(assembly);
                if (modules.Count == 0)
                {
                    failures.Add(new HostModuleLoadFailure(
                        resolvedPath,
                        "未找到可加载的 IPclHostModule。"));
                    continue;
                }

                foreach (IPclHostModule module in modules)
                {
                    if (builder is PclHostBuilder concreteBuilder)
                        concreteBuilder.AddModule(module);
                    else
                        module.Configure(builder);

                    loadedModuleIds.Add(module.Id);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new HostModuleLoadFailure(
                    resolvedPath,
                    "加载 Host Module 失败：" + ex.Message,
                    ex));
            }
        }

        return new HostModuleLoadResult(loadedModuleIds.ToArray(), failures.ToArray());
    }

    private static string ResolveAssemblyPath(string baseDirectory, string assemblyPath)
    {
        string expanded = Environment.ExpandEnvironmentVariables(assemblyPath);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(baseDirectory, expanded));
    }

    [RequiresUnreferencedCode("Creating Host Modules from a runtime-loaded assembly requires reflection over unreferenced types.")]
    private static List<IPclHostModule> CreateModules(Assembly assembly)
    {
        List<IPclHostModule> modules = [];
        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract ||
                !typeof(IPclHostModule).IsAssignableFrom(type) ||
                type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is IPclHostModule module)
                modules.Add(module);
        }

        return modules;
    }
}

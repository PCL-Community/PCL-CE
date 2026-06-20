// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Platform.Abstractions.Java;
using PCL.Platform.Abstractions.Paths;

namespace PCL.Application.Minecraft.Java;

public sealed class JavaRuntimeDownloadPlanService(IJavaRuntimeMetadataProvider metadataProvider)
{
    private readonly IJavaRuntimeMetadataProvider _metadataProvider =
        metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));

    public async ValueTask<JavaRuntimeDownloadPlan> CreatePlanAsync(
        string requestedComponent,
        JavaRuntimePlatform platform,
        string runtimeRootDirectory,
        CancellationToken cancellationToken = default)
    {
        string indexJson = await _metadataProvider.GetRuntimeIndexAsync(cancellationToken).ConfigureAwait(false);
        JavaRuntimePackageDescriptor package =
            JavaRuntimePackagePlanner.SelectPackage(indexJson, platform, requestedComponent);
        string manifestJson = await _metadataProvider.GetManifestAsync(package.ManifestUrl, cancellationToken)
            .ConfigureAwait(false);
        return JavaRuntimePackagePlanner.CreateDownloadPlan(package, manifestJson, runtimeRootDirectory);
    }

    public ValueTask<JavaRuntimeDownloadPlan> CreatePlanAsync(
        string requestedComponent,
        JavaRuntimePlatform platform,
        IPlatformPathProvider pathProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        return CreatePlanAsync(
            requestedComponent,
            platform,
            GetDefaultRuntimeRootDirectory(pathProvider),
            cancellationToken);
    }

    public static string GetDefaultRuntimeRootDirectory(IPlatformPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        return Path.Combine(pathProvider.ApplicationDataDirectory, ".minecraft", "runtime");
    }
}

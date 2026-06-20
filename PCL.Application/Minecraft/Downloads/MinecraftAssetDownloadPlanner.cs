// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Assets;

namespace PCL.Application.Minecraft.Downloads;

public static class MinecraftAssetDownloadPlanner
{
    public static MinecraftAssetDownloadPlan CreatePlan(MinecraftAssetDownloadPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Assets);
        ArgumentNullException.ThrowIfNull(request.ExistingFiles);

        List<MinecraftAssetDownloadFile> files = [];
        foreach (MinecraftAssetToken asset in request.Assets)
        {
            if (!request.CheckHash && IsAlreadyUsable(asset, request.ExistingFiles))
                continue;

            files.Add(new MinecraftAssetDownloadFile
            {
                Url = MinecraftAssetListResolver.GetObjectUrl(asset.Hash),
                LocalPath = asset.LocalPath,
                Hash = asset.Hash,
                ActualSize = asset.Size == 0L ? -1 : asset.Size
            });
        }

        return new MinecraftAssetDownloadPlan(files);
    }

    private static bool IsAlreadyUsable(
        MinecraftAssetToken asset,
        IReadOnlyDictionary<string, MinecraftAssetFileState> existingFiles)
    {
        return existingFiles.TryGetValue(asset.LocalPath, out MinecraftAssetFileState? state) &&
               state.Exists &&
               (asset.Size == 0L || asset.Size == state.Length);
    }
}

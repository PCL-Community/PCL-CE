// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch;

public sealed record JavaRuntimeAcquisitionDecision
{
    public required bool CanAutoDownload { get; init; }
    public string? JavaVersionCode { get; init; }
    public string? DownloadComponent { get; init; }
    public JavaAcquisitionBlockReason BlockReason { get; init; }

    public static JavaRuntimeAcquisitionDecision AutoDownload(string javaVersionCode, string downloadComponent) =>
        new()
        {
            CanAutoDownload = true,
            JavaVersionCode = javaVersionCode,
            DownloadComponent = downloadComponent
        };

    public static JavaRuntimeAcquisitionDecision Blocked(JavaAcquisitionBlockReason reason) =>
        new()
        {
            CanAutoDownload = false,
            BlockReason = reason
        };
}

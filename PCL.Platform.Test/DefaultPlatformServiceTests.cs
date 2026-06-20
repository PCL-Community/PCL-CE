// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Platform.Abstractions.Processes;
using PCL.Platform.Paths;
using PCL.Platform.Processes;
using PCL.Platform.System;

namespace PCL.Platform.Test;

[TestClass]
public sealed class DefaultPlatformServiceTests
{
    [TestMethod]
    public void PathProvider_ShouldReturnAbsoluteDirectories()
    {
        DefaultPlatformPathProvider provider = new();

        Assert.IsTrue(Path.IsPathRooted(provider.ApplicationDataDirectory));
        Assert.IsTrue(Path.IsPathRooted(provider.CacheDirectory));
        Assert.IsTrue(Path.IsPathRooted(provider.TemporaryDirectory));
    }

    [TestMethod]
    public void SystemInfoProvider_ShouldReturnPortableRuntimeInfo()
    {
        DefaultSystemInfoProvider provider = new();

        Assert.IsFalse(string.IsNullOrWhiteSpace(provider.GetOperatingSystem().Name));
        Assert.IsGreaterThan(0, provider.GetCpuInfo().LogicalProcessorCount);
    }

    [TestMethod]
    public async Task ProcessService_ShouldCaptureOutput()
    {
        DefaultProcessService service = new();

        ProcessResult result = await service.RunAsync(
            new ProcessStartRequest
            {
                FileName = "dotnet",
                Arguments = ["--version"]
            },
            CancellationToken.None);

        Assert.AreEqual(0, result.ExitCode);
        Assert.IsTrue(result.StandardOutput.Any(char.IsDigit));
    }
}

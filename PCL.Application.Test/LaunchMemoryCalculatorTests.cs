// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Launching;
using PCL.Platform.Abstractions.System;

namespace PCL.Application.Test;

[TestClass]
public sealed class LaunchMemoryCalculatorTests
{
    [TestMethod]
    public void SliderValueToGigabytes_UsesWpfSegmentedFormula()
    {
        Assert.AreEqual(1.5d, LaunchMemoryCalculator.SliderValueToGigabytes(12), 0.0001d);
        Assert.AreEqual(2.0d, LaunchMemoryCalculator.SliderValueToGigabytes(13), 0.0001d);
        Assert.AreEqual(8.0d, LaunchMemoryCalculator.SliderValueToGigabytes(25), 0.0001d);
        Assert.AreEqual(16.0d, LaunchMemoryCalculator.SliderValueToGigabytes(33), 0.0001d);
        Assert.AreEqual(18.0d, LaunchMemoryCalculator.SliderValueToGigabytes(34), 0.0001d);
    }

    [TestMethod]
    public void ResolveMemoryMegabytes_UsesCustomValueAnd32BitCap()
    {
        int memory = LaunchMemoryCalculator.ResolveMemoryMegabytes(
            new LaunchMemoryRequest
            {
                MemorySolution = 1,
                CustomMemorySize = 13,
                MemoryInfo = new MemoryInfo(8L * 1024 * 1024 * 1024, 6L * 1024 * 1024 * 1024)
            });

        Assert.AreEqual(2048, memory);

        int capped = LaunchMemoryCalculator.ResolveMemoryMegabytes(
            new LaunchMemoryRequest
            {
                MemorySolution = 1,
                CustomMemorySize = 25,
                Is32BitJava = true
            });

        Assert.AreEqual(1024, capped);
    }
}

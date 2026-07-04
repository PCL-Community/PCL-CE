// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Platform.Abstractions.System;

namespace PCL.Application.Launching;

public enum LaunchMemoryProfile
{
    Vanilla,
    OptiFine,
    Modded
}

public sealed record LaunchMemoryRequest
{
    public int MemorySolution { get; init; }
    public int CustomMemorySize { get; init; }
    public MemoryInfo MemoryInfo { get; init; } = new(0, 0);
    public LaunchMemoryProfile Profile { get; init; }
    public int ModCount { get; init; }
    public bool Is32BitJava { get; init; }
}

public static class LaunchMemoryCalculator
{
    public static int ResolveMemoryMegabytes(LaunchMemoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        double gigabytes = request.MemorySolution == 1
            ? SliderValueToGigabytes(request.CustomMemorySize)
            : CalculateAutomaticGigabytes(request);
        if (request.Is32BitJava)
            gigabytes = Math.Min(1d, gigabytes);

        return Math.Max(256, (int)Math.Round(gigabytes * 1024d));
    }

    public static double SliderValueToGigabytes(int value) =>
        value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => value - 25d + 8d,
            _ => (value - 33d) * 2d + 16d
        };

    private static double CalculateAutomaticGigabytes(LaunchMemoryRequest request)
    {
        double ramAvailable = GetAvailableGigabytes(request.MemoryInfo);
        (double minimum, double target1, double target2, double target3) = GetTargets(request.Profile, request.ModCount);
        double ramGive = 0d;

        AddStage(ref ramGive, ref ramAvailable, target1, 1d);
        if (ramAvailable >= 0.1d)
        {
            AddStage(ref ramGive, ref ramAvailable, target2 - target1, 0.7d);
            if (ramAvailable >= 0.1d)
            {
                AddStage(ref ramGive, ref ramAvailable, target3 - target2, 0.4d);
                if (ramAvailable >= 0.1d)
                    AddStage(ref ramGive, ref ramAvailable, target3, 0.15d);
            }
        }

        return Math.Round(Math.Max(ramGive, minimum), 1);
    }

    private static void AddStage(ref double ramGive, ref double ramAvailable, double delta, double ratio)
    {
        ramGive += Math.Min(ramAvailable * ratio, delta);
        ramAvailable -= delta / ratio;
    }

    private static double GetAvailableGigabytes(MemoryInfo memoryInfo)
    {
        long bytes = memoryInfo.AvailableBytes > 0 ? memoryInfo.AvailableBytes : memoryInfo.TotalBytes;
        if (bytes <= 0)
            return 4d;

        return Math.Round(bytes / 1024d / 1024d / 1024d * 10d) / 10d;
    }

    private static (double Minimum, double Target1, double Target2, double Target3) GetTargets(
        LaunchMemoryProfile profile,
        int modCount) =>
        profile switch
        {
            LaunchMemoryProfile.Modded => (
                0.5d + Math.Max(0, modCount) / 150d,
                1.5d + Math.Max(0, modCount) / 90d,
                2.7d + Math.Max(0, modCount) / 50d,
                4.5d + Math.Max(0, modCount) / 25d),
            LaunchMemoryProfile.OptiFine => (0.5d, 1.5d, 3d, 5d),
            _ => (0.5d, 1.5d, 2.5d, 4d)
        };
}

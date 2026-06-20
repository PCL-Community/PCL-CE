// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch;

public readonly record struct JavaVersionRange(Version Minimum, Version Maximum)
{
    public static JavaVersionRange Any { get; } = new(new Version(0, 0, 0, 0), new Version(999, 999, 999, 999));
    public static Version Java7Maximum { get; } = new(1, 7, 999, 999);
    public static Version Java8Maximum { get; } = new(1, 8, 999, 999);

    public bool IsSatisfiable => Normalize(Minimum).CompareTo(Normalize(Maximum)) <= 0;

    public bool Contains(Version version)
    {
        Version normalizedVersion = Normalize(version);
        return normalizedVersion.CompareTo(Normalize(Minimum)) >= 0 &&
               normalizedVersion.CompareTo(Normalize(Maximum)) <= 0;
    }

    public JavaVersionRange WithMinimum(Version minimum) =>
        Normalize(minimum).CompareTo(Normalize(Minimum)) > 0
            ? this with { Minimum = minimum }
            : this;

    public JavaVersionRange WithMaximum(Version maximum) =>
        Normalize(maximum).CompareTo(Normalize(Maximum)) < 0
            ? this with { Maximum = maximum }
            : this;

    public static Version ForMajor(int majorVersion) =>
        majorVersion <= 8
            ? new Version(1, majorVersion, 0, 0)
            : new Version(majorVersion, 0, 0, 0);

    public static Version ForMajorMaximum(int majorVersion) =>
        majorVersion <= 8
            ? new Version(1, majorVersion, 999, 999)
            : new Version(majorVersion, 999, 999, 999);

    public static Version Normalize(Version version)
    {
        int minor = Math.Max(version.Minor, 0);
        int build = Math.Max(version.Build, 0);
        int revision = Math.Max(version.Revision, 0);

        return version.Major == 1 && minor >= 0
            ? new Version(minor, build, revision, 0)
            : new Version(version.Major, minor, build, revision);
    }
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Domain.Minecraft.Launch;

namespace PCL.Application.Minecraft.Launch;

public static class JavaRuntimeRequirementResolver
{
    private static readonly DateTimeOffset Minecraft1205SnapshotDate = new(2024, 4, 2, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Minecraft118SnapshotDate = new(2021, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Minecraft117SnapshotDate = new(2021, 5, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Minecraft152BoundaryDate = new(2013, 5, 1, 0, 0, 0, TimeSpan.Zero);

    public static JavaRequirementResolution Resolve(MinecraftLaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        JavaVersionRange range = JavaVersionRange.Any;
        string? recommendedComponent = null;

        ApplyMinecraftBaseRules(profile, ref range);
        ApplyManifestRecommendedJava(profile, ref range, ref recommendedComponent);
        ApplyOptiFineRules(profile, ref range);
        ApplyForgeRules(profile, ref range);

        JavaRequirementResolution? cleanroomFailure = ApplyCleanroomRules(profile, ref range);
        if (cleanroomFailure is not null)
            return cleanroomFailure;

        ApplyFabricRules(profile, ref range);
        ApplyLiteLoaderRules(profile, ref range);
        ApplyLabyModRules(profile, ref range);
        ApplyManifestMinimumJava(profile, ref range);

        return JavaRequirementResolution.Valid(range, recommendedComponent);
    }

    private static void ApplyMinecraftBaseRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if ((!profile.HasReliableVanillaVersion && profile.ReleaseTime >= Minecraft1205SnapshotDate) ||
            VersionAtLeast(profile, new Version(20, 0, 5)))
        {
            range = range.WithMinimum(JavaVersionRange.ForMajor(21));
        }
        else if ((!profile.HasReliableVanillaVersion && profile.ReleaseTime >= Minecraft118SnapshotDate) ||
                 (profile.HasReliableVanillaVersion && profile.VanillaVersion?.Major >= 18))
        {
            range = range.WithMinimum(JavaVersionRange.ForMajor(17));
        }
        else if ((!profile.HasReliableVanillaVersion && profile.ReleaseTime >= Minecraft117SnapshotDate) ||
                 (profile.HasReliableVanillaVersion && profile.VanillaVersion?.Major >= 17))
        {
            range = range.WithMinimum(JavaVersionRange.ForMajor(16));
        }
        else if (profile.ReleaseTime?.Year >= 2017)
        {
            range = range.WithMinimum(JavaVersionRange.ForMajor(8));
        }
        else if (profile.ReleaseTime <= Minecraft152BoundaryDate && profile.ReleaseTime?.Year >= 2001)
        {
            range = range.WithMaximum(JavaVersionRange.Java8Maximum);
        }
    }

    private static void ApplyManifestRecommendedJava(
        MinecraftLaunchProfile profile,
        ref JavaVersionRange range,
        ref string? recommendedComponent)
    {
        if (profile.ManifestJavaMajorVersion is not >= 22)
            return;

        range = range.WithMinimum(JavaVersionRange.ForMajor(profile.ManifestJavaMajorVersion.Value));
        recommendedComponent = string.IsNullOrWhiteSpace(profile.ManifestJavaComponent)
            ? null
            : profile.ManifestJavaComponent;
    }

    private static void ApplyOptiFineRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (!profile.HasOptiFine || !profile.HasReliableVanillaVersion || profile.VanillaVersion is null)
            return;

        if (profile.VanillaVersion.Major < 7)
        {
            range = range.WithMaximum(JavaVersionRange.Java8Maximum);
        }
        else if (profile.VanillaVersion.Major >= 8 && profile.VanillaVersion.Major < 12)
        {
            range = range
                .WithMinimum(JavaVersionRange.ForMajor(8))
                .WithMaximum(JavaVersionRange.Java8Maximum);
        }
        else if (profile.VanillaVersion.Major == 12)
        {
            range = range.WithMaximum(JavaVersionRange.Java8Maximum);
        }
    }

    private static void ApplyForgeRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (!profile.HasForge)
            return;

        if (VersionAtLeast(profile, new Version(6, 0, 1)) &&
            VersionAtMost(profile, new Version(7, 0, 2)))
        {
            range = range
                .WithMinimum(JavaVersionRange.ForMajor(7))
                .WithMaximum(JavaVersionRange.Java7Maximum);
        }
        else if (!profile.HasReliableVanillaVersion || profile.VanillaVersion is null || profile.VanillaVersion.Major <= 12)
        {
            range = range.WithMaximum(JavaVersionRange.Java8Maximum);
        }
        else if (profile.VanillaVersion.Major <= 14)
        {
            range = range
                .WithMinimum(JavaVersionRange.ForMajor(8))
                .WithMaximum(JavaVersionRange.ForMajorMaximum(10));
        }
        else if (profile.VanillaVersion.Major == 15)
        {
            range = range
                .WithMinimum(JavaVersionRange.ForMajor(8))
                .WithMaximum(JavaVersionRange.ForMajorMaximum(15));
        }
        else if (VersionTextComparer.Compare(profile.ForgeVersion, "34.0.0") >= 0 &&
                 VersionTextComparer.Compare("36.2.25", profile.ForgeVersion) >= 0)
        {
            range = range.WithMaximum(new Version(1, 8, 0, 321));
        }
        else if (profile.VanillaVersion.Major >= 18 &&
                 profile.VanillaVersion.Major < 19 &&
                 profile.HasOptiFine)
        {
            range = range.WithMaximum(JavaVersionRange.ForMajorMaximum(18));
        }
    }

    private static JavaRequirementResolution? ApplyCleanroomRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (!profile.HasCleanroom)
            return null;

        string cleanroomText = profile.CleanroomVersion?.Split('-', 2)[0] ?? string.Empty;
        if (!Version.TryParse(cleanroomText, out Version? cleanroomVersion))
        {
            return JavaRequirementResolution.Invalid(
                JavaRequirementFailureReason.InvalidCleanroomVersion,
                $"无法解析 Cleanroom 版本号：{profile.CleanroomVersion}",
                range);
        }

        range = cleanroomVersion < new Version(0, 5, 0, 0)
            ? range.WithMinimum(JavaVersionRange.ForMajor(21))
            : range.WithMinimum(JavaVersionRange.ForMajor(25));

        return null;
    }

    private static void ApplyFabricRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (!profile.HasFabric || !profile.HasReliableVanillaVersion || profile.VanillaVersion is null)
            return;

        if (profile.VanillaVersion.Major >= 15 && profile.VanillaVersion.Major <= 16)
            range = range.WithMinimum(JavaVersionRange.ForMajor(8));
        else if (profile.VanillaVersion.Major >= 18)
            range = range.WithMinimum(JavaVersionRange.ForMajor(17));
    }

    private static void ApplyLiteLoaderRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (profile.HasLiteLoader && profile.HasReliableVanillaVersion)
            range = range.WithMaximum(JavaVersionRange.ForMajorMaximum(8));
    }

    private static void ApplyLabyModRules(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (!profile.HasLabyMod)
            return;

        range = range
            .WithMinimum(JavaVersionRange.ForMajor(21))
            .WithMaximum(JavaVersionRange.Any.Maximum);
    }

    private static void ApplyManifestMinimumJava(MinecraftLaunchProfile profile, ref JavaVersionRange range)
    {
        if (profile.ManifestJavaMajorVersion is not { } majorVersion)
            return;

        range = range.WithMinimum(JavaVersionRange.ForMajor(majorVersion));
        if (!range.IsSatisfiable)
            range = range.WithMaximum(JavaVersionRange.Any.Maximum);
    }

    private static bool VersionAtLeast(MinecraftLaunchProfile profile, Version version) =>
        profile.HasReliableVanillaVersion &&
        profile.VanillaVersion is not null &&
        profile.VanillaVersion.CompareTo(version) >= 0;

    private static bool VersionAtMost(MinecraftLaunchProfile profile, Version version) =>
        profile.HasReliableVanillaVersion &&
        profile.VanillaVersion is not null &&
        profile.VanillaVersion.CompareTo(version) <= 0;
}

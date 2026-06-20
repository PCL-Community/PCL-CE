// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace PCL.Application.Minecraft.Launch;

public static class JavaRuntimeAcquisitionPlanner
{
    private static readonly Version Java8Update140 = new(8, 0, 140, 0);
    private static readonly Version Java8Update321 = new(8, 0, 321, 0);

    public static JavaRuntimeAcquisitionDecision Plan(JavaRequirementResolution requirement, bool hasForge)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        return Plan(requirement.Range, requirement.RecommendedComponent, hasForge);
    }

    public static JavaRuntimeAcquisitionDecision Plan(
        JavaVersionRange range,
        string? recommendedComponent,
        bool hasForge)
    {
        Version normalizedMinimum = JavaVersionRange.Normalize(range.Minimum);
        Version normalizedMaximum = JavaVersionRange.Normalize(range.Maximum);

        if (normalizedMinimum.Major >= 9)
        {
            string code = normalizedMinimum.Major.ToString(CultureInfo.InvariantCulture);
            return JavaRuntimeAcquisitionDecision.AutoDownload(code, recommendedComponent ?? code);
        }

        if (normalizedMaximum.Major < 8)
        {
            return JavaRuntimeAcquisitionDecision.Blocked(
                hasForge
                    ? JavaAcquisitionBlockReason.LegacyForgeNeedsFixerOrJava7
                    : JavaAcquisitionBlockReason.LegacyJava7Required);
        }

        if (normalizedMinimum.CompareTo(Java8Update140) > 0 &&
            normalizedMaximum.CompareTo(Java8Update321) < 0)
        {
            return JavaRuntimeAcquisitionDecision.Blocked(JavaAcquisitionBlockReason.Java8Update141To320Required);
        }

        if (normalizedMinimum.CompareTo(Java8Update140) > 0)
            return JavaRuntimeAcquisitionDecision.Blocked(JavaAcquisitionBlockReason.Java8Update141OrLaterRequired);

        return JavaRuntimeAcquisitionDecision.AutoDownload("8", recommendedComponent ?? "8");
    }
}

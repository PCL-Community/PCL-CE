// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch;

public sealed record JavaRequirementResolution
{
    public required bool Success { get; init; }
    public required JavaVersionRange Range { get; init; }
    public string? RecommendedComponent { get; init; }
    public JavaRequirementFailureReason FailureReason { get; init; }
    public string? Detail { get; init; }

    public static JavaRequirementResolution Valid(JavaVersionRange range, string? recommendedComponent = null) =>
        new()
        {
            Success = true,
            Range = range,
            RecommendedComponent = recommendedComponent
        };

    public static JavaRequirementResolution Invalid(
        JavaRequirementFailureReason reason,
        string detail,
        JavaVersionRange range) =>
        new()
        {
            Success = false,
            Range = range,
            FailureReason = reason,
            Detail = detail
        };
}

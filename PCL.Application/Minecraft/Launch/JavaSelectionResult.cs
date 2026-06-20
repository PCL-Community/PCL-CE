// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Domain.Minecraft.Java;

namespace PCL.Application.Minecraft.Launch;

public sealed record JavaSelectionResult
{
    public required bool Success { get; init; }
    public required JavaRequirementResolution Requirement { get; init; }
    public JavaRuntimeCandidate? SelectedJava { get; init; }
    public JavaSelectionFailureReason FailureReason { get; init; }
    public string? Detail { get; init; }
    public string? SuggestedDownloadComponent { get; init; }

    public static JavaSelectionResult Selected(
        JavaRequirementResolution requirement,
        JavaRuntimeCandidate selectedJava) =>
        new()
        {
            Success = true,
            Requirement = requirement,
            SelectedJava = selectedJava
        };

    public static JavaSelectionResult Failed(
        JavaRequirementResolution requirement,
        JavaSelectionFailureReason reason,
        string? detail = null,
        string? suggestedDownloadComponent = null) =>
        new()
        {
            Success = false,
            Requirement = requirement,
            FailureReason = reason,
            Detail = detail,
            SuggestedDownloadComponent = suggestedDownloadComponent
        };
}

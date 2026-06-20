// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Domain.Minecraft.Java;
using PCL.Domain.Minecraft.Launch;
using PCL.Platform.Abstractions.Java;

namespace PCL.Application.Minecraft.Launch;

public sealed class JavaSelectionService(IJavaLocator javaLocator)
{
    private readonly IJavaLocator _javaLocator = javaLocator ?? throw new ArgumentNullException(nameof(javaLocator));

    public async Task<JavaSelectionResult> SelectAsync(
        MinecraftLaunchProfile profile,
        CancellationToken cancellationToken = default)
    {
        JavaRequirementResolution requirement = JavaRuntimeRequirementResolver.Resolve(profile);
        if (!requirement.Success)
        {
            return JavaSelectionResult.Failed(
                requirement,
                JavaSelectionFailureReason.InvalidVersionMetadata,
                requirement.Detail);
        }

        IReadOnlyList<JavaRuntimeCandidate> candidates;
        try
        {
            candidates = await _javaLocator.FindAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return JavaSelectionResult.Failed(
                requirement,
                JavaSelectionFailureReason.LocatorFailed,
                ex.Message);
        }

        JavaRuntimeCandidate? selectedJava = SelectBestCandidate(candidates, requirement.Range);
        if (selectedJava is not null)
            return JavaSelectionResult.Selected(requirement, selectedJava);

        JavaRuntimeAcquisitionDecision acquisitionDecision =
            JavaRuntimeAcquisitionPlanner.Plan(requirement, profile.HasForge);
        return JavaSelectionResult.Failed(
            requirement,
            JavaSelectionFailureReason.NoCompatibleJava,
            suggestedDownloadComponent: acquisitionDecision.DownloadComponent);
    }

    public static JavaRuntimeCandidate? SelectBestCandidate(
        IEnumerable<JavaRuntimeCandidate> candidates,
        JavaVersionRange range)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(candidate =>
                candidate.IsAvailable &&
                candidate.IsEnabled &&
                range.Contains(candidate.Installation.Version))
            .OrderBy(static candidate => candidate.Installation.MajorVersion)
            .ThenBy(static candidate => candidate.Installation.IsJre)
            .ThenBy(static candidate => candidate.Installation.Brand)
            .ThenByDescending(static candidate => JavaVersionRange.Normalize(candidate.Installation.Version))
            .FirstOrDefault();
    }

}

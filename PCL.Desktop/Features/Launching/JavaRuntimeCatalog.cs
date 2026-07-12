// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Minecraft.Launch;
using PCL.Application.Settings;
using PCL.Domain.Minecraft.Java;
using PCL.Domain.Minecraft.Launch;
using PCL.Platform.Java;

namespace PCL.Desktop.Features.Launching;

/// <summary>
/// Shared Java discovery used by Settings and launch so custom roots / disabled
/// flags stay consistent with <see cref="Features.Settings.Views.PageSetupJava"/>.
/// </summary>
internal static class JavaRuntimeCatalog
{
    public static async Task<IReadOnlyList<JavaRuntimeCandidate>> LoadAsync(
        LauncherSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string[] customRoots = ReadCustomJavaRoots(settings);
        List<JavaRuntimeCandidate> candidates = [];

        IReadOnlyList<JavaRuntimeCandidate> autoCandidates =
            await new FileSystemJavaLocator().FindAllAsync(cancellationToken).ConfigureAwait(false);
        candidates.AddRange(autoCandidates);

        if (customRoots.Length > 0)
        {
            IReadOnlyList<JavaRuntimeCandidate> manualCandidates =
                await new FileSystemJavaLocator(customRoots).FindAllAsync(cancellationToken).ConfigureAwait(false);
            candidates.AddRange(manualCandidates.Select(static candidate => candidate with
            {
                Source = JavaSource.ManualAdded
            }));
        }

        Dictionary<string, JavaRuntimeCandidate> merged = new(GetPathComparer());
        foreach (JavaRuntimeCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = candidate.Installation.JavaExecutablePath;
            bool disabled = settings.GetBooleanOption(
                LauncherSettingKeys.JavaDisabled(candidate.Installation.JavaExecutablePath));
            JavaRuntimeCandidate withState = candidate with
            {
                IsEnabled = !disabled && candidate.IsEnabled,
                IsAvailable = candidate.IsAvailable
            };

            if (!merged.TryGetValue(key, out JavaRuntimeCandidate? existing) ||
                withState.Source == JavaSource.ManualAdded ||
                existing.Source != JavaSource.ManualAdded)
            {
                merged[key] = withState;
            }
        }

        return merged.Values
            .OrderByDescending(static c => c.IsEnabled)
            .ThenByDescending(static c => c.Installation.MajorVersion)
            .ThenBy(static c => c.Installation.Brand.ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static c => c.Installation.JavaHome, GetPathComparer())
            .ToArray();
    }

    public static JavaRuntimeCandidate? SelectBest(
        IEnumerable<JavaRuntimeCandidate> candidates,
        JavaVersionRange range) =>
        JavaSelectionService.SelectBestCandidate(
            candidates.Where(static c => c.IsAvailable && c.IsEnabled),
            range);

    public static string[] ReadCustomJavaRoots(LauncherSettings settings)
    {
        if (!settings.TryGetTextOption(LauncherSettingKeys.JavaCustomRoots, out string? raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Distinct(GetPathComparer())
            .ToArray();
    }

    public static bool IsJavaPathEnabled(LauncherSettings settings, string javaExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath))
            return false;
        return !settings.GetBooleanOption(LauncherSettingKeys.JavaDisabled(javaExecutablePath));
    }

    public static bool TryResolveExistingJavaPath(string? path, out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string trimmed = path.Trim();
        if (File.Exists(trimmed))
        {
            resolvedPath = Path.GetFullPath(trimmed);
            return true;
        }

        // Settings may store java.exe while PreferJavaExecutable wants javaw.exe (or reverse).
        string? directory = Path.GetDirectoryName(trimmed);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;

        foreach (string name in OperatingSystem.IsWindows()
                     ? new[] { "javaw.exe", "java.exe" }
                     : new[] { "java" })
        {
            string candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
            {
                resolvedPath = Path.GetFullPath(candidate);
                return true;
            }
        }

        return false;
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

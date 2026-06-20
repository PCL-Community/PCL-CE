// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Domain.Minecraft.Java;

namespace PCL.Application.Minecraft.Java;

public static class JavaPreferenceParser
{
    public const string LegacyUseGlobalText = "使用全局设置";

    public static JavaPreference Parse(string? rawPreference, string? relativePathBaseDirectory = null)
    {
        JavaPreference preference = TryParseJson(rawPreference) ?? ParseLegacy(rawPreference);
        return Normalize(preference, relativePathBaseDirectory);
    }

    private static JavaPreference? TryParseJson(string? rawPreference)
    {
        if (string.IsNullOrWhiteSpace(rawPreference) || !rawPreference.TrimStart().StartsWith('{'))
            return null;

        try
        {
            JsonObject? json = JsonNode.Parse(rawPreference)?.AsObject();
            string? kind = json?["kind"]?.GetValue<string>();
            return kind switch
            {
                "auto" => new AutoSelectJavaPreference(),
                "global" => new UseGlobalJavaPreference(),
                "exist" => ReadString(json, "JavaExePath") is { } path
                    ? new ExistingJavaPreference(path)
                    : null,
                "relative" => ReadString(json, "RelativePath") is { } relativePath
                    ? new UseRelativeJavaPreference(relativePath)
                    : null,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static JavaPreference ParseLegacy(string? rawPreference)
    {
        string? trimmed = rawPreference?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new AutoSelectJavaPreference();

        return string.Equals(trimmed, LegacyUseGlobalText, StringComparison.Ordinal)
            ? new UseGlobalJavaPreference()
            : new ExistingJavaPreference(trimmed);
    }

    private static JavaPreference Normalize(JavaPreference preference, string? relativePathBaseDirectory)
    {
        return preference switch
        {
            ExistingJavaPreference existing when !Path.IsPathRooted(existing.JavaExecutablePath) =>
                new UseGlobalJavaPreference(),
            UseRelativeJavaPreference relative when !IsSafeRelativePath(relative.RelativePath, relativePathBaseDirectory) =>
                new UseGlobalJavaPreference(),
            _ => preference
        };
    }

    private static bool IsSafeRelativePath(string relativePath, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            string.IsNullOrWhiteSpace(baseDirectory) ||
            Path.IsPathRooted(relativePath))
            return false;

        try
        {
            string baseFullPath = EnsureTrailingSeparator(Path.GetFullPath(baseDirectory));
            string resolvedPath = Path.GetFullPath(Path.Combine(baseFullPath, relativePath));
            return resolvedPath.StartsWith(
                baseFullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string directory)
    {
        return directory.EndsWith(Path.DirectorySeparatorChar) || directory.EndsWith(Path.AltDirectorySeparatorChar)
            ? directory
            : string.Concat(directory, Path.DirectorySeparatorChar);
    }

    private static string? ReadString(JsonObject? json, string propertyName)
    {
        string? value = json?[propertyName]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

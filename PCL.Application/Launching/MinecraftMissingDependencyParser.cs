// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;

namespace PCL.Application.Launching;

public sealed record MinecraftMissingDependency(string Name, string ModId, string? RequiredVersion);

public static partial class MinecraftMissingDependencyParser
{
    public static IReadOnlyList<MinecraftMissingDependency> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        List<MinecraftMissingDependency> dependencies = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string? line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            Match match = ChineseMissingRegex().Match(line);
            if (!match.Success)
                match = ChineseWrongVersionRegex().Match(line);
            if (!match.Success)
                match = EnglishMissingRegex().Match(line);
            if (!match.Success)
                match = EnglishWrongVersionRegex().Match(line);
            if (!match.Success)
                match = EnglishAnyRegex().Match(line);
            if (!match.Success)
                continue;

            string name = match.Groups["name"].Value.Trim(' ', '\'', '"');
            string id = match.Groups["id"].Success ? match.Groups["id"].Value.Trim() : name;
            string version = match.Groups["version"].Success ? match.Groups["version"].Value.Trim() : string.Empty;
            version = version
                .Replace("及以上版本", string.Empty, StringComparison.Ordinal)
                .Replace("或更高版本", string.Empty, StringComparison.Ordinal)
                .Trim();
            if (version is "任意版本" or "任何版本" or "any version" or "any")
                version = string.Empty;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;

            dependencies.Add(new MinecraftMissingDependency(name, id, string.IsNullOrWhiteSpace(version) ? null : version));
        }

        return dependencies;
    }

    [GeneratedRegex(@"需要\s*(?:模组\s*)?(?<name>'[^']+'|[^\s(]+)\s*(?:\((?<id>[^)]*)\))?\s*的\s*(?<version>\S+?(?:\s*(?:及以上版本|或更高版本))?|任意版本|任何版本|any\s*version|any)\s*[，,\s]*但没有安装它", RegexOptions.IgnoreCase)]
    private static partial Regex ChineseMissingRegex();

    [GeneratedRegex(@"需要\s*(?:模组\s*)?(?<name>'[^']+'|[^\s(]+)\s*(?:\((?<id>[^)]*)\))?\s*的\s*(?<version>[^\s，,]+)\s*及以上版本[，,\s]*但已经安装了的版本\s*\S+\s*不对", RegexOptions.IgnoreCase)]
    private static partial Regex ChineseWrongVersionRegex();

    [GeneratedRegex(@"requires\s+(?:mod\s+)?(?<name>'[^']+'|[^\s(]+)\s*(?:\((?<id>[^)]*)\))?\s+version\s+(?<version>\S+)\s+or\s+later[^.]*\bis\s+missing", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishMissingRegex();

    [GeneratedRegex(@"requires\s+(?:mod\s+)?(?<name>'[^']+'|[^\s(]+)\s*(?:\((?<id>[^)]*)\))?\s+version\s+(?<version>\S+)\s+or\s+later[^.]*\b(?:but|however)\s+\S+\s+is\s+installed", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishWrongVersionRegex();

    [GeneratedRegex(@"requires\s+(?:mod\s+)?(?<name>'[^']+'|[^\s(]+)\s*(?:\((?<id>[^)]*)\))?\s+(?:any\s+version|any)\s*[^.]*\bis\s+missing", RegexOptions.IgnoreCase)]
    private static partial Regex EnglishAnyRegex();
}

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using System.IO.Enumeration;

namespace PCL.Application.Instances;

public static class InstanceExportService
{
    public static Task ExportAsync(InstanceExportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.GameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetArchivePath);

        return Task.Run(() => ExportCore(request, cancellationToken), cancellationToken);
    }

    private static void ExportCore(InstanceExportRequest request, CancellationToken cancellationToken)
    {
        string instanceDirectory = Path.GetFullPath(request.InstanceDirectory);
        string gameDirectory = Path.GetFullPath(request.GameDirectory);
        string targetArchive = Path.GetFullPath(request.TargetArchivePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetArchive) ?? Directory.GetCurrentDirectory());

        string tempArchive = targetArchive + ".tmp";
        if (File.Exists(tempArchive))
            File.Delete(tempArchive);

        ExportRuleSet rules = ExportRuleSet.Create(request.Rules);
        HashSet<string> addedEntries = new(StringComparer.OrdinalIgnoreCase);
        using (ZipArchive archive = ZipFile.Open(tempArchive, ZipArchiveMode.Create))
        {
            if (Directory.Exists(gameDirectory))
                AddDirectoryByRules(archive, gameDirectory, rules, addedEntries, cancellationToken);

            if (Directory.Exists(instanceDirectory))
                AddDirectory(archive, instanceDirectory, Path.GetFileName(instanceDirectory), addedEntries, cancellationToken);
        }

        if (File.Exists(targetArchive))
            File.Delete(targetArchive);
        File.Move(tempArchive, targetArchive);
    }

    private static void AddDirectoryByRules(
        ZipArchive archive,
        string rootDirectory,
        ExportRuleSet rules,
        HashSet<string> addedEntries,
        CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(rootDirectory, file));
            if (!rules.ShouldInclude(relativePath))
                continue;

            AddFile(archive, file, relativePath, addedEntries);
        }
    }

    private static void AddDirectory(
        ZipArchive archive,
        string rootDirectory,
        string archiveRoot,
        HashSet<string> addedEntries,
        CancellationToken cancellationToken)
    {
        foreach (string file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relativePath = NormalizeRelativePath(Path.GetRelativePath(rootDirectory, file));
            AddFile(archive, file, CombineArchivePath(archiveRoot, relativePath), addedEntries);
        }
    }

    private static void AddFile(ZipArchive archive, string file, string entryName, HashSet<string> addedEntries)
    {
        string normalizedEntry = NormalizeRelativePath(entryName);
        if (!addedEntries.Add(normalizedEntry))
            return;

        archive.CreateEntryFromFile(file, normalizedEntry, CompressionLevel.Fastest);
    }

    private static string CombineArchivePath(string left, string right) =>
        string.IsNullOrWhiteSpace(left) ? NormalizeRelativePath(right) : NormalizeRelativePath(left) + "/" + NormalizeRelativePath(right);

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private sealed class ExportRuleSet
    {
        private readonly List<string> _includeRules;
        private readonly List<string> _excludeRules;

        private ExportRuleSet(List<string> includeRules, List<string> excludeRules)
        {
            _includeRules = includeRules;
            _excludeRules = excludeRules;
        }

        public static ExportRuleSet Create(IEnumerable<string> rules)
        {
            List<string> includeRules = [];
            List<string> excludeRules = [];
            foreach (string rawRule in rules)
            {
                string rule = NormalizeRule(rawRule);
                if (string.IsNullOrWhiteSpace(rule))
                    continue;

                if (rule[0] == '!')
                    excludeRules.Add(rule[1..]);
                else
                    includeRules.Add(rule);
            }

            return new ExportRuleSet(includeRules, excludeRules);
        }

        public bool ShouldInclude(string relativePath)
        {
            string normalizedPath = NormalizeRelativePath(relativePath);
            bool included = _includeRules.Count == 0 || _includeRules.Any(rule => IsMatch(normalizedPath, rule));
            return included && !_excludeRules.Any(rule => IsMatch(normalizedPath, rule));
        }

        private static string NormalizeRule(string rawRule)
        {
            string rule = rawRule.Trim();
            if (rule.Length == 0 || rule[0] == '#' || rule[0] == '=')
                return string.Empty;

            return NormalizeRelativePath(rule);
        }

        private static bool IsMatch(string relativePath, string rule)
        {
            if (rule.EndsWith('/'))
                return relativePath.StartsWith(rule, StringComparison.OrdinalIgnoreCase);

            string normalizedRule = rule.Replace("**", "*", StringComparison.Ordinal);
            return FileSystemName.MatchesSimpleExpression(normalizedRule, relativePath, ignoreCase: true);
        }
    }
}

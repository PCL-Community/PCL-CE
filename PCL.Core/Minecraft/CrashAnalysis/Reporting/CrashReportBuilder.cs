using System.Text.Json;

namespace PCL.Core.Minecraft.CrashAnalysis;

public partial class CrashReportBuilder
{
    public static CrashReportPackage Build(CrashAnalysisResult result, CrashReportBuildOptions options)
    {
        var sensitiveValues = _CollectSensitiveValues(result, options).ToList();
        var entries = new List<CrashReportEntry>();
        if (options.Markdown is not null)
            entries.Add(new CrashReportEntry
            {
                FileName = "crash-analysis.md",
                Content = Encoding.UTF8.GetBytes(_Sanitize(options.Markdown.Content, sensitiveValues))
            });

        entries.Add(new CrashReportEntry
        {
            FileName = "summary.txt",
            Content = Encoding.UTF8.GetBytes(_Sanitize(Build(result), sensitiveValues))
        });

        entries.Add(new CrashReportEntry
        {
            FileName = "diagnosis.json",
            Content = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                createdAt = result.CreatedAt,
                topDiagnosis = result.TopDiagnosis?.Code.ToString(),
                diagnoses = result.Diagnoses.Select(d => new
                {
                    code = d.Code.ToString(),
                    category = d.Category.ToString(),
                    confidence = d.Confidence.ToString(),
                    score = d.Score,
                    evidence = d.Evidence.Select(e => new
                    {
                        fact = e.FactKind.ToString(),
                        source = _Sanitize(e.SourceName ?? string.Empty, sensitiveValues),
                        line = e.LineNumber,
                        weight = e.Weight,
                        summary = _Sanitize(e.Summary ?? string.Empty, sensitiveValues)
                    })
                }),
                facts = result.Facts.Facts
                    .Where(f => f.Visibility != CrashFactVisibility.Hidden)
                    .Select(f => new
                    {
                        kind = f.Kind.ToString(),
                        value = _Sanitize(f.Value, sensitiveValues),
                        confidence = f.Confidence.ToString(),
                        strength = f.Strength.ToString(),
                        scope = f.Scope.ToString(),
                        visibility = f.Visibility.ToString(),
                        properties = f.Properties.ToDictionary(
                            pair => pair.Key,
                            pair => _Sanitize(pair.Value, sensitiveValues),
                            StringComparer.OrdinalIgnoreCase)
                    })
            }, new JsonSerializerOptions { WriteIndented = true })
        });

        var usedLogNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        entries.AddRange(
            from document in result.LogBundle.Documents
            let name = _UniqueLogEntryName(document.Name, usedLogNames)
            let text = _Sanitize(document.Text, sensitiveValues)
            select new CrashReportEntry
            {
                FileName = name,
                Content = Encoding.UTF8.GetBytes(text)
            });

        return new CrashReportPackage(entries);
    }

    private static string _UniqueLogEntryName(string name, HashSet<string> usedNames)
    {
        var safeName = _SafeName(name);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "log.txt";

        var entryName = "logs/" + safeName;
        if (usedNames.Add(entryName)) return entryName;

        var fileName = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "log";

        for (var index = 2;; index++)
        {
            entryName = "logs/" + fileName + "-" + index + extension;
            if (usedNames.Add(entryName)) return entryName;
        }
    }

    private static string _SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars()
            .Concat(['/', '\\', ':'])
            .Distinct()
            .ToArray();
        var safe = invalid.Aggregate(name, (current, c) => current.Replace(c, '_'));
        safe = safe.Trim();
        return string.IsNullOrWhiteSpace(safe) ? "log.txt" : safe;
    }

    private static IReadOnlyList<string> _CollectSensitiveValues(
        CrashAnalysisResult result,
        CrashReportBuildOptions options)
    {
        var values = new List<string>();
        values.AddRange(options.SensitiveValues);

        var context = result.Request.RuntimeContext;
        Add(context.AccountName);
        Add(context.InstancePath);
        Add(context.JavaPath);
        Add(context.LauncherId);
        Add(context.InstanceName);

        foreach (var argument in context.LaunchArguments)
        {
            if (_LooksSensitiveLaunchArgument(argument))
                Add(argument);
            foreach (Match match in _SensitiveLaunchArgumentValueRegex().Matches(argument))
                Add(match.Groups["value"].Value);
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(static value => value.Length)
            .ToList();

        void Add(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }
    }

    private static bool _LooksSensitiveLaunchArgument(string argument)
    {
        return argument.Contains("accessToken", StringComparison.OrdinalIgnoreCase)
               || argument.Contains("uuid", StringComparison.OrdinalIgnoreCase)
               || argument.Contains("authlib", StringComparison.OrdinalIgnoreCase)
               || argument.Contains("clientId", StringComparison.OrdinalIgnoreCase)
               || argument.Contains("xuid", StringComparison.OrdinalIgnoreCase)
               || argument.Contains("username", StringComparison.OrdinalIgnoreCase);
    }

    private static string _Sanitize(string text, IReadOnlyList<string> sensitiveValues)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = sensitiveValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Aggregate(text, (current, value) =>
                current.Replace(value, "<redacted>", StringComparison.OrdinalIgnoreCase));

        text = _SensitiveLaunchArgumentRegex().Replace(text, "${prefix}<redacted>");
        text = _UserPathRegex().Replace(text, "$1<user>$2");

        return text;
    }

    public static string Build(CrashAnalysisResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Crash diagnosis summary");
        builder.AppendLine("CreatedAt: " + result.CreatedAt.ToString("O"));
        foreach (var diagnosis in result.Diagnoses)
            builder.AppendLine("- " + diagnosis.Code + " / " + diagnosis.Confidence + " / " + diagnosis.Score);
        return builder.ToString();
    }

    [GeneratedRegex(
        @"(?i)(?<prefix>(?:--(?:accessToken|uuid|username|clientId|xuid)\s+)|(?:(?:accessToken|uuid|username|clientId|xuid|authlib)[=:]\s*))(?<value>[^\s""']+)")]
    private static partial Regex _SensitiveLaunchArgumentRegex();

    [GeneratedRegex(
        @"(?i)(?<prefix>(?:--(?:accessToken|uuid|username|clientId|xuid)\s+)|(?:(?:accessToken|uuid|username|clientId|xuid|authlib)[=:]\s*))(?<value>[^\s""']+)")]
    private static partial Regex _SensitiveLaunchArgumentValueRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)([^\\\r\n]+)(\\)")]
    private static partial Regex _UserPathRegex();
}
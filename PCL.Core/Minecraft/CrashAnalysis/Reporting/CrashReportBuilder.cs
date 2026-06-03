using System.Text.Json;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashReportBuilder
{
    public static CrashReportPackage Build(CrashAnalysisResult result, CrashReportBuildOptions options)
    {
        var entries = new List<CrashReportEntry>();
        if (options.Markdown is not null)
            entries.Add(new CrashReportEntry
            {
                FileName = "crash-analysis.md",
                Content = Encoding.UTF8.GetBytes(options.Markdown.Content)
            });

        entries.Add(new CrashReportEntry
        {
            FileName = "summary.txt",
            Content = Encoding.UTF8.GetBytes(CrashReportSummaryBuilder.Build(result))
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
                        { fact = e.FactKind.ToString(), source = e.SourceName, line = e.LineNumber, weight = e.Weight })
                }),
                facts = result.Facts.Facts.Select(f => new { kind = f.Kind.ToString(), value = f.Value })
            }, new JsonSerializerOptions { WriteIndented = true })
        });

        entries.AddRange(from document in result.LogBundle.Documents
            let name = "logs/" + _SafeName(document.Name)
            let text = CrashReportSanitizer.Sanitize(document.Text, options.SensitiveValues)
            select new CrashReportEntry { FileName = name, Content = Encoding.UTF8.GetBytes(text) });

        return new CrashReportPackage(entries);
    }

    private static string _SafeName(string name)
    {
        return Path.GetInvalidFileNameChars()
            .Aggregate(name, (current, c) => current.Replace(c, '_'));
    }
}
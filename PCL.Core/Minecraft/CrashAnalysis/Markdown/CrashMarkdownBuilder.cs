namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashMarkdownBuilder
{
    private const int MaxKeyEvidence = 8;
    private const int MaxDetailedEvidence = 6;
    private const int MaxFactAppendix = 30;

    public static CrashMarkdownDocument Build(
        CrashAnalysisResult result,
        CrashPresentationModel presentation,
        CrashMarkdownLocalizer localize,
        CrashMarkdownExportOptions? options = null)
    {
        options ??= new CrashMarkdownExportOptions();

        var text = new CrashTextLocalizer(localize);
        var builder = new StringBuilder();
        var title = text.Text("Crash.Markdown.Title", null);

        builder.AppendLine("# " + title);
        builder.AppendLine();
        IReadOnlyDictionary<string, string>? parameters = _Args(("Time", result.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")));
        builder.AppendLine(text.Text("Crash.Markdown.GeneratedAt", parameters));
        builder.AppendLine();

        _AppendOverview(builder, presentation, text);
        _AppendPrimaryDiagnosis(builder, presentation, text);
        _AppendOtherDiagnoses(builder, presentation, text);
        _AppendSuggestions(builder, presentation, text);

        if (options.IncludeEvidence)
            _AppendEvidence(builder, presentation, text);

        if (options.IncludeEnvironment)
            _AppendEnvironment(builder, presentation, text);

        if (options.IncludeLogs)
            _AppendLogs(builder, presentation, text);

        _AppendTechnicalAppendix(builder, presentation, text);

        return new CrashMarkdownDocument
        {
            FileName = $"crash-analysis-{result.CreatedAt:yyyyMMdd-HHmmss}.md",
            Title = title,
            Content = builder.ToString()
        };
    }

    private static void _AppendOverview(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Overview", null));

        builder.AppendLine(text.Text(presentation.Summary.TitleKey, presentation.Summary.Parameters));
        builder.AppendLine();

        builder.AppendLine(text.Text(presentation.Summary.DescriptionKey, presentation.Summary.Parameters));

        if (!string.IsNullOrWhiteSpace(presentation.Summary.DetailKey))
        {
            builder.AppendLine();
            builder.AppendLine(text.Text(presentation.Summary.DetailKey!, presentation.Summary.Parameters));
        }

        builder.AppendLine();

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.Metric", "Metric"),
            ("Crash.Markdown.Table.Value", "Value"));

        var top = presentation.Diagnoses.FirstOrDefault();

        if (top is not null)
        {
            _AppendTableRow(
                builder,
                text.Text("Crash.Markdown.Metric.TopDiagnosis", null),
                text.Text(top.TitleKey, top.Parameters));

            _AppendTableRow(
                builder,
                text.Text("Crash.Markdown.Field.Confidence.Name", null),
                text.Text($"Crash.Confidence.{top.Confidence}", null));

            _AppendTableRow(
                builder,
                text.Text("Crash.Markdown.Field.Score.Name", null),
                top.Score.ToString());
        }

        foreach (var metric in presentation.Metrics)
            _AppendTableRow(
                builder,
                text.Text(metric.TitleKey, null),
                metric.Value);

        builder.AppendLine();
    }

    private static void _AppendPrimaryDiagnosis(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        var top = presentation.Diagnoses.FirstOrDefault();

        if (top is null)
        {
            _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Diagnoses", null));
            builder.AppendLine(text.Text("Crash.Markdown.Empty.Diagnoses", null));
            builder.AppendLine();
            return;
        }

        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.PrimaryDiagnosis", null));
        _AppendDiagnosis(builder, top, text);
    }

    private static void _AppendOtherDiagnoses(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        var diagnoses = presentation.Diagnoses.Skip(1).ToList();

        if (diagnoses.Count == 0)
            return;

        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.OtherDiagnoses", null));

        foreach (var diagnosis in diagnoses)
            _AppendDiagnosis(builder, diagnosis, text);
    }

    private static void _AppendDiagnosis(
        StringBuilder builder,
        CrashPresentationDiagnosis diagnosis,
        CrashTextLocalizer text)
    {
        _AppendHeading(
            builder,
            3,
            text.Text(diagnosis.TitleKey, diagnosis.Parameters));

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.Metric", "Metric"),
            ("Crash.Markdown.Table.Value", "Value"));

        _AppendTableRow(
            builder,
            text.Text("Crash.Markdown.Field.Confidence.Name", null),
            text.Text($"Crash.Confidence.{diagnosis.Confidence}", null));

        _AppendTableRow(
            builder,
            text.Text("Crash.Markdown.Field.Score.Name", null),
            diagnosis.Score.ToString());

        _AppendTableRow(
            builder,
            text.Text("Crash.Markdown.Field.Category", null),
            text.Text($"Crash.Category.{diagnosis.Category}", null));

        builder.AppendLine();

        _AppendDescriptionPart(
            builder,
            text.Text("Crash.Diagnosis.Part.Cause", null),
            text.Text(diagnosis.CauseKey, diagnosis.Parameters));

        _AppendDescriptionPart(
            builder,
            text.Text("Crash.Diagnosis.Part.Impact", null),
            text.Text(diagnosis.ImpactKey, diagnosis.Parameters));

        _AppendDescriptionPart(
            builder,
            text.Text("Crash.Diagnosis.Part.Recommendation", null),
            text.Text(diagnosis.RecommendationKey, diagnosis.Parameters));

        _AppendDiagnosisParameters(builder, diagnosis, text);
        _AppendDiagnosisNotes(builder, diagnosis, text);
    }

    private static void _AppendDiagnosisParameters(
        StringBuilder builder,
        CrashPresentationDiagnosis diagnosis,
        CrashTextLocalizer text)
    {
        if (diagnosis.Parameters.Count == 0)
            return;

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.Parameter", "Parameter"),
            ("Crash.Markdown.Table.Value", "Value"));

        foreach (var pair in diagnosis.Parameters)
            _AppendTableRow(builder, pair.Key, pair.Value);

        builder.AppendLine();
    }

    private static void _AppendDiagnosisNotes(
        StringBuilder builder,
        CrashPresentationDiagnosis diagnosis,
        CrashTextLocalizer text)
    {
        foreach (var note in diagnosis.Notes)
        {
            var noteText = text.Text(note.Key, note.Parameters);

            if (!string.IsNullOrWhiteSpace(noteText))
                builder.AppendLine("> " + noteText);
        }

        if (diagnosis.Notes.Count > 0)
            builder.AppendLine();
    }

    private static void _AppendDescriptionPart(StringBuilder builder, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        builder.AppendLine(title + "：");
        builder.AppendLine(CrashText.EscapeMarkdownParagraph(content));
        builder.AppendLine();
    }

    private static void _AppendSuggestions(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Suggestions", null));

        var index = 1;

        foreach (var group in presentation.Actions
                     .GroupBy(static action => action.Group)
                     .OrderBy(static group => group.Key))
        {
            _AppendHeading(builder, 3, text.Text($"Crash.Suggestions.Group.{group.Key}", null));

            foreach (var action in group.OrderBy(static action => action.Order))
            {
                builder.AppendLine(
                    $"{index}. {text.Text(action.TitleKey, action.Parameters)}");

                if (!string.IsNullOrWhiteSpace(action.DescriptionKey))
                    builder.AppendLine("   " + text.Text(action.DescriptionKey, action.Parameters));

                index++;
            }

            builder.AppendLine();
        }
    }

    private static void _AppendEvidence(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Evidence", null));

        var evidence = presentation.Evidence
            .OrderByDescending(static item => item.Weight)
            .Take(MaxKeyEvidence)
            .ToList();

        if (evidence.Count == 0)
        {
            builder.AppendLine(text.Text("Crash.Markdown.Empty.Evidence", null));
            builder.AppendLine();
            return;
        }

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.Source", "Source"),
            ("Crash.Markdown.Table.Line", "Line"),
            ("Crash.Markdown.Table.Weight", "Weight"),
            ("Crash.Markdown.Table.Summary", "Summary"));

        foreach (var item in evidence)
            _AppendTableRow(
                builder,
                item.SourceName ?? item.SourceKind.ToString(),
                item.LineNumber?.ToString() ?? "-",
                "+" + item.Weight,
                string.IsNullOrWhiteSpace(item.Summary) ? item.Excerpt : item.Summary);

        builder.AppendLine();

        _AppendDetailedEvidence(builder, evidence, text);
    }

    private static void _AppendDetailedEvidence(
        StringBuilder builder,
        IReadOnlyList<CrashPresentationEvidence> evidence,
        CrashTextLocalizer text)
    {
        _AppendHeading(
            builder,
            2,
            text.Text("Crash.Markdown.Section.DetailedEvidence", null));

        var index = 1;

        foreach (var item in evidence.Take(MaxDetailedEvidence))
        {
            IReadOnlyDictionary<string, string>? parameters = _Args(("Index", index.ToString()));
            _AppendHeading(
                builder,
                3,
                text.Text("Crash.Markdown.Evidence.Item", parameters));

            IReadOnlyDictionary<string, string>? parameters1 = _Args(
                ("Source", item.SourceName ?? item.SourceKind.ToString()),
                ("Line", item.LineNumber?.ToString() ?? "-"));
            builder.AppendLine(text.Text("Crash.Markdown.Evidence.SourceLine", parameters1));

            IReadOnlyDictionary<string, string>? parameters2 = _Args(("Weight", item.Weight.ToString()));
            builder.AppendLine(text.Text("Crash.Markdown.Evidence.Weight", parameters2));

            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine(CrashText.TrimPreview(
                string.IsNullOrWhiteSpace(item.Detail) ? item.Excerpt : item.Detail,
                8,
                1600));
            builder.AppendLine("```");
            builder.AppendLine();

            index++;
        }
    }

    private static void _AppendEnvironment(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Environment", null));

        foreach (var group in presentation.Environment.GroupBy(static item => item.GroupKey))
        {
            _AppendHeading(builder, 3, text.Text(group.Key, null));

            _AppendTableHeader(
                builder,
                text,
                ("Crash.Markdown.Table.Item", "Item"),
                ("Crash.Markdown.Table.Value", "Value"));

            foreach (var item in group)
            {
                var value = item.IsSensitive
                    ? text.Text("Crash.Environment.Sensitive", null)
                    : item.Value;

                _AppendTableRow(
                    builder,
                    text.Text(item.NameKey, null),
                    value);
            }

            builder.AppendLine();
        }
    }

    private static void _AppendLogs(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        _AppendHeading(builder, 2, text.Text("Crash.Markdown.Section.Logs", null));

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.File", "File"),
            ("Crash.Markdown.Table.Type", "Type"),
            ("Crash.Markdown.Table.Status", "Status"));

        foreach (var log in presentation.Logs)
            _AppendTableRow(
                builder,
                log.Name,
                $"`{log.Kind}`",
                log.UsedForAnalysis
                    ? text.Text("Crash.Logs.Used", null)
                    : text.Text("Crash.Logs.NotUsed", null));

        builder.AppendLine();
    }

    private static void _AppendTechnicalAppendix(
        StringBuilder builder,
        CrashPresentationModel presentation,
        CrashTextLocalizer text)
    {
        if (presentation.Facts.Count == 0)
            return;

        _AppendHeading(
            builder,
            2,
            text.Text("Crash.Markdown.Section.TechnicalAppendix", null));

        IReadOnlyDictionary<string, string>? parameters = _Args(("Count", Math.Min(MaxFactAppendix, presentation.Facts.Count).ToString()));
        builder.AppendLine(text.Text("Crash.Markdown.Appendix.FactsShown", parameters));
        builder.AppendLine();

        _AppendTableHeader(
            builder,
            text,
            ("Crash.Markdown.Table.Fact", "Fact"),
            ("Crash.Markdown.Table.Value", "Value"),
            ("Crash.Markdown.Table.Source", "Source"));

        foreach (var fact in presentation.Facts.Take(MaxFactAppendix))
            _AppendTableRow(
                builder,
                text.Text(fact.TitleKey, null),
                fact.Value,
                fact.SourceName ?? fact.SourceKind.ToString());

        builder.AppendLine();
    }

    private static void _AppendHeading(StringBuilder builder, int level, string title)
    {
        builder.AppendLine(new string('#', level) + " " + title);
        builder.AppendLine();
    }

    private static void _AppendTableHeader(
        StringBuilder builder,
        CrashTextLocalizer text,
        params (string Key, string Fallback)[] columns)
    {
        builder.Append("| ");

        foreach (var column in columns)
            builder.Append(CrashText.EscapeMarkdownCell(text.Text(column.Key, null))).Append(" | ");

        builder.AppendLine();

        builder.Append("| ");

        foreach (var _ in columns)
            builder.Append("--- | ");

        builder.AppendLine();
    }

    private static void _AppendTableRow(StringBuilder builder, params string[] cells)
    {
        builder.Append("| ");

        foreach (var cell in cells)
            builder.Append(CrashText.EscapeMarkdownCell(cell)).Append(" | ");

        builder.AppendLine();
    }

    private static Dictionary<string, string> _Args(params (string Key, string Value)[] args)
    {
        var result = new Dictionary<string, string>();

        foreach (var arg in args)
            result[arg.Key] = arg.Value;

        return result;
    }
}
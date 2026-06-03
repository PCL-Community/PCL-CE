namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashPresentationBuilder
{
    public static CrashPresentationModel Build(
        CrashLogBundle bundle,
        CrashFactSet facts,
        IReadOnlyList<CrashDiagnosis> diagnoses,
        CrashAnalysisRequest request,
        TimeSpan? analysisDuration = null)
    {
        var presentationDiagnoses = diagnoses.Select(_BuildDiagnosis).ToList();
        var actions = _CollectActions(bundle, presentationDiagnoses, request);
        var presentationFacts = _BuildFacts(facts);
        return new CrashPresentationModel
        {
            Summary = _BuildSummary(bundle, diagnoses),
            Diagnoses = presentationDiagnoses,
            Actions = actions,
            Evidence = presentationDiagnoses.SelectMany(static diagnosis => diagnosis.Evidence).ToList(),
            Facts = presentationFacts,
            Metrics = _BuildMetrics(bundle, presentationDiagnoses, presentationFacts, analysisDuration),
            Logs = _BuildLogs(bundle),
            Environment = _BuildEnvironment(request.RuntimeContext)
        };
    }

    private static CrashPresentationSummary _BuildSummary(
        CrashLogBundle bundle,
        IReadOnlyList<CrashDiagnosis> diagnoses)
    {
        if (!bundle.HasUsefulLog)
            return new CrashPresentationSummary
            {
                Severity = CrashPresentationSeverity.Warning,
                TitleKey = "Crash.Overview.Title.NoLog",
                DescriptionKey = "Crash.Overview.Subtitle.NoLog"
            };

        var top = diagnoses.FirstOrDefault();
        if (top is null || top.Code == CrashDiagnosisCode.Unknown)
            return new CrashPresentationSummary
            {
                Severity = CrashPresentationSeverity.Warning,
                TitleKey = "Crash.Overview.Title.Unknown",
                DescriptionKey = "Crash.Overview.Subtitle.Unknown"
            };

        return new CrashPresentationSummary
        {
            Severity = top.Severity == CrashDiagnosisSeverity.Error
                ? CrashPresentationSeverity.Error
                : CrashPresentationSeverity.Warning,
            TitleKey = "Crash.Overview.Title.WithDiagnosis",
            DescriptionKey = "Crash.Overview.Subtitle.WithDiagnosis",
            DetailKey = "Crash.Overview.Detail.WithDiagnosis",
            Parameters = new Dictionary<string, string>
            {
                ["Diagnosis"] = CrashDiagnosisLocalizer.TitleKey(top.Code),
                ["DiagnosisCode"] = top.Code.ToString(),
                ["DiagnosisCount"] = diagnoses.Count.ToString(),
                ["EvidenceCount"] = top.Evidence.Count.ToString()
            }
        };
    }

    private static IReadOnlyList<CrashPresentationMetric> _BuildMetrics(
        CrashLogBundle bundle,
        IReadOnlyList<CrashPresentationDiagnosis> diagnoses,
        IReadOnlyList<CrashPresentationFact> facts,
        TimeSpan? analysisDuration)
    {
        return
        [
            new CrashPresentationMetric
            {
                TitleKey = "Crash.Overview.Metric.DiagnosisCount",
                Value = diagnoses.Count.ToString()
            },
            new CrashPresentationMetric
            {
                TitleKey = "Crash.Overview.Metric.FactCount",
                Value = facts.Count.ToString()
            },
            new CrashPresentationMetric
            {
                TitleKey = "Crash.Overview.Metric.EvidenceCount",
                Value = diagnoses.SelectMany(static diagnosis => diagnosis.Evidence).Count().ToString()
            },
            new CrashPresentationMetric
            {
                TitleKey = "Crash.Overview.Metric.LogCount",
                Value = bundle.Documents.Count.ToString()
            },
            new CrashPresentationMetric
            {
                TitleKey = "Crash.Overview.Metric.AnalysisDuration",
                Value = analysisDuration is null
                    ? "-"
                    : Math.Max(1, (int)analysisDuration.Value.TotalMilliseconds) + " ms"
            }
        ];
    }

    private static CrashPresentationDiagnosis _BuildDiagnosis(CrashDiagnosis diagnosis)
    {
        var evidence = diagnosis.Evidence.Select(static item => new CrashPresentationEvidence
        {
            TitleKey = CrashDiagnosisLocalizer.EvidenceKey(item.FactKind),
            FactKind = item.FactKind,
            SourceKind = item.SourceKind,
            SourceName = item.SourceName,
            LineNumber = item.LineNumber,
            Excerpt = item.Excerpt,
            Summary = item.Summary,
            Detail = item.Detail,
            Weight = item.Weight
        }).ToList();

        return new CrashPresentationDiagnosis
        {
            Code = diagnosis.Code,
            Category = diagnosis.Category,
            TitleKey = CrashDiagnosisLocalizer.TitleKey(diagnosis.Code),
            DescriptionKey = CrashDiagnosisLocalizer.DescriptionKey(diagnosis.Code),
            CauseKey = CrashDiagnosisLocalizer.CauseKey(diagnosis.Code),
            ImpactKey = CrashDiagnosisLocalizer.ImpactKey(diagnosis.Code),
            RecommendationKey = CrashDiagnosisLocalizer.RecommendationKey(diagnosis.Code),
            Confidence = diagnosis.Confidence,
            Severity = diagnosis.Severity,
            Score = diagnosis.Score,
            Parameters = diagnosis.Parameters,
            Evidence = evidence,
            Notes = diagnosis.Notes,
            Actions = diagnosis.SuggestedActionKinds.Select((kind, index) => new CrashPresentationAction
            {
                Kind = kind,
                TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(kind),
                DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(kind),
                Priority = index == 0 ? CrashActionPriority.Primary : CrashActionPriority.Secondary,
                Group = _GroupForAction(kind),
                Order = index
            }).ToList()
        };
    }

    private static IReadOnlyList<CrashPresentationFact> _BuildFacts(CrashFactSet facts)
    {
        return facts.Facts
            .Where(static fact => fact.Visibility != CrashFactVisibility.Hidden)
            .OrderBy(static fact => fact.Visibility)
            .ThenBy(static fact => fact.Kind)
            .Take(30)
            .Select(static fact =>
            {
                var evidence = fact.Evidence.FirstOrDefault();
                return new CrashPresentationFact
                {
                    Kind = fact.Kind,
                    TitleKey = CrashDiagnosisLocalizer.EvidenceKey(fact.Kind),
                    Value = fact.Value,
                    SourceKind = evidence?.SourceKind ?? CrashLogKind.Unknown,
                    SourceName = evidence?.SourceName,
                    LineNumber = evidence?.LineNumber,
                    Excerpt = evidence?.Excerpt
                };
            }).ToList();
    }

    private static IReadOnlyList<CrashPresentationAction> _CollectActions(
        CrashLogBundle bundle,
        IReadOnlyList<CrashPresentationDiagnosis> diagnoses,
        CrashAnalysisRequest request)
    {
        var actions = new List<CrashPresentationAction>();
        foreach (var action in diagnoses.SelectMany(static diagnosis => diagnosis.Actions))
            if (actions.All(existing => existing.Kind != action.Kind))
                actions.Add(action);

        if (bundle.PreferredOpenDocument?.FullPath is { Length: > 0 } logPath)
            actions.Add(new CrashPresentationAction
            {
                Kind = CrashPresentationActionKind.OpenLog,
                TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(CrashPresentationActionKind.OpenLog),
                DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(CrashPresentationActionKind.OpenLog),
                Priority = CrashActionPriority.Secondary,
                Group = CrashActionGroup.Investigate,
                TargetPath = logPath
            });

        actions.Add(new CrashPresentationAction
        {
            Kind = CrashPresentationActionKind.ExportMarkdown,
            TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(CrashPresentationActionKind.ExportMarkdown),
            DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(CrashPresentationActionKind.ExportMarkdown),
            Priority = CrashActionPriority.Primary,
            Group = CrashActionGroup.AskForHelp,
            Order = 90
        });
        actions.Add(new CrashPresentationAction
        {
            Kind = CrashPresentationActionKind.ExportReport,
            TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(CrashPresentationActionKind.ExportReport),
            DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(CrashPresentationActionKind.ExportReport),
            Priority = CrashActionPriority.Secondary,
            Group = CrashActionGroup.AskForHelp,
            Order = 91
        });
        actions.Add(new CrashPresentationAction
        {
            Kind = CrashPresentationActionKind.CopyDiagnosisSummary,
            TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(CrashPresentationActionKind.CopyDiagnosisSummary),
            DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(CrashPresentationActionKind.CopyDiagnosisSummary),
            Priority = CrashActionPriority.More,
            Group = CrashActionGroup.AskForHelp,
            Order = 92
        });
        actions.Add(new CrashPresentationAction
        {
            Kind = CrashPresentationActionKind.PreviewMarkdown,
            TitleKey = CrashDiagnosisLocalizer.ActionTitleKey(CrashPresentationActionKind.PreviewMarkdown),
            DescriptionKey = CrashDiagnosisLocalizer.ActionDescriptionKey(CrashPresentationActionKind.PreviewMarkdown),
            Priority = CrashActionPriority.More,
            Group = CrashActionGroup.AskForHelp,
            Order = 93
        });

        return actions
            .GroupBy(static action => action.Kind)
            .Select(static group => group.First())
            .OrderBy(static action => action.Group)
            .ThenBy(static action => action.Priority)
            .ThenBy(static action => action.Order)
            .ToList();
    }

    private static CrashActionGroup _GroupForAction(CrashPresentationActionKind kind)
    {
        return kind switch
        {
            CrashPresentationActionKind.OpenJavaSettings or
                CrashPresentationActionKind.OpenMemorySettings or
                CrashPresentationActionKind.OpenInstanceModsFolder or
                CrashPresentationActionKind.OpenInstanceSettings or
                CrashPresentationActionKind.OpenResourcePackFolder => CrashActionGroup.FixNow,
            CrashPresentationActionKind.ExportMarkdown or
                CrashPresentationActionKind.ExportReport or
                CrashPresentationActionKind.CopyDiagnosisSummary or
                CrashPresentationActionKind.PreviewMarkdown => CrashActionGroup.AskForHelp,
            _ => CrashActionGroup.Investigate
        };
    }

    private static IReadOnlyList<CrashPresentationLogSource> _BuildLogs(CrashLogBundle bundle)
    {
        return bundle.Documents.Select(document => new CrashPresentationLogSource
        {
            Kind = document.Kind,
            Name = document.Name,
            FullPath = document.FullPath,
            Length = document.OriginalLength,
            UsedForAnalysis = bundle.Windows.Any(window => window.SourceName == document.Name),
            Preview = CrashText.TrimPreview(
                bundle.Windows.FirstOrDefault(window => window.SourceName == document.Name)?.ErrorWindow ??
                document.Text, 100, 20 * 1024)
        }).ToList();
    }

    private static IReadOnlyList<CrashPresentationEnvironmentItem> _BuildEnvironment(CrashRuntimeContext context)
    {
        var items = new List<CrashPresentationEnvironmentItem>();
        Add("Crash.Environment.Group.Instance",
            "Crash.Environment.Item.InstanceName",
            context.InstanceName, false);
        Add("Crash.Environment.Group.Instance",
            "Crash.Environment.Item.InstancePath",
            context.InstancePath, true);
        Add("Crash.Environment.Group.Instance",
            "Crash.Environment.Item.MinecraftVersion",
            context.MinecraftVersion, false);
        Add("Crash.Environment.Group.Instance",
            "Crash.Environment.Item.Loader",
            context.LoaderName, false);
        Add("Crash.Environment.Group.Java",
            "Crash.Environment.Item.Java",
            context.JavaInfo, false);
        Add("Crash.Environment.Group.Java",
            "Crash.Environment.Item.AllocatedMemory",
            context.AllocatedMemory, false);
        Add("Crash.Environment.Group.System",
            "Crash.Environment.Item.OS",
            context.OperatingSystem, false);
        Add("Crash.Environment.Group.System",
            "Crash.Environment.Item.CPU",
            context.CpuName, false);
        Add("Crash.Environment.Group.System",
            "Crash.Environment.Item.Memory",
            context.SystemMemoryMb is null
                ? null
                : context.SystemMemoryMb + " MB", false);

        foreach (var gpu in context.Gpus)
            Add("Crash.Environment.Group.System",
                "Crash.Environment.Item.GPU",
                gpu.Name, false);
        return items;

        void Add(string groupKey, string nameKey, string? value, bool sensitive)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            items.Add(new CrashPresentationEnvironmentItem
            {
                GroupKey = groupKey,
                NameKey = nameKey,
                Value = value,
                IsSensitive = sensitive
            });
        }
    }
}
namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashFactExtractor
{
    private readonly IReadOnlyList<ICrashLogParser> _parsers =
    [
        new JavaCrashParser(),
        new CrashReportSectionParser(),
        new MinecraftCrashReportParser(),
        new FabricResolutionErrorParser(),
        new ForgeErrorSectionParser(),
        new LoaderLogParser(),
        new ModMetadataParser(),
        new HsErrProblematicFrameParser(),
        new NativeCrashParser(),
        new FileSystemCrashParser(),
        new SystemInfoParser()
    ];

    public CrashFactSet Extract(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var analysisDocuments = bundle.Documents
            .Where(static document => document.AnalysisRole != CrashLogAnalysisRole.ReportOnly)
            .ToList();
        var analysisDocumentNames = analysisDocuments
            .Select(static document => document.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var analysisBundle = bundle with
        {
            Documents = analysisDocuments,
            Windows = bundle.Windows
                .Where(window => analysisDocumentNames.Contains(window.SourceName))
                .ToList()
        };

        var facts = new List<CrashFact>();
        foreach (var parser in _parsers)
            facts.AddRange(parser.Parse(analysisBundle, request));

        return _Normalize(facts);
    }

    private static CrashFactSet _Normalize(IEnumerable<CrashFact> facts)
    {
        var result = (from @group in facts.GroupBy(_GetStableKey)
            let best = @group.OrderBy(static fact => fact.Visibility)
                .ThenBy(static fact => _SourcePriority(fact))
                .ThenByDescending(static fact => fact.Confidence)
                .ThenBy(static fact => fact.Value.Length)
                .First()
            select best with
            {
                Evidence = @group.SelectMany(static fact => fact.Evidence)
                    .GroupBy(static evidence => new
                        {
                            evidence.SourceKind, evidence.SourceName,
                            evidence.LineNumber, evidence.Excerpt
                        }
                    )
                    .Select(static item => item.First())
                    .Take(5)
                    .ToList()
            }).ToList();

        return new CrashFactSet { Facts = result };
    }

    private static int _SourcePriority(CrashFact fact)
    {
        return fact.Evidence.FirstOrDefault()?.SourceKind switch
        {
            CrashLogKind.CapturedGameOutput => 0,
            CrashLogKind.MinecraftCrashReport => 1,
            CrashLogKind.MinecraftLatestLog => 2,
            CrashLogKind.MinecraftDebugLog => 3,
            CrashLogKind.JavaFatalErrorLog => 4,
            _ => 10
        };
    }

    private static string _GetStableKey(CrashFact fact)
    {
        var kind = fact.Kind.ToString();
        if (fact.Properties.TryGetValue("MissingModId", out var missing) && !string.IsNullOrWhiteSpace(missing))
        {
            fact.Properties.TryGetValue("AffectedModId", out var affectedMod);
            fact.Properties.TryGetValue("RequiredVersion", out var requiredVersion);
            return kind
                   + "|affected:" + (affectedMod ?? string.Empty).Trim().ToLowerInvariant()
                   + "|missing:" + missing.Trim().ToLowerInvariant()
                   + "|version:" + (requiredVersion ?? string.Empty).Trim().ToLowerInvariant();
        }

        if (fact.Properties.TryGetValue("AffectedModId", out var affected) &&
            fact.Kind is CrashFactKind.LoaderResolutionError or CrashFactKind.ModVersionConflictDetected)
            return kind + "|affected:" + affected.Trim().ToLowerInvariant();

        if (fact.Kind is CrashFactKind.LoaderDetected
            or CrashFactKind.LoaderVersionDetected
            or CrashFactKind.MinecraftVersionDetected
            or CrashFactKind.JavaVersionDetected
            or CrashFactKind.JavaArchitectureDetected)
            return kind + "|" + CrashText.NormalizeEvidence(fact.Value);

        return kind + "|" + CrashText.NormalizeEvidence(fact.Value) + "|" + SourceKey(fact);

        static string SourceKey(CrashFact fact)
        {
            var source = fact.Evidence.FirstOrDefault();
            return source?.SourceKind.ToString() ?? "context";
        }
    }
}

internal interface ICrashLogParser
{
    IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request);
}

internal static class CrashFactFactory
{
    public static CrashFact Create(
        CrashFactKind kind,
        string value,
        CrashLogDocument document,
        string? excerpt = null,
        int? lineNumber = null,
        IReadOnlyDictionary<string, string>? properties = null,
        CrashFactConfidence confidence = CrashFactConfidence.High,
        CrashFactVisibility visibility = CrashFactVisibility.Main,
        CrashFactStrength? strength = null,
        CrashFactScope? scope = null)
    {
        return new CrashFact
        {
            Id = kind + ":" + value,
            Kind = kind,
            Value = value,
            Confidence = confidence,
            Strength = strength ?? _DefaultStrength(kind),
            Scope = scope ?? _DefaultScope(kind),
            Visibility = visibility,
            Properties = properties ?? new Dictionary<string, string>(),
            Evidence =
            [
                new CrashFactEvidence
                {
                    SourceKind = document.Kind,
                    SourceName = document.Name,
                    Excerpt = excerpt,
                    LineNumber = lineNumber
                }
            ]
        };
    }

    public static CrashFact CreateFromContext(CrashFactKind kind, string value,
        IReadOnlyDictionary<string, string>? properties = null,
        CrashFactVisibility visibility = CrashFactVisibility.Technical,
        CrashFactStrength? strength = null,
        CrashFactScope? scope = null)
    {
        return new CrashFact
        {
            Id = kind + ":" + value,
            Kind = kind,
            Value = value,
            Strength = strength ?? _DefaultStrength(kind),
            Scope = scope ?? _DefaultScope(kind),
            Visibility = visibility,
            Properties = properties ?? new Dictionary<string, string>()
        };
    }

    private static CrashFactStrength _DefaultStrength(CrashFactKind kind)
    {
        return kind switch
        {
            CrashFactKind.JavaUnsupportedClassVersionDetected
                or CrashFactKind.JavaClassFileMajorVersionDetected
                or CrashFactKind.JavaRequiredVersionDetected
                or CrashFactKind.JavaOutOfMemoryDetected
                or CrashFactKind.JavaHeapSpaceOutOfMemoryDetected
                or CrashFactKind.JavaMetaspaceOutOfMemoryDetected
                or CrashFactKind.JavaDirectBufferOutOfMemoryDetected
                or CrashFactKind.JavaNativeThreadOutOfMemoryDetected
                or CrashFactKind.JavaGcOverheadDetected
                or CrashFactKind.ManualDebugCrashDetected
                or CrashFactKind.NativeProblematicFrameDetected
                or CrashFactKind.MissingModDependencyDetected
                or CrashFactKind.ForgeMissingMandatoryDependencyDetected
                or CrashFactKind.OpenGlInitializationFailed
                or CrashFactKind.GpuDriverIssueHint
                or CrashFactKind.GpuNativeLibraryCrashDetected
                or CrashFactKind.NativeLibraryMissingDetected
                or CrashFactKind.GameJarMissingDetected
                or CrashFactKind.DiskFullDetected => CrashFactStrength.Direct,

            CrashFactKind.LoaderMixinError
                or CrashFactKind.LoaderTransformError
                or CrashFactKind.ShaderIssueDetected
                or CrashFactKind.ResourcePackIssueDetected
                or CrashFactKind.MinecraftMainException => CrashFactStrength.Medium,

            CrashFactKind.OpenGlVersionTooLowDetected
                or CrashFactKind.GlfwErrorDetected => CrashFactStrength.Strong,

            _ => CrashFactStrength.Strong
        };
    }

    private static CrashFactScope _DefaultScope(CrashFactKind kind)
    {
        return kind switch
        {
            CrashFactKind.JavaVersionDetected
                or CrashFactKind.JavaVendorDetected
                or CrashFactKind.JavaArchitectureDetected
                or CrashFactKind.MinecraftVersionDetected
                or CrashFactKind.LoaderDetected
                or CrashFactKind.LoaderVersionDetected
                or CrashFactKind.MemoryAllocationDetected
                or CrashFactKind.OsVersionDetected
                or CrashFactKind.ProcessBitnessDetected
                or CrashFactKind.LaunchArgumentDetected => CrashFactScope.Context,

            CrashFactKind.LoaderMixinError
                or CrashFactKind.LoaderTransformError
                or CrashFactKind.NativeAccessViolationDetected
                or CrashFactKind.NativeProblematicFrameDetected
                or CrashFactKind.MinecraftMainException
                or CrashFactKind.MinecraftExitCodeDetected
                or CrashFactKind.ShaderIssueDetected => CrashFactScope.Symptom,

            _ => CrashFactScope.RootCause
        };
    }
}
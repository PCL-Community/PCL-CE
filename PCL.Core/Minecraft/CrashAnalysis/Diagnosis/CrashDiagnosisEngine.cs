namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashDiagnosisEngine
{
    private readonly IReadOnlyList<CrashDiagnosisRule> _rules = CrashDiagnosisRuleCatalog.Create();

    public IReadOnlyList<CrashDiagnosis> Diagnose(CrashLogBundle bundle, CrashFactSet facts,
        CrashAnalysisRequest request)
    {
        var diagnoses = _rules
            .Select(rule => rule.Evaluate(bundle, facts, request))
            .Where(static diagnosis => diagnosis is not null)
            .Cast<CrashDiagnosis>()
            .ToList();

        diagnoses = _ApplyConflictPolicy(diagnoses);
        diagnoses = diagnoses
            .Where(static diagnosis => diagnosis.Score > 0)
            .ToList();

        if (diagnoses.Count == 0 && bundle.HasUsefulLog)
            diagnoses.Add(_CreateInconclusiveDiagnosis());

        return diagnoses
            .OrderBy(static diagnosis => _NatureOrder(diagnosis.Nature))
            .ThenByDescending(static diagnosis => diagnosis.Score)
            .ThenBy(static diagnosis => diagnosis.Code)
            .Take(5)
            .ToList();
    }

    private static List<CrashDiagnosis> _ApplyConflictPolicy(List<CrashDiagnosis> diagnoses)
    {
        if (diagnoses.Any(static d => d.Code == CrashDiagnosisCode.ManualDebugCrash))
            diagnoses = diagnoses
                .Where(static d => d.Code is CrashDiagnosisCode.ManualDebugCrash
                    or CrashDiagnosisCode.NativeJvmCrash
                    or CrashDiagnosisCode.GraphicsDriverNativeCrash)
                .Select(static d => d.Code == CrashDiagnosisCode.NativeJvmCrash
                    ? _AdjustScore(d, -15, new CrashDiagnosisNote
                        {
                            Key = "Crash.Note.ManualDebugCrashWithNative",
                            Level = CrashDiagnosisNoteLevel.Info
                        }) with
                        {
                            Nature = CrashDiagnosisNature.Symptom
                        }
                    : d)
                .ToList();


        if (diagnoses.Any(static d => d.Code == CrashDiagnosisCode.LoaderDependencyMissing))
            diagnoses = diagnoses.Select(static d =>
                d.Code == CrashDiagnosisCode.LoaderMixinFailure
                    ? _AdjustScore(d, -35, new CrashDiagnosisNote
                    {
                        Key = "Crash.Note.DependencyOverridesMixin",
                        Level = CrashDiagnosisNoteLevel.Warning
                    })
                    : d).ToList();

        if (diagnoses.Any(static d =>
                d.Code is CrashDiagnosisCode.LoaderDependencyMissing
                    or CrashDiagnosisCode.LoaderDependencyVersionConflict
                    or CrashDiagnosisCode.ModSetConflict))
            diagnoses = diagnoses.Select(static d =>
                d.Code == CrashDiagnosisCode.ModLikelyCausedCrash
                    ? _AdjustScore(d, -30, new CrashDiagnosisNote { Key = "Crash.Note.DependencyOverridesMixin" })
                    : d).ToList();

        if (diagnoses.Any(static d => d.Code == CrashDiagnosisCode.ModSetConflict))
            diagnoses = diagnoses
                .Where(static d => d.Code != CrashDiagnosisCode.LoaderVersionIncompatible ||
                                   _HasExplicitLoaderOrGameVersionEvidence(d))
                .ToList();

        if (diagnoses.Any(static d =>
                d.Code is CrashDiagnosisCode.RuntimeJavaTooOld or CrashDiagnosisCode.RuntimeJavaTooNew))
            diagnoses = diagnoses.Select(static d =>
                d.Code is CrashDiagnosisCode.ModLikelyCausedCrash or CrashDiagnosisCode.LoaderMixinFailure
                    ? _AdjustScore(d, -25, new CrashDiagnosisNote { Key = "Crash.Note.JavaOverridesStack" })
                    : d).ToList();

        if (diagnoses.Any(static d => d.Code == CrashDiagnosisCode.GraphicsDriverNativeCrash))
            diagnoses = diagnoses.Select(static d =>
                d.Code == CrashDiagnosisCode.NativeJvmCrash
                    ? _AdjustScore(d, -80, new CrashDiagnosisNote { Key = "Crash.Note.GraphicsOverridesNativeJvm" })
                    : d).ToList();
        return diagnoses;
    }

    private static bool _HasExplicitLoaderOrGameVersionEvidence(CrashDiagnosis diagnosis)
    {
        return diagnosis.Evidence
            .Select(evidence => string.Join("\n", evidence.Summary, evidence.Detail, evidence.Excerpt))
            .Where(value => !_LooksLikeModToModConflictEvidence(value))
            .Any(value => value.Contains("minecraft", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("loader", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ||
                          value.Contains("quilt", StringComparison.OrdinalIgnoreCase));
    }

    private static bool _LooksLikeModToModConflictEvidence(string value)
    {
        return value.Contains("NEG_HARD_DEP", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("{breaks", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("version of mod", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("conflicting version is present", StringComparison.OrdinalIgnoreCase) ||
               (value.Contains("Replace mod", StringComparison.OrdinalIgnoreCase) &&
                value.Contains("compatible with", StringComparison.OrdinalIgnoreCase)) ||
               (value.Contains("any version", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("minecraft", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("loader", StringComparison.OrdinalIgnoreCase));
    }

    private static CrashDiagnosis _CreateInconclusiveDiagnosis()
    {
        const int score = 25;
        return new CrashDiagnosis
        {
            RuleId = "analysis.inconclusive",
            Code = CrashDiagnosisCode.AnalysisInconclusive,
            Category = CrashDiagnosisCategory.Unknown,
            Severity = CrashDiagnosisSeverity.Warning,
            Nature = CrashDiagnosisNature.Context,
            Score = score,
            Confidence = CrashScore.ToConfidence(score),
            SuggestedActionKinds =
                [CrashPresentationActionKind.ExportMarkdown, CrashPresentationActionKind.ExportReport]
        };
    }

    private static int _NatureOrder(CrashDiagnosisNature nature)
    {
        return nature switch
        {
            CrashDiagnosisNature.RootCause => 0,
            CrashDiagnosisNature.ProbableCause => 1,
            CrashDiagnosisNature.Symptom => 2,
            CrashDiagnosisNature.Context => 3,
            _ => 4
        };
    }

    private static CrashDiagnosis _AdjustScore(CrashDiagnosis diagnosis, int delta, CrashDiagnosisNote note)
    {
        var score = Math.Max(0, diagnosis.Score + delta);
        return diagnosis with
        {
            Score = score,
            Confidence = CrashScore.ToConfidence(score),
            Notes = diagnosis.Notes.Concat([note]).ToList()
        };
    }
}
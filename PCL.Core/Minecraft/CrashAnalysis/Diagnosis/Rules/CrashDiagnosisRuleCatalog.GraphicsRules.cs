namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    private sealed class LwjglNativeLoadRule : CrashDiagnosisRule
    {
        public override string Id => "graphics.lwjgl_native_load_failed";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsLwjglNativeLoadFailed;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Graphics;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.LwjglNativeLoadFailed)
                .Concat(facts.Find(CrashFactKind.LwjglInitializationFailed))
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(Math.Min(84, 64 + related.Count * 10),
                    related.Select(fact => Evidence(fact, 70)).ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class GraphicsOpenGlRule : CrashDiagnosisRule
    {
        public override string Id => "graphics.opengl_unavailable";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsOpenGlUnavailable;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Graphics;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            if (facts.Has(CrashFactKind.NativeLibraryMissingDetected) ||
                facts.Has(CrashFactKind.LibraryMissingDetected))
                return null;

            var related = facts
                .Find(CrashFactKind.OpenGlInitializationFailed)
                .ToList();
            if (related.Count == 0) return null;

            var evidence = related
                .Take(2)
                .Select(fact => Evidence(fact, 80))
                .ToList();
            return Create(Math.Min(95, related.Count * 80), evidence,
                actions: [CrashPresentationActionKind.ExportMarkdown],
                nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class GraphicsDriverNativeCrashRule : CrashDiagnosisRule
    {
        public override string Id => "graphics.driver_native_crash";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsDriverNativeCrash;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Graphics;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var driver = facts
                .Find(CrashFactKind.GpuDriverIssueHint)
                .ToList();
            if (driver.Count == 0) return null;

            var evidence = driver
                .Take(1)
                .Select(fact => Evidence(fact, 90))
                .ToList();
            var hasNativeCrash = facts.Has(CrashFactKind.NativeAccessViolationDetected) ||
                                 facts.Has(CrashFactKind.JavaFatalErrorDetected);
            var score = hasNativeCrash ? 95 : 70;

            evidence.AddRange(facts
                .Find(CrashFactKind.NativeAccessViolationDetected)
                .Take(1)
                .Select(fact => Evidence(fact, 25)));

            return Create(score, evidence,
                actions:
                [
                    CrashPresentationActionKind.OpenResourcePackFolder,
                    CrashPresentationActionKind.ExportMarkdown
                ],
                nature: CrashDiagnosisNature.RootCause);
        }
    }
}
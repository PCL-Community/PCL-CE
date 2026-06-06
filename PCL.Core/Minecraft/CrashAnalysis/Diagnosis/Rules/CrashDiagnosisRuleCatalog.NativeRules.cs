namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    private sealed class NativeJvmRule : CrashDiagnosisRule
    {
        public override string Id => "native.jvm_crash";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.NativeJvmCrash;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Native;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var fatal = facts
                .Find(CrashFactKind.NativeProblematicFrameDetected)
                .Concat(facts.Find(CrashFactKind.NativeAccessViolationDetected))
                .Concat(facts.Find(CrashFactKind.JavaFatalErrorDetected))
                .Take(4)
                .ToList();
            if (fatal.Count == 0)
                return null;

            return Create(
                60,
                fatal
                    .Select(f => Evidence(f, f.Kind == CrashFactKind.NativeProblematicFrameDetected ? 70 : 55))
                    .ToList(),
                actions:
                [
                    CrashPresentationActionKind.OpenJavaSettings,
                    CrashPresentationActionKind.ExportReport
                ],
                nature: CrashDiagnosisNature.ProbableCause);
        }
    }
}
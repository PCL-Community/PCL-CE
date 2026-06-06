namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    private sealed class ManualDebugCrashRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.manual_debug_crash";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ManualDebugCrash;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.ManualDebugCrashDetected)
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(
                    100,
                    related.Select(fact => Evidence(fact, 100)).ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class MemoryRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.memory.exhausted";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeMemoryExhausted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var heapFacts = facts
                .Find(CrashFactKind.JavaHeapSpaceOutOfMemoryDetected)
                .Concat(facts.Find(CrashFactKind.JavaDirectBufferOutOfMemoryDetected))
                .Concat(facts.Find(CrashFactKind.JavaGcOverheadDetected))
                .ToList();
            var metaspaceFacts = facts.Find(CrashFactKind.JavaMetaspaceOutOfMemoryDetected).ToList();
            var nativeThreadFacts = facts.Find(CrashFactKind.JavaNativeThreadOutOfMemoryDetected).ToList();
            var outOfMemoryFacts = facts
                .Find(CrashFactKind.JavaOutOfMemoryDetected)
                .Concat(heapFacts)
                .Concat(metaspaceFacts)
                .Concat(nativeThreadFacts)
                .GroupBy(static fact => fact.Kind + "|" + fact.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToList();
            if (outOfMemoryFacts.Count == 0)
                return null;

            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;

            foreach (var fact in outOfMemoryFacts.Take(3))
            {
                var weight = fact.Kind == CrashFactKind.JavaOutOfMemoryDetected ? 70 : 90;
                score += weight;
                evidence.Add(Evidence(fact, weight));
            }

            score = Math.Min(score, 92);

            foreach (var fact in facts
                         .Find(CrashFactKind.ProcessBitnessDetected)
                         .Take(1))
            {
                score += 15;
                evidence.Add(Evidence(fact, 15));
            }

            var code = CrashDiagnosisCode.RuntimeMemoryExhausted;
            if (heapFacts.Count > 0)
                code = CrashDiagnosisCode.RuntimeMemoryHeapExhausted;
            else if (metaspaceFacts.Count > 0)
                code = CrashDiagnosisCode.RuntimeMemoryMetaspaceExhausted;
            else if (nativeThreadFacts.Count > 0)
                code = CrashDiagnosisCode.RuntimeMemoryNativeThreadExhausted;

            return Create(score, evidence,
                    actions:
                    [
                        CrashPresentationActionKind.OpenMemorySettings,
                        CrashPresentationActionKind.ExportMarkdown
                    ],
                    nature: CrashDiagnosisNature.RootCause) with
                {
                    Code = code
                };
        }
    }

    private sealed class JavaCompatibilityRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.java.compatibility";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeJavaTooOld;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var unsupported = facts.First(CrashFactKind.JavaUnsupportedClassVersionDetected);
            var requiredJava = _GetRequiredJavaMajor(facts);
            var currentJava = _GetCurrentJavaMajor(facts);

            if (unsupported is not null)
            {
                var parameters = new Dictionary<string, string>();
                if (requiredJava > 0) parameters["RequiredJavaMajor"] = requiredJava.ToString();
                if (currentJava > 0) parameters["CurrentJavaMajor"] = currentJava.ToString();

                var evidence = new List<CrashDiagnosisEvidence> { Evidence(unsupported, 85) };
                evidence.AddRange(facts
                    .Find(CrashFactKind.JavaRequiredVersionDetected)
                    .Take(1)
                    .Select(fact => Evidence(fact, 20)));

                var score = requiredJava > 0 && currentJava > 0 && requiredJava > currentJava ? 95 : 85;
                return Create(score, evidence,
                    parameters,
                    [CrashPresentationActionKind.OpenJavaSettings],
                    nature: CrashDiagnosisNature.RootCause);
            }

            var module = facts.First(CrashFactKind.JavaModuleAccessErrorDetected);
            if (module is null) return null;

            var moduleScore = currentJava >= 16 ? 82 : 72;
            return new CrashDiagnosis
            {
                RuleId = "runtime.java.module_access",
                Code = CrashDiagnosisCode.RuntimeJavaTooNew,
                Category = CrashDiagnosisCategory.Runtime,
                Severity = CrashDiagnosisSeverity.Error,
                Nature = CrashDiagnosisNature.RootCause,
                Score = moduleScore,
                Confidence = CrashScore.ToConfidence(moduleScore),
                Evidence = [Evidence(module, moduleScore)],
                SuggestedActionKinds = [CrashPresentationActionKind.OpenJavaSettings],
                Parameters = currentJava > 0
                    ? new Dictionary<string, string> { ["CurrentJavaMajor"] = currentJava.ToString() }
                    : new Dictionary<string, string>()
            };
        }

        private static int _GetRequiredJavaMajor(CrashFactSet facts)
        {
            foreach (var fact in facts.Find(CrashFactKind.JavaRequiredVersionDetected))
                if (fact.Properties.TryGetValue("RequiredJavaMajor", out var value) &&
                    int.TryParse(value, out var major))
                    return major;
            return 0;
        }

        private static int _GetCurrentJavaMajor(CrashFactSet facts)
        {
            foreach (var fact in facts.Find(CrashFactKind.JavaVersionDetected))
            {
                if (fact.Properties.TryGetValue("JavaMajor", out var value) &&
                    int.TryParse(value, out var major))
                    return major;
                if (int.TryParse(fact.Value.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(),
                        out major))
                    return major == 1 && fact.Value.Split('.').Length > 1 &&
                           int.TryParse(fact.Value.Split('.')[1], out var legacy)
                        ? legacy
                        : major;
            }

            return 0;
        }
    }

    private sealed class JavaLaunchRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.java.launch_failed";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeJavaLaunchFailed;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.JavaExecutableMissingDetected)
                .Concat(facts.Find(CrashFactKind.JavaMainClassMissingDetected))
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(88,
                    related.Select(fact => Evidence(fact, 88)).ToList(),
                    actions: [CrashPresentationActionKind.OpenJavaSettings],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class JavaVendorRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.java.vendor_unsupported";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeJavaVendorUnsupported;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var vendor = facts
                .Find(CrashFactKind.JavaVendorDetected)
                .FirstOrDefault(static fact => fact.Value.Contains("OpenJ9", StringComparison.OrdinalIgnoreCase));
            return vendor is null
                ? null
                : Create(80,
                    [Evidence(vendor, 80)],
                    actions: [CrashPresentationActionKind.OpenJavaSettings],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class JavaArchitectureRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.java.architecture_mismatch";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeArchitectureMismatch;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            if (request.RuntimeContext.Is32BitSystem == true)
                return null;

            var architecture = facts
                .Find(CrashFactKind.JavaArchitectureDetected)
                .FirstOrDefault(static fact => fact.Value.Equals("x86", StringComparison.OrdinalIgnoreCase));
            return architecture is null
                ? null
                : Create(72,
                    [Evidence(architecture, 72)],
                    actions: [CrashPresentationActionKind.OpenJavaSettings],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class LibraryOrNativeRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.library_or_native_missing";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LibraryOrNativeMissing;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.NativeLibraryMissingDetected)
                .Concat(facts.Find(CrashFactKind.LibraryMissingDetected))
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(
                    80,
                    related
                        .Select(fact => Evidence(fact, 80))
                        .ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }
}
namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashDiagnosisRuleCatalog
{
    public static IReadOnlyList<CrashDiagnosisRule> Create()
    {
        return
        [
            new NoUsefulLogRule(),
            new MemoryRule(),
            new JavaCompatibilityRule(),
            new GraphicsOpenGlRule(),
            new GraphicsDriverNativeCrashRule(),
            new GameResourceOrShaderRule(),
            new LoaderDependencyRule(),
            new MixinTransformRule(),
            new DuplicateModRule(),
            new ModFileRule(),
            new WorldContentRule(),
            new NativeJvmRule()
        ];
    }

    private sealed class NoUsefulLogRule : CrashDiagnosisRule
    {
        public override string Id => "launcher.no_useful_log";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LauncherCapturedNoUsefulLog;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Launcher;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            return bundle.HasUsefulLog
                ? null
                : Create(70, [], actions: [CrashPresentationActionKind.ExportReport],
                    severity: CrashDiagnosisSeverity.Warning,
                    nature: CrashDiagnosisNature.Context);
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
            var outOfMemoryFacts = facts
                .Find(CrashFactKind.JavaOutOfMemoryDetected)
                .ToList();
            if (outOfMemoryFacts.Count == 0)
                return null;

            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;

            foreach (var fact in outOfMemoryFacts.Take(2))
            {
                score += 80;
                evidence.Add(Evidence(fact, 80));
            }

            score = Math.Min(score, 90);

            foreach (var fact in facts
                         .Find(CrashFactKind.ProcessBitnessDetected)
                         .Take(1))
            {
                score += 15;
                evidence.Add(Evidence(fact, 15));
            }

            return Create(score, evidence,
                actions:
                [
                    CrashPresentationActionKind.OpenMemorySettings,
                    CrashPresentationActionKind.ExportMarkdown
                ],
                nature: CrashDiagnosisNature.RootCause);
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
            var related = facts
                .Find(CrashFactKind.OpenGlInitializationFailed)
                .Concat(facts.Find(CrashFactKind.LwjglInitializationFailed))
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

    private sealed class GameResourceOrShaderRule : CrashDiagnosisRule
    {
        public override string Id => "graphics.resource_or_shader";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsResourceOrShaderOverload;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Graphics;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.ShaderIssueDetected)
                .Concat(facts.Find(CrashFactKind.ResourcePackIssueDetected))
                .ToList();
            if (related.Count == 0) return null;

            var evidence = related
                .Take(2)
                .Select(fact => Evidence(fact, 45))
                .ToList();
            return Create(Math.Min(70, related.Count * 45), evidence,
                actions:
                [
                    CrashPresentationActionKind.OpenResourcePackFolder,
                    CrashPresentationActionKind.ExportMarkdown
                ],
                nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class LoaderDependencyRule : CrashDiagnosisRule
    {
        public override string Id => "loader.dependency";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LoaderDependencyMissing;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.ModLoader;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;
            var parameters = new Dictionary<string, string>();

            foreach (var fact in facts
                         .Find(CrashFactKind.MissingModDependencyDetected)
                         .Take(2))
            {
                score += 90;
                evidence.Add(Evidence(fact, 90));
                _CopyDependencyParameters(fact, parameters);
            }

            score = Math.Min(score, 95);

            foreach (var fact in facts
                         .Find(CrashFactKind.LoaderResolutionError)
                         .Take(1))
            {
                score += 30;
                evidence.Add(Evidence(fact, 30));
                _CopyDependencyParameters(fact, parameters);
            }

            foreach (var fact in facts
                         .Find(CrashFactKind.LoaderDependencyError)
                         .Take(1))
            {
                score += 25;
                evidence.Add(Evidence(fact, 25));
                _CopyDependencyParameters(fact, parameters);
            }

            foreach (var fact in facts
                         .Find(CrashFactKind.ModVersionConflictDetected)
                         .Take(1))
            {
                score += 50;
                evidence.Add(Evidence(fact, 50));
                _CopyDependencyParameters(fact, parameters);
            }

            if (parameters.TryGetValue("MissingModId", out var missingModId))
            {
                parameters.TryAdd("MissingDependency", missingModId);
                score += 15;
            }

            if (parameters.TryGetValue("AffectedMod", out var affectedMod))
                parameters.TryAdd("AffectedModName", affectedMod);
            else if (parameters.TryGetValue("AffectedModId", out var affectedModId))
                parameters.TryAdd("AffectedMod", affectedModId);

            if (!parameters.ContainsKey("RequiredVersion"))
                parameters.TryAdd("RequiredVersion", "compatible version");
            if (!parameters.ContainsKey("LoaderName"))
                parameters.TryAdd("LoaderName", "Mod loader");
            if (parameters.ContainsKey("AffectedModId")) score += 10;

            return score < 40
                ? null
                : Create(score, evidence,
                        actions:
                        [
                            CrashPresentationActionKind.OpenInstanceModsFolder,
                            CrashPresentationActionKind.OpenInstanceSettings
                        ],
                        nature: CrashDiagnosisNature.RootCause) with
                    {
                        Parameters = parameters
                    };
        }

        private static void _CopyDependencyParameters(CrashFact fact, Dictionary<string, string> parameters)
        {
            foreach (var pair in fact.Properties)
                parameters.TryAdd(pair.Key, pair.Value);
        }
    }

    private sealed class MixinTransformRule : CrashDiagnosisRule
    {
        public override string Id => "loader.mixin_or_transform";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LoaderMixinFailure;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.ModLoader;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.LoaderMixinError)
                .Concat(facts.Find(CrashFactKind.LoaderTransformError))
                .ToList();
            if (related.Count == 0) return null;

            var hasDependencyRootCause = facts.Has(CrashFactKind.MissingModDependencyDetected) ||
                                         facts.Has(CrashFactKind.LoaderDependencyError) ||
                                         facts.Has(CrashFactKind.LoaderResolutionError);
            var hasJavaRootCause = facts.Has(CrashFactKind.JavaUnsupportedClassVersionDetected) ||
                                   facts.Has(CrashFactKind.JavaModuleAccessErrorDetected) ||
                                   facts.Has(CrashFactKind.JavaRequiredVersionDetected);
            var baseScore = related.Any(f => f.Kind == CrashFactKind.LoaderMixinError) ? 55 : 45;
            if (hasDependencyRootCause) baseScore -= 35;
            if (hasJavaRootCause) baseScore -= 25;

            if (baseScore < 30) return null;

            var diagnosis = Create(
                Math.Max(0, baseScore),
                related.Take(2)
                    .Select(f => Evidence(f, f.Kind == CrashFactKind.LoaderMixinError ? 55 : 45))
                    .ToList(),
                actions:
                [
                    CrashPresentationActionKind.OpenInstanceModsFolder,
                    CrashPresentationActionKind.ExportMarkdown
                ],
                nature: CrashDiagnosisNature.Symptom);
            if (hasDependencyRootCause)
                diagnosis = diagnosis with
                {
                    Notes =
                    [
                        new CrashDiagnosisNote
                        {
                            Key = "Crash.Note.DependencyOverridesMixin",
                            Level = CrashDiagnosisNoteLevel.Warning
                        }
                    ]
                };
            return diagnosis;
        }
    }

    private sealed class DuplicateModRule : CrashDiagnosisRule
    {
        public override string Id => "mod.duplicate";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModDuplicateInstalled;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var duplicates = facts
                .Find(CrashFactKind.DuplicateModDetected)
                .ToList();
            return duplicates.Count == 0
                ? null
                : Create(
                    85,
                    duplicates
                        .Select(f => Evidence(f, 85))
                        .ToList(),
                    actions: [CrashPresentationActionKind.OpenInstanceModsFolder],
                    nature: CrashDiagnosisNature.RootCause);
        }
    }

    private sealed class ModFileRule : CrashDiagnosisRule
    {
        public override string Id => "mod.file_integrity";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModFileInvalidOrCorrupted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var files = facts
                .Find(CrashFactKind.ModFileCorrupted)
                .Concat(facts.Find(CrashFactKind.ModFileNameInvalid))
                .ToList();
            return files.Count == 0
                ? null
                : Create(
                    70,
                    files
                        .Select(f => Evidence(f, 70))
                        .ToList(),
                    actions: [CrashPresentationActionKind.OpenInstanceModsFolder],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class WorldContentRule : CrashDiagnosisRule
    {
        public override string Id => "game.world_content";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GameWorldBlockEntityCorrupted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var block = facts.Find(CrashFactKind.WorldBlockEntityIssueDetected).ToList();
            if (block.Count > 0)
                return Create(
                        65,
                        block
                            .Select(f => Evidence(f, 65))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.GameWorldBlockEntityCorrupted
                    };

            var entity = facts
                .Find(CrashFactKind.WorldEntityIssueDetected)
                .ToList();
            return entity.Count == 0
                ? null
                : Create(
                        65,
                        entity
                            .Select(f => Evidence(f, 65))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.GameWorldEntityCorrupted
                    };
        }
    }

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
                .Find(CrashFactKind.JavaFatalErrorDetected)
                .Concat(facts.Find(CrashFactKind.NativeAccessViolationDetected))
                .ToList();
            return fatal.Count == 0
                ? null
                : Create(
                    55,
                    fatal
                        .Select(f => Evidence(f, 55))
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
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
            new GraphicsRule(),
            new LoaderDependencyRule(),
            new MixinTransformRule(),
            new DuplicateModRule(),
            new ModFileRule(),
            new WorldContentRule(),
            new NativeJvmRule(),
            new UnknownRule()
        ];
    }

    private sealed class NoUsefulLogRule : CrashDiagnosisRule
    {
        public override string Id => "launcher.no_useful_log";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LauncherCapturedNoUsefulLog;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Launcher;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            return bundle.HasUsefulLog
                ? null
                : Create(70, [], actions: [CrashPresentationActionKind.ExportReport],
                    severity: CrashDiagnosisSeverity.Warning);
        }
    }

    private sealed class MemoryRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.memory.exhausted";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeMemoryExhausted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;
            foreach (var fact in facts.Find(CrashFactKind.JavaOutOfMemoryDetected))
            {
                score += 80;
                evidence.Add(Evidence(fact, 80));
            }

            foreach (var fact in facts.Find(CrashFactKind.MemoryAllocationDetected))
            {
                score += 15;
                evidence.Add(Evidence(fact, 15));
            }

            foreach (var fact in facts.Find(CrashFactKind.ProcessBitnessDetected))
            {
                score += 15;
                evidence.Add(Evidence(fact, 15));
            }

            return score < 35
                ? null
                : Create(score, evidence,
                    actions:
                    [
                        CrashPresentationActionKind.OpenMemorySettings, CrashPresentationActionKind.ExportMarkdown
                    ]);
        }
    }

    private sealed class JavaCompatibilityRule : CrashDiagnosisRule
    {
        public override string Id => "runtime.java.compatibility";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.RuntimeJavaTooOld;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Runtime;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var unsupported = facts.First(CrashFactKind.JavaUnsupportedClassVersionDetected);
            var module = facts.First(CrashFactKind.JavaModuleAccessErrorDetected);
            if (unsupported is null && module is null) return null;
            if (unsupported is not null)
                return Create(90, [Evidence(unsupported, 90)], actions: [CrashPresentationActionKind.OpenJavaSettings]);
            return new CrashDiagnosis
            {
                RuleId = "runtime.java.module_access",
                Code = CrashDiagnosisCode.RuntimeJavaTooNew,
                Category = CrashDiagnosisCategory.Runtime,
                Severity = CrashDiagnosisSeverity.Error,
                Score = 75,
                Confidence = CrashDiagnosisConfidence.High,
                Evidence = [Evidence(module!, 75)],
                SuggestedActionKinds = [CrashPresentationActionKind.OpenJavaSettings]
            };
        }
    }

    private sealed class GraphicsRule : CrashDiagnosisRule
    {
        public override string Id => "graphics.driver_or_opengl";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsDriverNativeCrash;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Graphics;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;
            foreach (var fact in facts.Find(CrashFactKind.OpenGlInitializationFailed)
                         .Concat(facts.Find(CrashFactKind.LwjglInitializationFailed)))
            {
                score += 80;
                evidence.Add(Evidence(fact, 80));
            }

            foreach (var fact in facts.Find(CrashFactKind.GpuDriverIssueHint))
            {
                score += 70;
                evidence.Add(Evidence(fact, 70));
            }

            foreach (var fact in facts.Find(CrashFactKind.ShaderIssueDetected)
                         .Concat(facts.Find(CrashFactKind.ResourcePackIssueDetected)))
            {
                score += 35;
                evidence.Add(Evidence(fact, 35));
            }

            if (score < 35) return null;
            var code = facts.Has(CrashFactKind.ShaderIssueDetected) ||
                       facts.Has(CrashFactKind.ResourcePackIssueDetected)
                ? CrashDiagnosisCode.GraphicsResourceOrShaderOverload
                : facts.Has(CrashFactKind.OpenGlInitializationFailed)
                    ? CrashDiagnosisCode.GraphicsOpenGlUnavailable
                    : CrashDiagnosisCode.GraphicsDriverNativeCrash;
            return Create(score, evidence,
                    actions:
                    [
                        CrashPresentationActionKind.OpenResourcePackFolder, CrashPresentationActionKind.ExportMarkdown
                    ]) with
                {
                    Code = code
                };
        }
    }

    private sealed class LoaderDependencyRule : CrashDiagnosisRule
    {
        public override string Id => "loader.dependency";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LoaderDependencyMissing;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.ModLoader;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var evidence = new List<CrashDiagnosisEvidence>();
            var score = 0;
            var parameters = new Dictionary<string, string>();

            foreach (var fact in facts.Find(CrashFactKind.MissingModDependencyDetected))
            {
                score += 90;
                evidence.Add(Evidence(fact, 90));
                _CopyDependencyParameters(fact, parameters);
            }

            foreach (var fact in facts.Find(CrashFactKind.LoaderResolutionError))
            {
                score += 70;
                evidence.Add(Evidence(fact, 70));
                _CopyDependencyParameters(fact, parameters);
            }

            foreach (var fact in facts.Find(CrashFactKind.LoaderDependencyError))
            {
                score += 60;
                evidence.Add(Evidence(fact, 60));
                _CopyDependencyParameters(fact, parameters);
            }

            foreach (var fact in facts.Find(CrashFactKind.ModVersionConflictDetected))
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
                    ]) with
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

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts.Find(CrashFactKind.LoaderMixinError)
                .Concat(facts.Find(CrashFactKind.LoaderTransformError)).ToList();
            if (related.Count == 0) return null;

            var hasDependencyRootCause = facts.Has(CrashFactKind.MissingModDependencyDetected) ||
                                         facts.Has(CrashFactKind.LoaderDependencyError) ||
                                         facts.Has(CrashFactKind.LoaderResolutionError);
            var hasJavaRootCause = facts.Has(CrashFactKind.JavaUnsupportedClassVersionDetected) ||
                                   facts.Has(CrashFactKind.JavaModuleAccessErrorDetected);
            var baseScore = related.Any(f => f.Kind == CrashFactKind.LoaderMixinError) ? 55 : 45;
            if (hasDependencyRootCause) baseScore -= 35;
            if (hasJavaRootCause) baseScore -= 25;

            if (baseScore < 30) return null;

            var diagnosis = Create(Math.Max(0, baseScore),
                related.Select(f => Evidence(f, f.Kind == CrashFactKind.LoaderMixinError ? 55 : 45)).ToList(),
                actions:
                [
                    CrashPresentationActionKind.OpenInstanceModsFolder, CrashPresentationActionKind.ExportMarkdown
                ]);
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

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var duplicates = facts.Find(CrashFactKind.DuplicateModDetected).ToList();
            return duplicates.Count == 0
                ? null
                : Create(85, duplicates.Select(f => Evidence(f, 85)).ToList(),
                    actions: [CrashPresentationActionKind.OpenInstanceModsFolder]);
        }
    }

    private sealed class ModFileRule : CrashDiagnosisRule
    {
        public override string Id => "mod.file_integrity";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModFileInvalidOrCorrupted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var files = facts.Find(CrashFactKind.ModFileCorrupted).Concat(facts.Find(CrashFactKind.ModFileNameInvalid))
                .ToList();
            return files.Count == 0
                ? null
                : Create(70, files.Select(f => Evidence(f, 70)).ToList(),
                    actions: [CrashPresentationActionKind.OpenInstanceModsFolder]);
        }
    }

    private sealed class WorldContentRule : CrashDiagnosisRule
    {
        public override string Id => "game.world_content";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GameWorldBlockEntityCorrupted;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var block = facts.Find(CrashFactKind.WorldBlockEntityIssueDetected).ToList();
            if (block.Count > 0)
                return Create(65, block.Select(f => Evidence(f, 65)).ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown]) with
                    {
                        Code = CrashDiagnosisCode.GameWorldBlockEntityCorrupted
                    };
            var entity = facts.Find(CrashFactKind.WorldEntityIssueDetected).ToList();
            return entity.Count == 0
                ? null
                : Create(65, entity.Select(f => Evidence(f, 65)).ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown]) with
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

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var fatal = facts.Find(CrashFactKind.JavaFatalErrorDetected)
                .Concat(facts.Find(CrashFactKind.NativeAccessViolationDetected)).ToList();
            return fatal.Count == 0
                ? null
                : Create(55, fatal.Select(f => Evidence(f, 55)).ToList(),
                    actions: [CrashPresentationActionKind.OpenJavaSettings, CrashPresentationActionKind.ExportReport]);
        }
    }

    private sealed class UnknownRule : CrashDiagnosisRule
    {
        public override string Id => "unknown.fallback";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.Unknown;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Unknown;

        public override CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            return facts.Facts.Count == 0 && bundle.HasUsefulLog
                ? Create(20, [],
                    actions: [CrashPresentationActionKind.ExportMarkdown, CrashPresentationActionKind.ExportReport],
                    severity: CrashDiagnosisSeverity.Warning)
                : null;
        }
    }
}
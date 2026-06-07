namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
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
            var hasExplicitMissingDependency = facts.Has(CrashFactKind.MissingModDependencyDetected) ||
                                               facts.Has(CrashFactKind.ForgeMissingMandatoryDependencyDetected);
            if (!hasExplicitMissingDependency && facts.Has(CrashFactKind.ModSetConflictDetected))
                return null;

            var hasVersionConflict = facts.Has(CrashFactKind.ModVersionConflictDetected);

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
                         .Find(CrashFactKind.ForgeMissingMandatoryDependencyDetected)
                         .Take(1))
            {
                score += 70;
                evidence.Add(Evidence(fact, 70));
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
                        Code = hasVersionConflict && !hasExplicitMissingDependency
                            ? CrashDiagnosisCode.LoaderDependencyVersionConflict
                            : CrashDiagnosisCode.LoaderDependencyMissing,
                        Parameters = parameters
                    };
        }

        private static void _CopyDependencyParameters(CrashFact fact, Dictionary<string, string> parameters)
        {
            foreach (var pair in fact.Properties)
                parameters.TryAdd(pair.Key, pair.Value);
        }
    }

    private sealed class LoaderVersionRule : CrashDiagnosisRule
    {
        public override string Id => "loader.version_incompatible";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LoaderVersionIncompatible;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.ModLoader;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            if (facts.Has(CrashFactKind.MissingModDependencyDetected) ||
                facts.Has(CrashFactKind.ForgeMissingMandatoryDependencyDetected))
                return null;

            var loaderVersionRequirements = facts
                .Find(CrashFactKind.LoaderVersionRequirementDetected)
                .Where(static fact => _LooksLikeLoaderOrGameVersionRequirement(fact))
                .ToList();

            var modVersionConflicts = facts
                .Find(CrashFactKind.ModVersionConflictDetected)
                .Where(static fact => _LooksLikeLoaderOrGameVersionRequirement(fact.Value))
                .ToList();

            if (facts.Has(CrashFactKind.ModSetConflictDetected) &&
                !facts.Has(CrashFactKind.ForgeVersionRequirementDetected) &&
                loaderVersionRequirements.Count == 0 &&
                modVersionConflicts.Count == 0)
                return null;

            var related = loaderVersionRequirements
                .Concat(modVersionConflicts)
                .Take(3)
                .ToList();
            if (related.Count == 0) return null;

            return Create(
                Math.Min(85, 60 + related.Count * 15),
                related
                    .Select(fact => Evidence(fact, 60))
                    .ToList(),
                actions: [CrashPresentationActionKind.OpenInstanceSettings],
                nature: CrashDiagnosisNature.RootCause);
        }

        private static bool _LooksLikeLoaderOrGameVersionRequirement(CrashFact fact)
        {
            var value = fact.Value + "\n" + string.Join("\n", fact.Evidence
                .Select(static evidence => evidence.Excerpt)
                .Where(static excerpt => !string.IsNullOrWhiteSpace(excerpt)));

            return _LooksLikeLoaderOrGameVersionRequirement(value);
        }

        private static bool _LooksLikeLoaderOrGameVersionRequirement(string value)
        {
            if (_LooksLikeFabricModConflict(value))
                return false;

            return value.Contains("minecraft", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("loader", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("fabric", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("forge", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("quilt", StringComparison.OrdinalIgnoreCase);
        }

        private static bool _LooksLikeFabricModConflict(string value)
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
    }

    private sealed class ForgeModLoadingRule : CrashDiagnosisRule
    {
        public override string Id => "loader.mod_loading_failed";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.LoaderModLoadingFailed;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.ModLoader;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            if (facts.Has(CrashFactKind.MissingModDependencyDetected) ||
                facts.Has(CrashFactKind.ForgeMissingMandatoryDependencyDetected) ||
                facts.Has(CrashFactKind.LoaderVersionRequirementDetected))
                return null;

            var related = facts
                .Find(CrashFactKind.ForgeModLoadingErrorDetected)
                .Concat(facts.Find(CrashFactKind.LoaderModLoadingFailed))
                .Concat(facts.Find(CrashFactKind.ForgeLanguageProviderMissingDetected))
                .Take(3)
                .ToList();
            if (related.Count == 0) return null;

            return Create(
                    Math.Min(80, 55 + related.Count * 15),
                    related
                        .Select(fact => Evidence(fact, 55))
                        .ToList(),
                    actions:
                    [
                        CrashPresentationActionKind.OpenInstanceModsFolder,
                        CrashPresentationActionKind.ExportMarkdown
                    ],
                    nature: CrashDiagnosisNature.ProbableCause) with
                {
                    Code = related.Any(static fact => fact.Kind == CrashFactKind.ForgeLanguageProviderMissingDetected)
                        ? CrashDiagnosisCode.LoaderInstallationIncomplete
                        : CrashDiagnosisCode.LoaderModLoadingFailed
                };
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
                    nature: CrashDiagnosisNature.Symptom) with
                {
                    Code = related.Any(static fact => fact.Kind == CrashFactKind.LoaderMixinError)
                        ? CrashDiagnosisCode.LoaderMixinFailure
                        : CrashDiagnosisCode.LoaderTransformFailure
                };
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
}
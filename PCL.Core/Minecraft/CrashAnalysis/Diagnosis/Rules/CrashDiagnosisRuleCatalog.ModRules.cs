namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    private sealed class CrashReportSuspectedModRule : CrashDiagnosisRule
    {
        public override string Id => "mod.crash_report_suspected";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModLikelyCausedCrash;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var suspected = facts
                .Find(CrashFactKind.CrashReportSuspectedModDetected)
                .Take(2)
                .ToList();
            if (suspected.Count == 0) return null;

            var parameters = new Dictionary<string, string>();
            foreach (var pair in suspected.SelectMany(fact => fact.Properties))
                parameters.TryAdd(pair.Key, pair.Value);

            return Create(
                Math.Min(78, 58 + suspected.Count * 12),
                suspected
                    .Select(fact => Evidence(fact, 70))
                    .ToList(),
                parameters,
                [
                    CrashPresentationActionKind.OpenInstanceModsFolder,
                    CrashPresentationActionKind.ExportMarkdown
                ],
                nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class ModSetConflictRule : CrashDiagnosisRule
    {
        public override string Id => "mod.set_conflict";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModSetConflict;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var conflicts = facts
                .Find(CrashFactKind.ModSetConflictDetected)
                .Take(3)
                .ToList();
            if (conflicts.Count == 0 || facts.Has(CrashFactKind.MissingModDependencyDetected))
                return null;

            var parameters = new Dictionary<string, string>();
            foreach (var pair in conflicts.SelectMany(static fact => fact.Properties))
                parameters.TryAdd(pair.Key, pair.Value);

            var score = conflicts.Any(static fact =>
                fact.Properties.ContainsKey("ConflictModId") &&
                fact.Properties.ContainsKey("ConflictingModId"))
                ? 95
                : Math.Min(92, 70 + conflicts.Count * 10);

            return Create(
                score,
                conflicts
                    .Select(fact => Evidence(fact, 85))
                    .ToList(),
                parameters,
                [CrashPresentationActionKind.OpenInstanceModsFolder, CrashPresentationActionKind.ExportMarkdown],
                nature: CrashDiagnosisNature.RootCause);
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

    private sealed class ModConfigRule : CrashDiagnosisRule
    {
        public override string Id => "mod.config_invalid";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.ModConfigInvalid;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Mod;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.ModConfigParseFailed)
                .Concat(facts.Find(CrashFactKind.ConfigParseIssueDetected))
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(
                    70,
                    related
                        .Select(fact => Evidence(fact, 70))
                        .ToList(),
                    actions: [CrashPresentationActionKind.OpenInstanceModsFolder],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }
}
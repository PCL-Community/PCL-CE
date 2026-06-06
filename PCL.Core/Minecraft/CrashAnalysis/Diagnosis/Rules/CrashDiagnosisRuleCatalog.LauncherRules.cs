namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
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

    private sealed class FileSystemRule : CrashDiagnosisRule
    {
        public override string Id => "launcher.file_system";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.FileAccessOrPermissionIssue;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.Launcher;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var diskFull = facts
                .Find(CrashFactKind.DiskFullDetected)
                .Take(2)
                .ToList();
            if (diskFull.Count > 0)
                return Create(
                        88,
                        diskFull
                            .Select(fact => Evidence(fact, 88))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.RootCause) with
                    {
                        Code = CrashDiagnosisCode.DiskSpaceInsufficient
                    };

            var pathTooLong = facts
                .Find(CrashFactKind.PathTooLongDetected)
                .Take(2)
                .ToList();
            if (pathTooLong.Count > 0)
                return Create(
                        76,
                        pathTooLong
                            .Select(fact => Evidence(fact, 76))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.PathOrFolderEnvironmentIssue
                    };

            var accessDenied = facts
                .Find(CrashFactKind.AccessDeniedDetected)
                .Take(3)
                .ToList();
            return accessDenied.Count == 0
                ? null
                : Create(
                    78,
                    accessDenied
                        .Select(fact => Evidence(fact, 78))
                        .ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }
}
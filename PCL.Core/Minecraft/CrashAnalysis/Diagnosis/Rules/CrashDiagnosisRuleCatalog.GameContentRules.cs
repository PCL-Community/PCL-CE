namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    private sealed class GameResourceOrShaderRule : CrashDiagnosisRule
    {
        public override string Id => "game.resource_or_shader";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GraphicsResourceOrShaderOverload;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var shader = facts
                .Find(CrashFactKind.ShaderCompileFailedDetected)
                .Concat(facts.Find(CrashFactKind.ShaderIssueDetected))
                .Take(3)
                .ToList();
            if (shader.Count > 0)
                return Create(Math.Min(78, 58 + shader.Count * 10),
                        shader.Select(fact => Evidence(fact, 60)).ToList(),
                        actions:
                        [
                            CrashPresentationActionKind.OpenResourcePackFolder,
                            CrashPresentationActionKind.ExportMarkdown
                        ],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.GameShaderFailed
                    };

            var resourcePack = facts
                .Find(CrashFactKind.ResourcePackIssueDetected)
                .Take(3)
                .ToList();
            if (resourcePack.Count > 0)
                return Create(Math.Min(76, 56 + resourcePack.Count * 10),
                        resourcePack.Select(fact => Evidence(fact, 58)).ToList(),
                        actions:
                        [
                            CrashPresentationActionKind.OpenResourcePackFolder,
                            CrashPresentationActionKind.ExportMarkdown
                        ],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.GameResourcePackFailed
                    };

            var textureAtlas = facts
                .Find(CrashFactKind.TextureAtlasTooLargeDetected)
                .Take(3)
                .ToList();
            return textureAtlas.Count == 0
                ? null
                : Create(Math.Min(70, 50 + textureAtlas.Count * 10),
                    textureAtlas.Select(fact => Evidence(fact, 50)).ToList(),
                    actions:
                    [
                        CrashPresentationActionKind.OpenResourcePackFolder,
                        CrashPresentationActionKind.ExportMarkdown
                    ],
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
            if (facts.Has(CrashFactKind.JavaOutOfMemoryDetected) ||
                facts.Has(CrashFactKind.JavaFatalErrorDetected) ||
                facts.Has(CrashFactKind.NativeAccessViolationDetected) ||
                facts.Has(CrashFactKind.ManualDebugCrashDetected))
                return null;

            var worldData = facts
                .Find(CrashFactKind.WorldChunkLoadFailed)
                .Concat(facts.Find(CrashFactKind.WorldNbtReadFailed))
                .Take(3)
                .ToList();
            if (worldData.Count > 0)
                return Create(
                        74,
                        worldData
                            .Select(f => Evidence(f, 74))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.GameWorldDataCorrupted
                    };

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

    private sealed class DataPackRule : CrashDiagnosisRule
    {
        public override string Id => "game.datapack_failed";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GameDataPackFailed;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.DataPackLoadFailed)
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(
                    75,
                    related
                        .Select(fact => Evidence(fact, 75))
                        .ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class RegistryRule : CrashDiagnosisRule
    {
        public override string Id => "game.registry_mismatch";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GameRegistryMismatch;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var related = facts
                .Find(CrashFactKind.RegistryEntryMissingDetected)
                .Take(3)
                .ToList();
            return related.Count == 0
                ? null
                : Create(
                    72,
                    related
                        .Select(fact => Evidence(fact, 72))
                        .ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.ProbableCause);
        }
    }

    private sealed class GameFileIntegrityRule : CrashDiagnosisRule
    {
        public override string Id => "game.file_integrity";
        public override CrashDiagnosisCode Code => CrashDiagnosisCode.GameFileIntegrityIssue;
        public override CrashDiagnosisCategory Category => CrashDiagnosisCategory.GameContent;

        public override CrashDiagnosis? Evaluate(
            CrashLogBundle bundle,
            CrashFactSet facts,
            CrashAnalysisRequest request)
        {
            var integrity = facts
                .Find(CrashFactKind.GameJarMissingDetected)
                .Concat(facts.Find(CrashFactKind.ChecksumMismatchDetected))
                .ToList();
            if (integrity.Count > 0)
                return Create(
                    82,
                    integrity
                        .Take(3)
                        .Select(fact => Evidence(fact, 82))
                        .ToList(),
                    actions: [CrashPresentationActionKind.ExportMarkdown],
                    nature: CrashDiagnosisNature.RootCause);

            var assets = facts
                .Find(CrashFactKind.AssetMissingDetected)
                .Take(3)
                .ToList();
            return assets.Count == 0
                ? null
                : Create(
                        68,
                        assets
                            .Select(fact => Evidence(fact, 68))
                            .ToList(),
                        actions: [CrashPresentationActionKind.ExportMarkdown],
                        nature: CrashDiagnosisNature.ProbableCause) with
                    {
                        Code = CrashDiagnosisCode.AssetMissingOrCorrupted
                    };
        }
    }
}
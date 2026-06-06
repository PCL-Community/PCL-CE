using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashFixtureDiagnosisTests
{
    private static readonly string[] ExpectedFixtures =
    [
        "oom_heap.txt",
        "oom_metaspace.txt",
        "java_too_old_class61.txt",
        "java_too_new_old_forge.txt",
        "fabric_missing_dependency.txt",
        "forge_missing_dependency.txt",
        "forge_mod_loading_error.txt",
        "duplicate_mod.txt",
        "invalid_mod_jar.txt",
        "config_parse_error.txt",
        "glfw_opengl_unsupported.txt",
        "gpu_native_nvidia.txt",
        "lwjgl_native_missing.txt",
        "ticking_entity.txt",
        "datapack_failed.txt",
        "access_denied.txt",
        "disk_full.txt",
        "unknown_with_generic_exception.txt"
    ];

    [TestMethod]
    public void AllRequestedFixturesExist()
    {
        foreach (var fixture in ExpectedFixtures)
            Assert.IsTrue(File.Exists(_FixturePath(fixture)), "Missing crash fixture: " + fixture);
    }

    [TestMethod]
    [DataRow("oom_heap.txt", CrashDiagnosisCode.RuntimeMemoryHeapExhausted, CrashDiagnosisConfidence.High,
        "JavaOutOfMemoryDetected", 2, "", "")]
    [DataRow("oom_metaspace.txt", CrashDiagnosisCode.RuntimeMemoryMetaspaceExhausted, CrashDiagnosisConfidence.High,
        "JavaOutOfMemoryDetected", 2, "", "")]
    [DataRow("java_too_old_class61.txt", CrashDiagnosisCode.RuntimeJavaTooOld, CrashDiagnosisConfidence.High,
        "JavaUnsupportedClassVersionDetected", 2, "LoaderMixinFailure;ModLikelyCausedCrash", "Java 8")]
    [DataRow("java_too_new_old_forge.txt", CrashDiagnosisCode.RuntimeJavaTooNew, CrashDiagnosisConfidence.High,
        "JavaModuleAccessErrorDetected", 2, "LoaderMixinFailure;ModLikelyCausedCrash", "Java 17")]
    [DataRow("fabric_missing_dependency.txt", CrashDiagnosisCode.LoaderDependencyMissing, CrashDiagnosisConfidence.High,
        "MissingModDependencyDetected", 2, "LoaderMixinFailure;ModLikelyCausedCrash", "")]
    [DataRow("forge_missing_dependency.txt", CrashDiagnosisCode.LoaderDependencyMissing, CrashDiagnosisConfidence.High,
        "ForgeMissingMandatoryDependencyDetected", 2, "LoaderModLoadingFailed", "")]
    [DataRow("forge_mod_loading_error.txt", CrashDiagnosisCode.LoaderModLoadingFailed, CrashDiagnosisConfidence.High,
        "ForgeModLoadingErrorDetected", 2, "LoaderDependencyMissing", "")]
    [DataRow("duplicate_mod.txt", CrashDiagnosisCode.ModDuplicateInstalled, CrashDiagnosisConfidence.High,
        "DuplicateModDetected", 2, "LoaderDependencyMissing", "")]
    [DataRow("invalid_mod_jar.txt", CrashDiagnosisCode.ModFileInvalidOrCorrupted, CrashDiagnosisConfidence.High,
        "ModFileCorrupted", 2, "LibraryOrNativeMissing", "")]
    [DataRow("config_parse_error.txt", CrashDiagnosisCode.ModConfigInvalid, CrashDiagnosisConfidence.High,
        "ModConfigParseFailed", 2, "ModFileInvalidOrCorrupted", "")]
    [DataRow("glfw_opengl_unsupported.txt", CrashDiagnosisCode.GraphicsOpenGlUnavailable, CrashDiagnosisConfidence.High,
        "OpenGlInitializationFailed", 2, "GraphicsDriverNativeCrash;NativeJvmCrash", "")]
    [DataRow("gpu_native_nvidia.txt", CrashDiagnosisCode.GraphicsDriverNativeCrash, CrashDiagnosisConfidence.High,
        "GpuDriverIssueHint", 4, "GraphicsOpenGlUnavailable;NativeJvmCrash", "")]
    [DataRow("lwjgl_native_missing.txt", CrashDiagnosisCode.GraphicsLwjglNativeLoadFailed,
        CrashDiagnosisConfidence.High,
        "NativeLibraryMissingDetected", 2, "GraphicsOpenGlUnavailable", "")]
    [DataRow("ticking_entity.txt", CrashDiagnosisCode.GameWorldEntityCorrupted, CrashDiagnosisConfidence.Medium,
        "WorldEntityIssueDetected", 2, "GameWorldBlockEntityCorrupted", "")]
    [DataRow("datapack_failed.txt", CrashDiagnosisCode.GameDataPackFailed, CrashDiagnosisConfidence.High,
        "DataPackLoadFailed", 2, "GameRegistryMismatch", "")]
    [DataRow("access_denied.txt", CrashDiagnosisCode.FileAccessOrPermissionIssue, CrashDiagnosisConfidence.High,
        "AccessDeniedDetected", 2, "DiskSpaceInsufficient;PathOrFolderEnvironmentIssue", "")]
    [DataRow("disk_full.txt", CrashDiagnosisCode.DiskSpaceInsufficient, CrashDiagnosisConfidence.High,
        "DiskFullDetected", 2, "FileAccessOrPermissionIssue", "")]
    [DataRow("unknown_with_generic_exception.txt", CrashDiagnosisCode.AnalysisInconclusive,
        CrashDiagnosisConfidence.Low, "", 0, "RuntimeMemoryExhausted;LoaderDependencyMissing;GraphicsOpenGlUnavailable",
        "")]
    public void FixtureProducesExpectedTopDiagnosis(
        string fixtureName,
        CrashDiagnosisCode expectedCode,
        CrashDiagnosisConfidence minimumConfidence,
        string expectedFactKind,
        int expectedLine,
        string forbiddenCodes,
        string javaInfo)
    {
        var result = _AnalyzeFixture(fixtureName, javaInfo);

        Assert.IsNotEmpty(result.Diagnoses, "No diagnosis was produced for fixture: " + fixtureName);
        var top = result.Diagnoses[0];

        Assert.AreEqual(expectedCode, top.Code, _FormatDiagnoses(result));
        Assert.IsTrue(top.Confidence >= minimumConfidence,
            $"Expected at least {minimumConfidence} confidence for {fixtureName}, but got {top.Confidence}.\n" +
            _FormatDiagnoses(result));

        if (!string.IsNullOrWhiteSpace(expectedFactKind))
        {
            var factKind = Enum.Parse<CrashFactKind>(expectedFactKind);
            Assert.Contains(evidence =>
                    evidence.FactKind == factKind && evidence.LineNumber == expectedLine, top.Evidence,
                $"Top diagnosis for {fixtureName} does not include {factKind} evidence from line {expectedLine}.\n" +
                _FormatDiagnoses(result));
        }

        foreach (var forbidden in _ParseDiagnosisCodes(forbiddenCodes))
            Assert.DoesNotContain(diagnosis => diagnosis.Code == forbidden, result.Diagnoses,
                $"Fixture {fixtureName} produced unrelated diagnosis {forbidden}.\n" +
                _FormatDiagnoses(result));
    }

    [TestMethod]
    public void FabricDependencyFixtureKeepsMixinAsSymptom()
    {
        var result = _AnalyzeFixture("fabric_missing_dependency.txt");

        Assert.AreEqual(CrashDiagnosisCode.LoaderDependencyMissing, result.TopDiagnosis?.Code);
        Assert.DoesNotContain(static diagnosis =>
            diagnosis.Code == CrashDiagnosisCode.LoaderMixinFailure &&
            diagnosis.Nature != CrashDiagnosisNature.Symptom, result.Diagnoses);
    }

    [TestMethod]
    public void UnknownFixtureStillProducesUsefulInconclusiveDiagnosis()
    {
        var result = _AnalyzeFixture("unknown_with_generic_exception.txt");

        Assert.AreEqual(CrashDiagnosisCode.AnalysisInconclusive, result.TopDiagnosis?.Code);
        Assert.IsNotEmpty(result.Facts.Facts, "The unknown fixture should still expose parsed facts.");
    }

    private static CrashAnalysisResult _AnalyzeFixture(string fixtureName, string javaInfo = "")
    {
        var lines = File.ReadAllLines(_FixturePath(fixtureName));
        var request = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            Mode = CrashAnalysisMode.Automatic,
            Now = new DateTimeOffset(2026, 6, 6, 12, 0, 0, TimeSpan.Zero),
            CapturedOutputLines = lines,
            RuntimeContext = string.IsNullOrWhiteSpace(javaInfo)
                ? CrashRuntimeContext.Empty
                : new CrashRuntimeContext { JavaInfo = javaInfo }
        };

        return new CrashAnalyzer().Analyze(request);
    }

    private static IEnumerable<CrashDiagnosisCode> _ParseDiagnosisCodes(string value)
    {
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static item => Enum.Parse<CrashDiagnosisCode>(item));
    }

    private static string _FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var direct = Path.Combine(directory.FullName, "Minecraft", "CrashAnalysis", "Fixtures", fileName);
            if (File.Exists(direct)) return direct;

            var projectRelative = Path.Combine(directory.FullName, "PCL.Core.Test", "Minecraft", "CrashAnalysis",
                "Fixtures", fileName);
            if (File.Exists(projectRelative)) return projectRelative;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find crash fixture: " + fileName, fileName);
    }

    private static string _FormatDiagnoses(CrashAnalysisResult result)
    {
        return string.Join(Environment.NewLine, result.Diagnoses.Select(static diagnosis =>
            $"{diagnosis.Code} score={diagnosis.Score} confidence={diagnosis.Confidence} nature={diagnosis.Nature} evidence={string.Join(", ", diagnosis.Evidence.Select(e => e.FactKind + "@" + e.LineNumber))}"));
    }
}
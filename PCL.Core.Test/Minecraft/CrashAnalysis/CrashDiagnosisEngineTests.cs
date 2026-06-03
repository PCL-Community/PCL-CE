using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashDiagnosisEngineTests
{
    [TestMethod]
    public void AnalyzerFindsOutOfMemoryDiagnosis()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines = ["java.lang.OutOfMemoryError: Java heap space"]
        });

        Assert.IsTrue(result.Diagnoses.Any(static diagnosis =>
            diagnosis.Code == CrashDiagnosisCode.RuntimeMemoryExhausted));
    }

    [TestMethod]
    public void DependencyDiagnosisBeatsMixinSymptom()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines =
            [
                "Mod examplemod requires any version of architectury, which is missing!",
                "Mixin apply failed for examplemod.mixins.json"
            ]
        });

        Assert.AreEqual(CrashDiagnosisCode.LoaderDependencyMissing, result.Diagnoses[0].Code);
    }
}
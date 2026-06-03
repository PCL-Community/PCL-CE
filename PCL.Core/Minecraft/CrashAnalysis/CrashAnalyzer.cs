using System.Diagnostics;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     崩溃分析核心入口。该类只编排输入、事实抽取、诊断评分和展示模型构建。
/// </summary>
public sealed class CrashAnalyzer(
    CrashInputReader inputReader,
    CrashFactExtractor factExtractor,
    CrashDiagnosisEngine diagnosisEngine,
    CrashPresentationBuilder presentationBuilder)
{
    public CrashAnalyzer() : this(
        new CrashInputReader(),
        new CrashFactExtractor(),
        new CrashDiagnosisEngine(),
        new CrashPresentationBuilder())
    {
    }

    public CrashAnalysisResult Analyze(CrashAnalysisRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var bundle = CrashInputReader.Read(request);
        var facts = factExtractor.Extract(bundle, request);
        var diagnoses = diagnosisEngine.Diagnose(bundle, facts, request);
        var presentation = CrashPresentationBuilder.Build(bundle, facts, diagnoses, request, stopwatch.Elapsed);
        stopwatch.Stop();

        return new CrashAnalysisResult
        {
            CreatedAt = request.Now,
            AnalysisDuration = stopwatch.Elapsed,
            Request = request,
            LogBundle = bundle,
            Facts = facts,
            Diagnoses = diagnoses,
            Presentation = presentation
        };
    }
}
using System;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>Minecraft 崩溃分析的唯一核心入口。</p>
///     <p>
///         这个类刻意保持为“薄管线”：它只决定本次分析应从实时游戏日志还是导入文件读取日志，
///         然后依次调用收集/导入、准备、规则执行和报告聚合。这里不允许放置具体规则、正则、
///         用户可见文案、弹窗逻辑或报告保存逻辑，否则会重新退化成旧版 <c>ModCrash.cs</c>。
///     </p>
/// </summary>
public sealed class CrashAnalyzer(
    CrashLogCollector collector,
    CrashLogImporter importer,
    CrashLogPreparer preparer,
    CrashRuleEngine ruleEngine)
{
    public CrashAnalyzer()
        : this(new CrashLogCollector(), new CrashLogImporter(), new CrashLogPreparer(), new CrashRuleEngine())
    {
    }

    /// <summary>
    ///     执行一次完整崩溃分析，并返回结构化结果。
    /// </summary>
    /// <param name="request">分析请求，描述日志来源、模式、实例路径、临时目录和环境信息。</param>
    /// <returns>包含准备后的日志、结构化原因和建议动作的分析报告。</returns>
    public CrashAnalysisReport Analyze(CrashAnalysisRequest request)
    {
        var rawLogs = request.Source switch
        {
            CrashAnalysisSource.LiveGame => CrashLogCollector.Collect(request),
            CrashAnalysisSource.ImportedFile => CrashLogImporter.Import(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Source))
        };

        var preparedLogs = preparer.Prepare(rawLogs, request);
        var findings = CrashRuleEngine.Analyze(preparedLogs, request);

        return CrashAnalysisReport.Create(request, preparedLogs, findings);
    }
}
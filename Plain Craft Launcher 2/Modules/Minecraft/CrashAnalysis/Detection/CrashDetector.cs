using PCL.Core.Logging;

namespace PCL;

internal sealed class CrashDetector
{
    private readonly CrashEvidenceExtractor _extractor = new();
    private readonly CrashStackAnalyzer _stackAnalyzer = new();

    public CrashAnalysisResult Analyze(CrashLogSet logs, McInstance? instance)
    {
        LogWrapper.Info("Crash", "步骤 3：分析崩溃原因");
        var result = new CrashAnalysisResult();
        if (!logs.HasAnalyzableLog)
        {
            result.Add(new CrashFinding(CrashCause.NoAnalyzableFile, CrashConfidence.High) { ShouldStop = true });
            _LogSummary(result);
            return result;
        }

        var normalizedLogs = _Normalize(logs);
        var modIndex = CrashModIndex.Create(normalizedLogs, instance);

        _RunPhase(DetectionPhase.Fatal, normalizedLogs, modIndex, result);
        if (_ShouldStop(result))
        {
            _LogSummary(result);
            return result;
        }

        _RunPhase(DetectionPhase.Primary, normalizedLogs, modIndex, result);
        if (_ShouldStop(result))
        {
            _LogSummary(result);
            return result;
        }

        var stackFinding = _stackAnalyzer.Analyze(normalizedLogs, modIndex);
        if (stackFinding is not null)
            result.Add(stackFinding);
        if (_ShouldStop(result))
        {
            _LogSummary(result);
            return result;
        }

        _RunPhase(DetectionPhase.Secondary, normalizedLogs, modIndex, result);
        _LogSummary(result);
        return result;
    }

    private static CrashLogSet _Normalize(CrashLogSet logs)
    {
        var all = logs.All;
        if (all.Contains("quilt", StringComparison.OrdinalIgnoreCase) &&
            all.Contains("Mod Table Version", StringComparison.Ordinal))
        {
            LogWrapper.Info("Crash", "处理 Quilt Mod Table 后再继续分析");
            all = CrashText.BeforeFirst(all, "| Index") + CrashText.AfterFirst(all, "Mod Table Version:");
        }

        return new CrashLogSet
        {
            Game = logs.Game,
            Debug = logs.Debug,
            CrashReport = logs.CrashReport,
            HsErr = logs.HsErr,
            All = all
        };
    }

    private void _RunPhase(
        DetectionPhase phase,
        CrashLogSet logs,
        CrashModIndex modIndex,
        CrashAnalysisResult result)
    {
        var input = new CrashRuleInput { Logs = logs, ModIndex = modIndex };
        foreach (var rule in CrashRuleCatalog.Rules.Where(rule => rule.Phase == phase))
        {
            var finding = rule.Evaluate(input);
            if (finding is not null)
                _AddAndLog(result, finding);
        }

        foreach (var finding in _extractor.Extract(phase, logs, modIndex))
            _AddAndLog(result, finding);
    }

    private static void _AddAndLog(CrashAnalysisResult result, CrashFinding finding)
    {
        result.Add(finding);
        _LogFinding(finding);
    }

    private static bool _ShouldStop(CrashAnalysisResult result)
    {
        return result.Findings.Any(finding => finding.ShouldStop);
    }

    private static void _LogFinding(CrashFinding finding)
    {
        var evidence = string.Join("；", finding.Details);
        LogWrapper.Info(
            "Crash",
            $"可能的崩溃原因：{finding.Cause}{(string.IsNullOrEmpty(evidence) ? "" : "（" + evidence + "）")}");
    }

    private static void _LogSummary(CrashAnalysisResult result)
    {
        if (!result.Any)
        {
            LogWrapper.Info("Crash", "步骤 3：分析崩溃原因完成，未找到可能的原因");
            return;
        }

        LogWrapper.Info("Crash", $"步骤 3：分析崩溃原因完成，找到 {result.Findings.Count} 条可能的原因");
        foreach (var finding in result.Findings)
            _LogFinding(finding);
    }
}
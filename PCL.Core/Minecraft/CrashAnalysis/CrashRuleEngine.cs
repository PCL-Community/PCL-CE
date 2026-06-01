using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>崩溃规则执行器。</p>
///     <p>
///         执行器只关心规则优先级、命中去重和停止策略。具体匹配文本、正则和复杂判断都应放在
///         <see cref="CrashRuleCatalog" /> 或规则定义中，避免这里变成新的上帝类。
///     </p>
/// </summary>
public sealed class CrashRuleEngine
{
    private static readonly IReadOnlyList<ICrashRule> _Rules = CrashRuleCatalog.Create();

    /// <summary>
    ///     按优先级执行所有规则，并返回去重后的结构化崩溃发现。
    /// </summary>
    public static IReadOnlyList<CrashFinding> Analyze(PreparedCrashLogs logs, CrashAnalysisRequest request)
    {
        var context = new CrashRuleContext(logs, request);
        var findings = new List<CrashFinding>();

        foreach (var group in _Rules.GroupBy(static rule => rule.Priority).OrderBy(static group => group.Key))
        {
            var stopAfterPriority = false;
            foreach (var rule in group)
            {
                if (!rule.IsMatch(context)) continue;

                findings.Add(rule.CreateFinding(context));

                if (rule.Behavior == CrashRuleBehavior.StopAll)
                    return _DistinctFindings(findings);
                if (rule.Behavior == CrashRuleBehavior.StopPriority)
                    stopAfterPriority = true;
            }

            if (stopAfterPriority)
                return _DistinctFindings(findings);
        }

        return _DistinctFindings(findings);
    }

    private static IReadOnlyList<CrashFinding> _DistinctFindings(IEnumerable<CrashFinding> findings)
    {
        return findings
            .GroupBy(static finding => new
            {
                finding.Reason,
                Parameters = string.Join("|",
                    finding.Parameters.Select(static parameter => parameter.Name + "=" + parameter.Value))
            })
            .Select(static group => group.First())
            .ToList();
    }
}
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashRuleEngine
{
    private static readonly IReadOnlyList<ICrashRule> _Rules = CrashRuleCatalog.Create();

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
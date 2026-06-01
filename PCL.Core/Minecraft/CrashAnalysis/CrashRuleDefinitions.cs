using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.CrashAnalysis;

internal interface ICrashRule
{
    string Id { get; }
    CrashRulePriority Priority { get; }
    CrashRuleBehavior Behavior { get; }

    bool IsMatch(CrashRuleContext context);
    CrashFinding CreateFinding(CrashRuleContext context);
}

internal enum CrashRulePriority
{
    Critical = 0,
    High = 100,
    Medium = 200,
    StackTrace = 300,
    Low = 400
}

internal enum CrashRuleBehavior
{
    Continue,
    StopPriority,
    StopAll
}

internal sealed record TextCrashRuleDefinition
{
    public required string Id { get; init; }
    public required CrashReasonCode Reason { get; init; }
    public required CrashRulePriority Priority { get; init; }
    public CrashRuleBehavior Behavior { get; init; } = CrashRuleBehavior.StopPriority;
    public CrashLogSection Section { get; init; } = CrashLogSection.Combined;
    public IReadOnlyList<string> ContainsAny { get; init; } = [];
    public IReadOnlyList<string> ContainsAll { get; init; } = [];
    public CrashFindingConfidence Confidence { get; init; } = CrashFindingConfidence.High;
}

internal sealed record CrashParameterMapping(string ParameterName, string GroupName);

internal sealed record RegexCrashRuleDefinition
{
    public required string Id { get; init; }
    public required CrashReasonCode Reason { get; init; }
    public required CrashRulePriority Priority { get; init; }
    public required Regex Pattern { get; init; }
    public CrashRuleBehavior Behavior { get; init; } = CrashRuleBehavior.StopPriority;
    public CrashLogSection Section { get; init; } = CrashLogSection.Combined;
    public IReadOnlyList<CrashParameterMapping> Parameters { get; init; } = [];
    public CrashFindingConfidence Confidence { get; init; } = CrashFindingConfidence.High;
}

internal sealed class TextCrashRule(TextCrashRuleDefinition definition) : ICrashRule
{
    public string Id => definition.Id;
    public CrashRulePriority Priority => definition.Priority;
    public CrashRuleBehavior Behavior => definition.Behavior;

    public bool IsMatch(CrashRuleContext context)
    {
        var text = context.GetSection(definition.Section);
        if (text.IsEmpty) return false;

        if (definition.ContainsAll.Any(pattern => !text.Contains(pattern))) return false;
        if (definition.ContainsAny.Count > 0 && !definition.ContainsAny.Any(text.Contains)) return false;

        return true;
    }

    public CrashFinding CreateFinding(CrashRuleContext context)
    {
        var evidence =
            definition.ContainsAny.FirstOrDefault(pattern =>
                context.GetSection(definition.Section).Contains(pattern)) ??
            definition.ContainsAll.FirstOrDefault();
        return new CrashFinding
        {
            RuleId = Id,
            Reason = definition.Reason,
            Confidence = definition.Confidence,
            Evidence = [new CrashFindingEvidence { Source = definition.Section.ToLogKind(), MatchedText = evidence }]
        };
    }
}

internal sealed class RegexCrashRule(RegexCrashRuleDefinition definition) : ICrashRule
{
    private Match? _lastMatch;

    public string Id => definition.Id;
    public CrashRulePriority Priority => definition.Priority;
    public CrashRuleBehavior Behavior => definition.Behavior;

    public bool IsMatch(CrashRuleContext context)
    {
        _lastMatch = definition.Pattern.Match(context.GetSection(definition.Section).Text);
        return _lastMatch.Success;
    }

    public CrashFinding CreateFinding(CrashRuleContext context)
    {
        var match = _lastMatch ?? definition.Pattern.Match(context.GetSection(definition.Section).Text);
        var parameters = (
            from mapping in definition.Parameters
            let @group = match.Groups[mapping.GroupName]
            where @group.Success
            let value = @group.Value.Trim()
            where !string.IsNullOrWhiteSpace(value)
            select new CrashFindingParameter(mapping.ParameterName, value)
        ).ToList();

        return new CrashFinding
        {
            RuleId = Id,
            Reason = definition.Reason,
            Confidence = definition.Confidence,
            Parameters = parameters,
            Evidence =
            [
                new CrashFindingEvidence { Source = definition.Section.ToLogKind(), MatchedText = match.Value }
            ]
        };
    }
}

internal abstract class CrashRuleBase : ICrashRule
{
    public abstract string Id { get; }
    public abstract CrashRulePriority Priority { get; }
    public virtual CrashRuleBehavior Behavior => CrashRuleBehavior.StopPriority;
    public abstract bool IsMatch(CrashRuleContext context);
    public abstract CrashFinding CreateFinding(CrashRuleContext context);

    protected static CrashFinding Finding(
        string ruleId,
        CrashReasonCode reason,
        IEnumerable<CrashFindingParameter>? parameters = null,
        CrashFindingConfidence confidence = CrashFindingConfidence.High)
    {
        return new CrashFinding
        {
            RuleId = ruleId,
            Reason = reason,
            Confidence = confidence,
            Parameters = parameters
                ?.Where(static parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .ToList() ?? []
        };
    }
}

internal static class CrashRuleDefinitionExtensions
{
    public static CrashLogKind ToLogKind(this CrashLogSection section)
    {
        return section switch
        {
            CrashLogSection.Game => CrashLogKind.GameLog,
            CrashLogSection.Debug => CrashLogKind.DebugLog,
            CrashLogSection.CrashReport => CrashLogKind.CrashReport,
            CrashLogSection.JavaError => CrashLogKind.JavaErrorLog,
            _ => CrashLogKind.Unknown
        };
    }
}

internal static class Rules
{
    public static ICrashRule Text(
        string id,
        CrashReasonCode reason,
        CrashRulePriority priority,
        CrashLogSection section,
        string[]? any = null,
        string[]? all = null,
        CrashRuleBehavior behavior = CrashRuleBehavior.StopPriority,
        CrashFindingConfidence confidence = CrashFindingConfidence.High)
    {
        return new TextCrashRule(new TextCrashRuleDefinition
        {
            Id = id,
            Reason = reason,
            Priority = priority,
            Section = section,
            ContainsAny = any ?? [],
            ContainsAll = all ?? [],
            Behavior = behavior,
            Confidence = confidence
        });
    }

    public static ICrashRule Regex(
        string id,
        CrashReasonCode reason,
        CrashRulePriority priority,
        CrashLogSection section,
        Regex regex,
        CrashParameterMapping[]? parameters = null,
        CrashRuleBehavior behavior = CrashRuleBehavior.StopPriority,
        CrashFindingConfidence confidence = CrashFindingConfidence.High)
    {
        return new RegexCrashRule(new RegexCrashRuleDefinition
        {
            Id = id,
            Reason = reason,
            Priority = priority,
            Section = section,
            Pattern = regex,
            Parameters = parameters ?? [],
            Behavior = behavior,
            Confidence = confidence
        });
    }

    public static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None)
    {
        return new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
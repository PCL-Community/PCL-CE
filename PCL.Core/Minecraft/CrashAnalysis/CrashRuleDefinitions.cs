using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>崩溃规则的最小接口。</p>
///     <p>
///         规则只负责判断日志是否命中并产出结构化 <see cref="CrashFinding" />。
///         不允许在规则里拼接用户可见文本，也不允许执行 UI 操作。
///     </p>
/// </summary>
internal interface ICrashRule
{
    string Id { get; }
    CrashRulePriority Priority { get; }
    CrashRuleBehavior Behavior { get; }

    bool IsMatch(CrashRuleContext context);
    CrashFinding CreateFinding(CrashRuleContext context);
}

/// <summary>
///     <p>规则执行优先级。数值越小越先执行。</p>
///     <p>
///         高优先级规则用于确定性强的问题，例如 Java 版本、内存、显卡驱动。
///         低优先级规则用于补充猜测，例如世界实体、方块或堆栈关键词。
///     </p>
/// </summary>
internal enum CrashRulePriority
{
    Critical = 0,
    High = 100,
    Medium = 200,
    StackTrace = 300,
    Low = 400
}

/// <summary>
///     规则命中后对后续规则执行的影响。
/// </summary>
internal enum CrashRuleBehavior
{
    Continue,
    StopPriority,
    StopAll
}

/// <summary>
///     声明式文本规则定义。适用于“大量 pattern → 一个原因”的简单规则。
/// </summary>
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

/// <summary>
///     声明式正则规则定义。适用于需要从日志中提取 Mod 名称、方块 ID、详细错误等参数的规则。
/// </summary>
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

/// <summary>
///     文本规则的执行实现。
/// </summary>
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

/// <summary>
///     <p>正则规则的执行实现。</p>
///     <p>Regex 实例必须带有 timeout，推荐通过 <see cref="Rules.Pattern" /> 创建，避免异常日志导致灾难性回溯。</p>
/// </summary>
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

/// <summary>
///     <p>少量复杂规则的基类。</p>
///     <p>
///         能用 <see cref="TextCrashRule" /> 或 <see cref="RegexCrashRule" /> 表达的规则不要继承此类；
///         只有跨多段日志、需要多步解析或需要构造多个参数时才新增复杂规则。
///     </p>
/// </summary>
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

/// <summary>
///     规则声明辅助工厂，保持新增规则时的写法简洁一致。
/// </summary>
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

    /// <summary>
    ///     创建带超时的正则表达式。所有用于崩溃日志的正则都应通过该方法创建。
    /// </summary>
    public static Regex Pattern(string pattern, RegexOptions options = RegexOptions.None)
    {
        return new Regex(pattern, options | RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
    }
}
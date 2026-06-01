using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>一次崩溃分析的最终结构化报告。</p>
///     <p>
///         报告中的 <see cref="Findings" /> 只描述崩溃原因，<see cref="Actions" /> 描述 UI 可执行的动作。
///         两者都不包含最终本地化文案；用户可见文本由 <see cref="CrashResultLocalizer" /> 生成。
///     </p>
/// </summary>
public sealed record CrashAnalysisReport
{
    public required CrashAnalysisRequest Request { get; init; }
    public required PreparedCrashLogs Logs { get; init; }

    public IReadOnlyList<CrashFinding> Findings { get; init; } = [];
    public IReadOnlyList<CrashSuggestedAction> Actions { get; init; } = [];

    public bool HasFindings => Findings.Count > 0;

    /// <summary>
    ///     从准备后的日志和规则命中结果生成最终报告，并集中创建建议动作。
    /// </summary>
    public static CrashAnalysisReport Create(
        CrashAnalysisRequest request,
        PreparedCrashLogs logs,
        IReadOnlyList<CrashFinding> findings)
    {
        return new CrashAnalysisReport
        {
            Request = request,
            Logs = logs,
            Findings = findings,
            Actions = CrashSuggestedAction.Create(findings, logs, request)
        };
    }
}

/// <summary>
///     <p>建议 UI 层提供给用户的后续动作。</p>
///     <p>
///         动作是结构化的，UI 层必须根据 <see cref="Kind" /> 判断按钮行为，
///         不能再通过分析文案的开头或内容判断是否“前往修改”等操作。
///     </p>
/// </summary>
public sealed record CrashSuggestedAction
{
    public required CrashSuggestedActionKind Kind { get; init; }
    public string? TargetPath { get; init; }

    /// <summary>
    ///     根据分析结果生成推荐动作。该方法只返回动作意图，不执行任何 UI 操作。
    /// </summary>
    public static IReadOnlyList<CrashSuggestedAction> Create(
        IReadOnlyList<CrashFinding> findings,
        PreparedCrashLogs logs,
        CrashAnalysisRequest request)
    {
        var actions = new List<CrashSuggestedAction>();

        if (!string.IsNullOrWhiteSpace(logs.PreferredOpenFile?.FullPath))
            actions.Add(new CrashSuggestedAction
            {
                Kind = CrashSuggestedActionKind.ViewLog,
                TargetPath = logs.PreferredOpenFile.FullPath
            });

        if (findings.Any(static finding =>
                finding.Reason is CrashReasonCode.IncompatibleMods
                    or CrashReasonCode.MissingDependencyOrWrongMinecraftVersion &&
                finding.Parameters.Any(static parameter =>
                    parameter.Name == CrashFindingParameterNames.RequiresModLoaderChange &&
                    bool.TryParse(parameter.Value, out var value) && value)))
            actions.Add(new CrashSuggestedAction
            {
                Kind = CrashSuggestedActionKind.OpenInstanceModifyPage
            });

        if (request.Mode == CrashAnalysisMode.Automatic)
            actions.Add(new CrashSuggestedAction
            {
                Kind = CrashSuggestedActionKind.ExportReport
            });

        return actions;
    }
}

/// <summary>
///     UI 层可以执行的崩溃处理动作类型。
/// </summary>
public enum CrashSuggestedActionKind
{
    ViewLog,
    ExportReport,
    OpenInstanceModifyPage,
    OpenJavaSettings,
    OpenMemorySettings
}
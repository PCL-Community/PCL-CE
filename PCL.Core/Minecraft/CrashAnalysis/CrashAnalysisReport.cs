using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashAnalysisReport
{
    public required CrashAnalysisRequest Request { get; init; }
    public required PreparedCrashLogs Logs { get; init; }

    public IReadOnlyList<CrashFinding> Findings { get; init; } = [];
    public IReadOnlyList<CrashSuggestedAction> Actions { get; init; } = [];

    public bool HasFindings => Findings.Count > 0;

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

public sealed record CrashSuggestedAction
{
    public required CrashSuggestedActionKind Kind { get; init; }
    public string? TargetPath { get; init; }

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

public enum CrashSuggestedActionKind
{
    ViewLog,
    ExportReport,
    OpenInstanceModifyPage,
    OpenJavaSettings,
    OpenMemorySettings
}
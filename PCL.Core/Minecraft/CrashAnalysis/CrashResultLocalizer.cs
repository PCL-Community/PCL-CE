using System;
using System.Collections.Generic;
using System.Linq;
using PCL.Core.App.Localization;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>将结构化崩溃分析结果转换为用户可见文本。</p>
///     <p>
///         这是 Core 中唯一允许选择用户展示文案 key 的位置。它只负责选择 i18n key 和参数，
///         不能把完整中文分析段落写在 C# 中。新增 CrashReasonCode 时请同步补齐语言文件和测试。
///     </p>
/// </summary>
public sealed class CrashResultLocalizer
{
    /// <summary>
    ///     根据当前语言生成崩溃分析弹窗正文。
    /// </summary>
    public static string Localize(CrashAnalysisReport report, CrashResultLocalizeOptions options)
    {
        if (!report.Logs.HasAnalyzableContent ||
            report.Findings.Any(static finding => finding.Reason == CrashReasonCode.NoAnalyzableLog))
            return Lang.Text("Crash.Result.NoAnalyzableLog");

        if (!report.HasFindings)
            return Lang.Text(options.Mode == CrashAnalysisMode.Manual
                ? "Crash.Result.Unknown.Manual"
                : "Crash.Result.Unknown.Automatic");

        var messages = report.Findings
            .Select(_LocalizeFinding)
            .Where(static message => !string.IsNullOrWhiteSpace(message));
        var text = string.Join(Lang.Text("Crash.Result.Joiner"), messages);

        if (options.Mode == CrashAnalysisMode.Automatic) text += _BuildFooter(options);

        return CrashTextUtils.NormalizeNewLines(text).Trim();
    }

    private static string _LocalizeFinding(CrashFinding finding)
    {
        var key = _SelectKey(finding);
        var args = _SelectArguments(finding)
            .Select(static object? (value) => value)
            .ToArray();

        return Lang.Text(key, args);
    }

    private static string _SelectKey(CrashFinding finding)
    {
        var hasDetail = !string.IsNullOrWhiteSpace(finding.GetParameter(CrashFindingParameterNames.Detail));
        var modNames = finding.GetParameter(CrashFindingParameterNames.ModNames)
                       ?? finding.GetParameter(CrashFindingParameterNames.ModName);
        var hasMultipleMods = modNames?.Contains('\n', StringComparison.Ordinal) == true;

        return finding.Reason switch
        {
            CrashReasonCode.ConfirmedModCrash when hasMultipleMods =>
                "Crash.Finding.ConfirmedModCrash.Multiple",
            CrashReasonCode.ConfirmedModCrash when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.ConfirmedModCrash.Single",
            CrashReasonCode.SuspectedModCrash when hasMultipleMods =>
                "Crash.Finding.SuspectedModCrash.Multiple",
            CrashReasonCode.SuspectedModCrash when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.SuspectedModCrash.Single",
            CrashReasonCode.ModMixinFailed when hasMultipleMods =>
                "Crash.Finding.ModMixinFailed.Multiple",
            CrashReasonCode.ModMixinFailed when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.ModMixinFailed.Single",
            CrashReasonCode.DuplicateModInstalled when hasMultipleMods =>
                "Crash.Finding.DuplicateModInstalled.Multiple",
            CrashReasonCode.DuplicateModInstalled when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.DuplicateModInstalled.Single",
            CrashReasonCode.FabricError when hasDetail =>
                "Crash.Finding.FabricError.WithDetail",
            CrashReasonCode.FabricProvidedSolution when hasDetail =>
                "Crash.Finding.FabricProvidedSolution.WithDetail",
            CrashReasonCode.ForgeError when hasDetail =>
                "Crash.Finding.ForgeError.WithDetail",
            CrashReasonCode.ModLoaderError when hasDetail =>
                "Crash.Finding.ModLoaderError.WithDetail",
            CrashReasonCode.IncompatibleMods when hasDetail =>
                "Crash.Finding.IncompatibleMods.WithDetail",
            CrashReasonCode.MissingDependencyOrWrongMinecraftVersion when hasDetail =>
                "Crash.Finding.MissingDependencyOrWrongMinecraftVersion.WithDetail",
            CrashReasonCode.ModConfigCrash when hasDetail =>
                "Crash.Finding.ModConfigCrash.WithDetail",
            CrashReasonCode.ModInitializationFailed when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.ModInitializationFailed.Single",
            CrashReasonCode.StackTraceModName when hasMultipleMods =>
                "Crash.Finding.StackTraceModName.Multiple",
            CrashReasonCode.StackTraceModName when !string.IsNullOrWhiteSpace(modNames) =>
                "Crash.Finding.StackTraceModName.Single",
            CrashReasonCode.StackTraceKeyword =>
                "Crash.Finding.StackTraceKeyword.WithDetail",
            CrashReasonCode.SpecificBlockCrash =>
                "Crash.Finding.SpecificBlockCrash.Single",
            CrashReasonCode.SpecificEntityCrash =>
                "Crash.Finding.SpecificEntityCrash.Single",
            CrashReasonCode.VeryShortProgramOutput when hasDetail =>
                "Crash.Finding.VeryShortProgramOutput.WithDetail",
            CrashReasonCode.FileIntegrityFailed when !string.IsNullOrWhiteSpace(
                    finding.GetParameter(CrashFindingParameterNames.FileName)) =>
                "Crash.Finding.FileIntegrityFailed.WithDetail",
            _ => $"Crash.Finding.{finding.Reason}"
        };
    }

    private static IReadOnlyList<string> _SelectArguments(CrashFinding finding)
    {
        var detail = finding.GetParameter(CrashFindingParameterNames.Detail);
        var modNames = finding.GetParameter(CrashFindingParameterNames.ModNames)
                       ?? finding.GetParameter(CrashFindingParameterNames.ModName);

        return finding.Reason switch
        {
            CrashReasonCode.ConfirmedModCrash
                or CrashReasonCode.SuspectedModCrash
                or CrashReasonCode.ModMixinFailed
                or CrashReasonCode.DuplicateModInstalled
                or CrashReasonCode.StackTraceModName =>
                Single(_FormatListArgument(modNames ?? "")),

            CrashReasonCode.StackTraceKeyword =>
                Single(finding.GetParameter(CrashFindingParameterNames.Keywords)),

            CrashReasonCode.SpecificBlockCrash =>
                Single(finding.GetParameter(CrashFindingParameterNames.BlockName)),

            CrashReasonCode.SpecificEntityCrash =>
                Single(finding.GetParameter(CrashFindingParameterNames.EntityName)),

            CrashReasonCode.FileIntegrityFailed =>
                Single(finding.GetParameter(CrashFindingParameterNames.FileName)),

            _ when !string.IsNullOrWhiteSpace(detail) =>
                Single(detail),

            _ => []
        };

        IReadOnlyList<string> Single(string? value)
        {
            return [value ?? ""];
        }
    }

    private static string _FormatListArgument(string value)
    {
        return string.Join("\n - ", CrashTextUtils.ReadLinesNormalized(value)
            .Select(static line => line.Trim(' ', '-', '\t'))
            .Where(static line => !string.IsNullOrWhiteSpace(line)));
    }

    private static string _BuildFooter(CrashResultLocalizeOptions options)
    {
        var footer = Lang.Text("Crash.Result.Footer.RequestReport");
        if (!options.IsLauncherLatest) footer += Lang.Text("Crash.Result.Footer.LauncherOutdated");

        return footer;
    }
}

/// <summary>
///     本地化崩溃结果时需要的展示上下文。
/// </summary>
public sealed record CrashResultLocalizeOptions
{
    public CrashAnalysisMode Mode { get; init; }
    public bool IsLauncherLatest { get; init; } = true;
}
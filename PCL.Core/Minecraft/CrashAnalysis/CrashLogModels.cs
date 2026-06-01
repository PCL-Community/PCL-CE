using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashLogFile
{
    public required string DisplayName { get; init; }
    public string? FullPath { get; init; }
    public CrashLogOrigin Origin { get; init; }
    public CrashLogKind Kind { get; init; } = CrashLogKind.Unknown;
    public DateTimeOffset? LastWriteTime { get; init; }
    public long? Length { get; init; }
    public required string Content { get; init; }
}

public enum CrashLogOrigin
{
    FileSystem,
    ImportedArchive,
    ImportedFile,
    CapturedOutput,
    Generated
}

public enum CrashLogKind
{
    Unknown,
    JavaErrorLog,
    GameLog,
    DebugLog,
    CrashReport,
    CapturedGameOutput,
    LauncherLog,
    ExtraLog,
    ExtraReport
}

public sealed record PreparedCrashLogs
{
    public CrashLogFile? GameLog { get; init; }
    public CrashLogFile? DebugLog { get; init; }
    public CrashLogFile? CrashReport { get; init; }
    public CrashLogFile? JavaErrorLog { get; init; }

    public CrashTextSection GameText { get; init; } = CrashTextSection.Empty;
    public CrashTextSection DebugText { get; init; } = CrashTextSection.Empty;
    public CrashTextSection CrashReportText { get; init; } = CrashTextSection.Empty;
    public CrashTextSection JavaErrorText { get; init; } = CrashTextSection.Empty;

    public IReadOnlyList<CrashLogFile> ReportSourceFiles { get; init; } = [];
    public CrashLogFile? PreferredOpenFile { get; init; }

    public bool HasAnalyzableContent =>
        !GameText.IsEmpty ||
        !DebugText.IsEmpty ||
        !CrashReportText.IsEmpty ||
        !JavaErrorText.IsEmpty;
}

public sealed class CrashTextSection(string text)
{
    public static CrashTextSection Empty { get; } = new("");

    public string Text { get; } = CrashTextUtils.NormalizeNewLines(text ?? "");
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
    public string LowerInvariant => field ??= Text.ToLowerInvariant();

    public bool Contains(string value)
    {
        return Text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class CrashRuleContext(PreparedCrashLogs logs, CrashAnalysisRequest request)
{
    public PreparedCrashLogs Logs { get; } = logs;
    public CrashAnalysisRequest Request { get; } = request;

    public CrashTextSection Game => Logs.GameText;
    public CrashTextSection Debug => Logs.DebugText;
    public CrashTextSection CrashReport => Logs.CrashReportText;
    public CrashTextSection JavaError => Logs.JavaErrorText;

    public CrashTextSection Combined => field ??= new CrashTextSection(string.Concat(
        Game.Text,
        "\n",
        Debug.Text,
        "\n",
        CrashReport.Text,
        "\n",
        JavaError.Text));

    public bool IsModdedGame =>
        Combined.Contains("net.minecraftforge") ||
        Combined.Contains("fabric-loader") ||
        Combined.Contains("quilt-loader") ||
        Combined.Contains("liteloader") ||
        Combined.Contains("ModLauncher") ||
        Combined.Contains("-- MOD ") ||
        Combined.Contains("Fabric Mods") ||
        Combined.Contains("Forge Mod Loader");

    public CrashTextSection GetSection(CrashLogSection section)
    {
        return section switch
        {
            CrashLogSection.Game => Game,
            CrashLogSection.Debug => Debug,
            CrashLogSection.CrashReport => CrashReport,
            CrashLogSection.JavaError => JavaError,
            CrashLogSection.Combined => Combined,
            _ => Combined
        };
    }
}

public enum CrashLogSection
{
    Combined,
    Game,
    Debug,
    CrashReport,
    JavaError
}
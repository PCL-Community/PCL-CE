using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>Core 层统一处理的日志文件模型。</p>
///     <p>
///         日志可以来自文件系统、压缩包、PCL 捕获的虚拟输出或后续生成内容。
///         规则系统不关心日志来源，只读取 <see cref="PreparedCrashLogs" /> 中准备好的文本段。
///     </p>
/// </summary>
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

/// <summary>
///     日志来源，用于报告导出和调试，不参与用户文案生成。
/// </summary>
public enum CrashLogOrigin
{
    FileSystem,
    ImportedArchive,
    ImportedFile,
    CapturedOutput,
    Generated
}

/// <summary>
///     日志类型，由 <see cref="CrashLogPreparer.Classify" /> 根据文件名和兼容规则识别。
/// </summary>
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

/// <summary>
///     <p>经过分类、选择和截断后的日志集合。</p>
///     <p>
///         这是规则系统唯一应该读取的日志入口。它保留了原始文件引用用于导出，
///         同时提供适合匹配的 <see cref="CrashTextSection" />，避免规则直接读取磁盘或重复处理巨型日志。
///     </p>
/// </summary>
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

/// <summary>
///     <p>面向规则匹配的文本段，负责统一换行并缓存常用派生文本。</p>
///     <p>
///         不要在规则中自行构造大字符串或反复 <c>ToLowerInvariant()</c>。
///         如果必须跨日志搜索，请使用 <see cref="CrashRuleContext.Combined" />，它会延迟构造并缓存。
///     </p>
/// </summary>
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

/// <summary>
///     <p> 单次规则执行的只读上下文。</p>
///     <p>
///         它把准备后的日志按 section 暴露给规则，并提供延迟构造的 Combined 文本和 Mod 环境判断。
///         规则不应该越过该上下文访问文件系统或 UI 状态。
///     </p>
/// </summary>
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

    /// <summary>
    ///     根据规则声明的日志区域返回对应文本段。
    /// </summary>
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

/// <summary>
///     规则匹配时可以选择的日志区域。
/// </summary>
public enum CrashLogSection
{
    Combined,
    Game,
    Debug,
    CrashReport,
    JavaError
}
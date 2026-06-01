using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>一次崩溃分析的输入上下文。</p>
///     <p>
///         该模型只保存 Core 层可理解的数据，不直接保存 WPF 控件、页面对象或启动器实例对象。
///         UI 层应在调用 <see cref="CrashAnalyzer" /> 前把自身状态转换为这个 DTO。
///     </p>
/// </summary>
public sealed record CrashAnalysisRequest
{
    public CrashAnalysisSource Source { get; init; }
    public CrashAnalysisMode Mode { get; init; }

    public string? VersionPath { get; init; }
    public string? MinecraftRootPath { get; init; }
    public string? ImportedFilePath { get; init; }

    public string TempDirectory { get; init; } = "";
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    public IReadOnlyList<string> LatestOutputLines { get; init; } = [];
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];

    public string? LatestLaunchScript { get; init; }
    public CrashEnvironmentInfo? EnvironmentInfo { get; init; }
}

/// <summary>
///     指示崩溃日志从哪里来。实时游戏崩溃和用户手动导入走不同的读取路径，
///     但后续准备、规则和展示流程保持一致。
/// </summary>
public enum CrashAnalysisSource
{
    LiveGame,
    ImportedFile
}

/// <summary>
///     指示分析结果面向自动崩溃弹窗还是手动导入分析。
///     该值只影响展示和导出动作，不影响规则判断本身。
/// </summary>
public enum CrashAnalysisMode
{
    Automatic,
    Manual
}
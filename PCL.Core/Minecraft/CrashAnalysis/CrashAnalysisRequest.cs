using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.CrashAnalysis;

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

public enum CrashAnalysisSource
{
    LiveGame,
    ImportedFile
}

public enum CrashAnalysisMode
{
    Automatic,
    Manual
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashLogPreparer
{
    private const int GameLogHeadLines = 1500;
    private const int GameLogTailLines = 500;
    private const int CrashReportHeadLines = 300;
    private const int CrashReportTailLines = 700;
    private const int JavaErrorHeadLines = 200;
    private const int JavaErrorTailLines = 100;
    private const int DebugLogHeadLines = 1000;
    private const int CapturedOutputTailLines = 500;

    public PreparedCrashLogs Prepare(IReadOnlyList<CrashLogFile> rawLogs, CrashAnalysisRequest request)
    {
        var classified = rawLogs
            .Select(file => file with { Kind = Classify(file) })
            .ToList();

        classified
            .AddRange(request.ExtraReportFiles
            .Select(extraFile => CrashLogCollector.TryReadFile(extraFile, CrashLogOrigin.FileSystem))
            .OfType<CrashLogFile>()
            .Select(extra => extra with { Kind = Classify(extra) }));

        var capturedOutput = _Newest(classified, CrashLogKind.CapturedGameOutput);
        var latestLog = _ByName(classified, "latest.log") ?? _ByName(classified, "latest log.txt");
        var debugLog = _Newest(classified, CrashLogKind.DebugLog);
        var extraLog = _Newest(classified, CrashLogKind.ExtraLog);
        var gameLog = capturedOutput ?? latestLog ?? debugLog ?? extraLog;
        var crashReport = _Newest(classified, CrashLogKind.CrashReport);
        var javaError = _Newest(classified, CrashLogKind.JavaErrorLog);

        return new PreparedCrashLogs
        {
            GameLog = gameLog,
            DebugLog = debugLog,
            CrashReport = crashReport,
            JavaErrorLog = javaError,
            GameText = _CreateGameText(gameLog),
            DebugText = debugLog is null
                ? CrashTextSection.Empty
                : new CrashTextSection(CrashTextUtils.HeadTailDistinct(debugLog.Content, DebugLogHeadLines, 0)),
            CrashReportText = crashReport is null
                ? CrashTextSection.Empty
                : new CrashTextSection(CrashTextUtils.HeadTailDistinct(crashReport.Content, CrashReportHeadLines,
                    CrashReportTailLines)),
            JavaErrorText = javaError is null
                ? CrashTextSection.Empty
                : new CrashTextSection(CrashTextUtils.HeadTailDistinct(javaError.Content, JavaErrorHeadLines,
                    JavaErrorTailLines)),
            ReportSourceFiles = classified,
            PreferredOpenFile = crashReport ?? gameLog ?? javaError ?? debugLog
        };
    }

    public static CrashLogKind Classify(CrashLogFile file)
    {
        var fileName = Path.GetFileName(file.DisplayName).ToLowerInvariant();
        var extension = Path.GetExtension(fileName);

        return fileName switch
        {
            _ when fileName.StartsWith("hs_err") => CrashLogKind.JavaErrorLog,
            _ when fileName.StartsWith("crash-") => CrashLogKind.CrashReport,

            "latest.log" or "latest log.txt" => CrashLogKind.GameLog,
            "debug.log" or "debug log.txt" => CrashLogKind.DebugLog,
            "rawoutput.log" or "游戏崩溃前的输出.txt" => CrashLogKind.CapturedGameOutput,

            "启动器日志.txt"
                or "pcl2 启动器日志.txt"
                or "pcl 启动器日志.txt"
                or "log1.txt"
                or "log-ce1.log" => CrashLogKind.LauncherLog,

            _ when extension == ".log" => CrashLogKind.ExtraLog,
            _ when extension == ".txt" => CrashLogKind.ExtraReport,

            _ => CrashLogKind.Unknown
        };
    }

    private static CrashTextSection _CreateGameText(CrashLogFile? gameLog)
    {
        if (gameLog is null) return CrashTextSection.Empty;

        var content = gameLog.Content;
        if (gameLog.Kind == CrashLogKind.CapturedGameOutput)
        {
            const string marker = "以下为游戏输出的最后一段内容";
            return content.Contains(marker, StringComparison.OrdinalIgnoreCase)
                ? new CrashTextSection(CrashTextUtils.AfterFirst(content, marker))
                : new CrashTextSection(CrashTextUtils.HeadTailDistinct(content, 0, CapturedOutputTailLines));
        }

        return new CrashTextSection(CrashTextUtils.HeadTailDistinct(content, GameLogHeadLines, GameLogTailLines));
    }

    private static CrashLogFile? _Newest(IEnumerable<CrashLogFile> files, CrashLogKind kind)
    {
        return files
            .Where(file => file.Kind == kind)
            .OrderByDescending(file => file.LastWriteTime ?? DateTimeOffset.MinValue)
            .ThenBy(file => file.DisplayName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static CrashLogFile? _ByName(IEnumerable<CrashLogFile> files, string fileName)
    {
        return files
            .Where(file =>
                string.Equals(Path.GetFileName(file.DisplayName), fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(file => file.LastWriteTime ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }
}
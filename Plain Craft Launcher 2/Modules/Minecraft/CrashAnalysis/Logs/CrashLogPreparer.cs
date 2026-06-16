using System.IO;
using System.Text;
using PCL.Core.Logging;

namespace PCL;

internal sealed class CrashLogPreparer(CrashAnalysisContext context)
{
    private static readonly HashSet<string> KnownGameLogNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "latest.log",
        "latest log.txt",
        "游戏崩溃前的输出.txt",
        "rawoutput.log"
    };

    private static readonly HashSet<string> KnownDebugLogNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "debug.log",
        "debug log.txt"
    };

    private static readonly HashSet<string> KnownLauncherLogNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "启动器日志.txt",
        "PCL2 启动器日志.txt",
        "PCL 启动器日志.txt",
        "log1.txt",
        "log-ce1.log"
    };

    public bool Prepare()
    {
        LogWrapper.Info("Crash", "步骤 2：准备日志文本");

        context.DirectOpenFile = null;
        context.PreparedLogs = null;
        context.OutputFiles.Clear();

        var classifiedFiles = _ClassifyFiles();

        var analyzable = classifiedFiles
            .Where(item => item.Kind is not null)
            .ToList();

        var extraFiles = classifiedFiles
            .Where(item => item.Kind is null)
            .Select(item => item.File)
            .ToList();

        if (analyzable.Count == 0 && extraFiles.Count > 0)
        {
            LogWrapper.Info("Crash", "由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析");

            analyzable = extraFiles
                .Select(file => new ClassifiedCrashLog(file, CrashLogKind.Game))
                .ToList();

            extraFiles.Clear();
        }

        var game = _SelectGameLog(analyzable);
        var debug = _SelectDebugLog(analyzable);
        var crashReport = _SelectNewest(analyzable, CrashLogKind.CrashReport, 300, 700);
        var hsErr = _SelectNewest(analyzable, CrashLogKind.HsErr, 200, 100);

        foreach (var extraFile in extraFiles)
        {
            context.OutputFiles.Add(extraFile.FullPath);
            LogWrapper.Info("Crash", $"输出报告：{extraFile.FullPath}，不用作分析");
        }

        var logs = new CrashLogSet
        {
            Game = game,
            Debug = debug,
            CrashReport = crashReport,
            HsErr = hsErr,
            All = string.Concat(
                game?.Text ?? debug?.Text ?? string.Empty,
                hsErr?.Text ?? string.Empty,
                crashReport?.Text ?? string.Empty)
        };

        context.PreparedLogs = logs;
        context.LogAll = logs.All;

        if (logs.HasAnalyzableLog)
            LogWrapper.Info(
                "Crash",
                ("步骤 2：准备日志文本完成，找到" +
                 (game is null ? "" : "游戏日志、") +
                 (debug is null ? "" : "游戏 Debug 日志、") +
                 (hsErr is null ? "" : "虚拟机日志、") +
                 (crashReport is null ? "" : "崩溃日志、"))
                .TrimEnd('、') + "用作分析");
        else
            LogWrapper.Info("Crash", "步骤 2：准备日志文本完成，没有任何可供分析的日志");

        return logs.HasAnalyzableLog;
    }

    private List<ClassifiedCrashLog> _ClassifyFiles()
    {
        var result = new List<ClassifiedCrashLog>();

        foreach (var file in context.RawFiles)
        {
            var name = file.FileName.ToLowerInvariant();

            if (!file.Lines.Any())
            {
                LogWrapper.Info("Crash", $"{name} 由于内容为空跳过");
                continue;
            }

            var kind = _ClassifyLogKind(file);

            if (kind is null && !_IsExtraLogLike(file))
            {
                LogWrapper.Info("Crash", $"{name} 分类为 Ignore");
                continue;
            }

            if (kind is not null && context.DirectOpenFile is null)
                context.DirectOpenFile = file;

            result.Add(new ClassifiedCrashLog(file, kind));
            LogWrapper.Info("Crash", $"{name} 分类为 {kind?.ToString() ?? "Extra"}");
        }

        return result;
    }

    private static CrashLogKind? _ClassifyLogKind(CrashLogEntry file)
    {
        var name = file.FileName.ToLowerInvariant();

        if (name.StartsWith("hs_err", StringComparison.Ordinal))
            return CrashLogKind.HsErr;

        if (name.StartsWith("crash-", StringComparison.Ordinal))
            return CrashLogKind.CrashReport;

        if (KnownDebugLogNames.Contains(name))
            return CrashLogKind.Debug;

        if (KnownGameLogNames.Contains(name))
            return CrashLogKind.Game;

        if (KnownLauncherLogNames.Contains(name) &&
            file.Lines.Any(line => line.Contains("以下为游戏输出的最后一段内容")))
            return CrashLogKind.Game;

        return null;
    }

    private static bool _IsExtraLogLike(CrashLogEntry file)
    {
        var name = file.FileName.ToLowerInvariant();

        return name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private CrashLogText? _SelectGameLog(IReadOnlyList<ClassifiedCrashLog> files)
    {
        var gameFiles = files
            .Where(item => item.Kind is CrashLogKind.Game or CrashLogKind.Debug)
            .Select(item => item.File)
            .ToList();

        if (gameFiles.Count == 0)
            return null;

        var byName = gameFiles
            .GroupBy(file => file.FileName.ToLowerInvariant())
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(_GetLastWriteTime).Last(),
                StringComparer.OrdinalIgnoreCase);

        var text = "";

        foreach (var name in new[]
                 {
                     "rawoutput.log",
                     "启动器日志.txt",
                     "log1.txt",
                     "log-ce1.log",
                     "游戏崩溃前的输出.txt",
                     "PCL2 启动器日志.txt",
                     "PCL 启动器日志.txt"
                 })
        {
            if (!byName.TryGetValue(name, out var currentLog))
                continue;

            text += _ExtractLauncherGameOutput(currentLog);

            context.OutputFiles.Add(currentLog.FullPath);
            LogWrapper.Info("Crash", $"导入分析：{currentLog.FullPath}，作为启动器日志");

            break;
        }

        foreach (var name in new[]
                 {
                     "latest.log",
                     "latest log.txt",
                     "debug.log",
                     "debug log.txt"
                 })
        {
            if (!byName.TryGetValue(name, out var currentLog))
                continue;

            text += _GetHeadTailLines(currentLog.Lines, 1500, 500);

            context.OutputFiles.Add(currentLog.FullPath);
            LogWrapper.Info("Crash", $"导入分析：{currentLog.FullPath}，作为 Minecraft 日志");

            break;
        }

        if (string.IsNullOrEmpty(text))
        {
            var fallback = gameFiles
                .OrderBy(_GetLastWriteTime)
                .Last();

            text = _GetHeadTailLines(fallback.Lines, 1500, 500);

            context.OutputFiles.Add(fallback.FullPath);
            LogWrapper.Info("Crash", $"导入分析：{fallback.FullPath}，作为兜底日志");
        }

        foreach (var file in gameFiles.Where(file => !context.OutputFiles.Contains(file.FullPath)))
        {
            context.OutputFiles.Add(file.FullPath);
            LogWrapper.Info("Crash", $"输出报告：{file.FullPath}，作为 Minecraft 或启动器日志");
        }

        return new CrashLogText
        {
            Kind = CrashLogKind.Game,
            Text = text.TrimEnd('\r', '\n'),
            FilePath = gameFiles.FirstOrDefault()?.FullPath
        };
    }

    private CrashLogText? _SelectDebugLog(IReadOnlyList<ClassifiedCrashLog> files)
    {
        var debugFiles = files
            .Where(item => item.Kind == CrashLogKind.Debug)
            .Select(item => item.File)
            .ToList();

        if (debugFiles.Count == 0)
            return null;

        var selected = debugFiles
            .OrderBy(_GetLastWriteTime)
            .Last();

        if (!context.OutputFiles.Contains(selected.FullPath))
            context.OutputFiles.Add(selected.FullPath);

        LogWrapper.Info("Crash", $"导入分析：{selected.FullPath}，作为 Minecraft Debug 日志");

        return new CrashLogText
        {
            Kind = CrashLogKind.Debug,
            Text = _GetHeadTailLines(selected.Lines, 1000, 0),
            FilePath = selected.FullPath
        };
    }

    private CrashLogText? _SelectNewest(
        IReadOnlyList<ClassifiedCrashLog> files,
        CrashLogKind kind,
        int head,
        int tail)
    {
        var selectedFiles = files
            .Where(item => item.Kind == kind)
            .Select(item => item.File)
            .ToList();

        if (selectedFiles.Count == 0)
            return null;

        var selected = selectedFiles
            .OrderBy(_GetLastWriteTime)
            .Last();

        context.OutputFiles.Add(selected.FullPath);

        LogWrapper.Info(
            "Crash",
            $"输出报告：{selected.FullPath}{(kind == CrashLogKind.HsErr ? "，作为虚拟机错误信息" : "，作为 Minecraft 崩溃报告")}");

        LogWrapper.Info(
            "Crash",
            $"导入分析：{selected.FullPath}{(kind == CrashLogKind.HsErr ? "，作为虚拟机错误信息" : "，作为 Minecraft 崩溃报告")}");

        return new CrashLogText
        {
            Kind = kind,
            Text = _GetHeadTailLines(selected.Lines, head, tail),
            FilePath = selected.FullPath
        };
    }

    private static string _ExtractLauncherGameOutput(CrashLogEntry log)
    {
        var text = "";
        var hasLauncherMark = false;

        foreach (var line in log.Lines)
            if (hasLauncherMark)
            {
                text += line + "\n";
            }
            else if (line.Contains("以下为游戏输出的最后一段内容"))
            {
                hasLauncherMark = true;
                LogWrapper.Info("Crash", "找到 PCL 输出的游戏实时日志头");
            }

        return hasLauncherMark
            ? text.TrimEnd('\r', '\n')
            : _GetHeadTailLines(log.Lines, 0, 500);
    }

    private static DateTime _GetLastWriteTime(CrashLogEntry file)
    {
        try
        {
            return new FileInfo(file.FullPath).LastWriteTime;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Crash", "获取日志文件修改时间失败");
            return new DateTime(1900, 1, 1);
        }
    }

    private static string _GetHeadTailLines(
        IReadOnlyList<string> raw,
        int headLines,
        int tailLines)
    {
        if (raw.Count <= headLines + tailLines)
            return string.Join("\n", raw.Distinct());

        var lines = new List<string>();
        var seen = new HashSet<string>();
        var realHeadLines = 0;

        int viewedLines;

        for (viewedLines = 0; viewedLines <= raw.Count - 1; viewedLines++)
        {
            if (!seen.Add(raw[viewedLines]))
                continue;

            realHeadLines += 1;
            lines.Add(raw[viewedLines]);

            if (realHeadLines >= headLines)
                break;
        }

        var realTailLines = 0;

        for (var i = raw.Count - 1; i >= viewedLines; i -= 1)
        {
            if (!seen.Add(raw[i]))
                continue;

            realTailLines += 1;
            lines.Insert(realHeadLines, raw[i]);

            if (realTailLines >= tailLines)
                break;
        }

        var result = new StringBuilder();

        foreach (var line in lines.Where(line => !string.IsNullOrEmpty(line)))
        {
            result.Append(line);
            result.Append('\n');
        }

        return result.ToString();
    }

    private sealed class ClassifiedCrashLog(
        CrashLogEntry file,
        CrashLogKind? kind)
    {
        public CrashLogEntry File { get; } = file;

        public CrashLogKind? Kind { get; } = kind;
    }
}
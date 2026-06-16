using System.IO;

namespace PCL;

internal sealed class CrashLogCollector(CrashAnalysisContext context)
{
    public void Collect(string versionPathIndie, IList<string>? latestLog)
    {
        ModBase.Log("[Crash] 步骤 1：收集日志文件");

        var possibleLogs = _FindPossibleLogFiles(versionPathIndie)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var recentLogs = possibleLogs.Where(IsRecentNonEmptyFile).ToList();
        if (recentLogs.Count == 0)
            ModBase.Log("[Crash] 未发现可能可用的日志文件");

        foreach (var filePath in recentLogs)
            TryAddLogFile(filePath, "读取可能的崩溃日志文件失败");

        AddCapturedOutput(latestLog);

        ModBase.Log("[Crash] 步骤 1：收集日志文件完成，收集到 " + context.RawFiles.Count + " 个文件");
    }

    private static IEnumerable<string> _FindPossibleLogFiles(string versionPathIndie)
    {
        var possibleLogs = new List<string>();

        try
        {
            var dirInfo = new DirectoryInfo(Path.Combine(versionPathIndie, "crash-reports"));
            if (dirInfo.Exists)
                possibleLogs.AddRange(dirInfo.EnumerateFiles().Select(file => file.FullName));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败");
        }

        try
        {
            var rootDirectory = new DirectoryInfo(versionPathIndie).Parent?.Parent;
            if (rootDirectory is not null && rootDirectory.Exists)
                possibleLogs.AddRange(
                    rootDirectory.EnumerateFiles()
                        .Where(file => file.Extension == ".log")
                        .Select(file => file.FullName));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 主文件夹下的日志失败");
        }

        try
        {
            var instanceDirectory = new DirectoryInfo(versionPathIndie);
            if (instanceDirectory.Exists)
                possibleLogs.AddRange(
                    instanceDirectory.EnumerateFiles()
                        .Where(file => file.Extension == ".log")
                        .Select(file => file.FullName));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 隔离文件夹下的日志失败");
        }

        possibleLogs.Add(Path.Combine(versionPathIndie, "logs", "latest.log"));
        var launchScript = ModBase.ReadFile(Path.Combine(ModBase.exePath, "PCL", "LatestLaunch.bat"));
        if (launchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false"))
            possibleLogs.Add(Path.Combine(versionPathIndie, "logs", "debug.log"));

        return possibleLogs;
    }

    private static bool IsRecentNonEmptyFile(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists || info.Length <= 0L)
                return false;

            var ageMinutes = Math.Abs((info.LastWriteTime - DateTime.Now).TotalMinutes);
            if (ageMinutes >= 3d)
                return false;

            ModBase.Log("[Crash] 可能可用的日志文件：" + filePath + "（" + Math.Round(ageMinutes, 1) + " 分钟）");
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "确认崩溃日志时间失败（" + filePath + "）");
            return false;
        }
    }

    private void TryAddLogFile(string filePath, string errorMessage)
    {
        try
        {
            context.RawFiles.Add(new CrashLogEntry(filePath, ModBase.ReadFile(filePath).Split("\r\n".ToCharArray())));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, errorMessage + "（" + filePath + "）");
        }
    }

    private void AddCapturedOutput(IList<string>? latestLog)
    {
        if (latestLog is null || !latestLog.Any())
            return;

        var rawOutput = latestLog.Join("\r\n");
        ModBase.Log("[Crash] 以下为游戏输出的最后一段内容：" + "\r\n" + rawOutput);
        var rawOutputPath = Path.Combine(context.TempFolder, "RawOutput.log");
        ModBase.WriteFile(rawOutputPath, rawOutput);
        context.RawFiles.Add(new CrashLogEntry(rawOutputPath, latestLog.ToArray()));
        latestLog.Clear();
    }
}
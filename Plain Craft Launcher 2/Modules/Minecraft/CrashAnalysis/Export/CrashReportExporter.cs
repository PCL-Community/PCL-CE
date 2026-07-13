using System.IO;
using System.IO.Compression;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

internal sealed class CrashReportExporter
{
    private const string ReportFolderName = "Report";

    private const string LaunchScriptFileName = "启动脚本.bat";
    private const string RawOutputFileName = "游戏崩溃前的输出.txt";
    private const string LauncherLogFileName = "PCL CE 启动器日志.txt";
    private const string EnvironmentFileName = "环境与启动信息.txt";

    public void Export(
        CrashAnalysisContext context,
        string targetZipPath,
        IEnumerable<string>? extraFiles)
    {
        var targetFolder = Basics.GetParentPathOrEmpty(targetZipPath);
        Directory.CreateDirectory(targetFolder);

        if (File.Exists(targetZipPath))
            File.Delete(targetZipPath);

        ModBase.FeedbackInfo();

        var reportFolder = Path.Combine(context.TempFolder, ReportFolderName);

        if (Directory.Exists(reportFolder))
            CrashFileIo.DeleteDirectory(reportFolder);

        Directory.CreateDirectory(reportFolder);

        try
        {
            foreach (var outputFile in _CollectOutputFiles(context, extraFiles))
                _CopyFileToReport(reportFolder, outputFile);

            _WriteEnvironmentInfo(reportFolder);

            ZipFile.CreateFromDirectory(reportFolder, targetZipPath);
        }
        finally
        {
            CrashFileIo.DeleteDirectory(reportFolder);
        }
    }

    private static void _CopyFileToReport(
        string reportFolder,
        string outputFile)
    {
        if (!File.Exists(outputFile))
            return;

        var fileName = _GetExportFileName(outputFile, out var fileEncoding);

        fileEncoding ??= EncodingDetector.DetectEncoding(CrashFileIo.ReadBytes(outputFile));

        var fileContent = CrashFileIo.ReadText(outputFile, fileEncoding);
        fileContent = _SanitizeFileContent(fileContent, fileName);

        CrashFileIo.WriteText(
            Path.Combine(reportFolder, fileName),
            fileContent,
            fileEncoding);

        LogWrapper.Info("Crash", $"导出文件：{fileName}，编码：{fileEncoding.HeaderName}");
    }

    private static string _GetExportFileName(
        string outputFile,
        out Encoding? fileEncoding)
    {
        fileEncoding = null;

        var fileName = Path.GetFileName(outputFile);

        switch (fileName)
        {
            case "LatestLaunch.bat":
                return LaunchScriptFileName;

            case "RawOutput.log":
                fileEncoding = Encoding.UTF8;
                return RawOutputFileName;
        }

        var currentLogFile = LogWrapper.CurrentLogger.CurrentLogFiles.LastOrDefault();
        var currentLogFileName = currentLogFile is null ? null : CrashText.AfterLast(currentLogFile, @"\");

        if (currentLogFileName != fileName) return fileName;

        fileEncoding = Encoding.UTF8;
        return LauncherLogFileName;
    }

    private static IReadOnlyList<string> _CollectOutputFiles(
        CrashAnalysisContext context,
        IEnumerable<string>? extraFiles)
    {
        if (extraFiles is not null)
            context.OutputFiles.AddRange(extraFiles);

        return context.OutputFiles;
    }

    private static string _SanitizeFileContent(
        string fileContent,
        string fileName)
    {
        var tokenMask = fileName == LaunchScriptFileName ? 'F' : '*';

        fileContent = McLogFilter.FilterAccessToken(fileContent, tokenMask);
        return McLogFilter.FilterUserName(fileContent, '*');
    }

    private static void _WriteEnvironmentInfo(string reportFolder)
    {
        var launcherLog = CrashText.BeforeFirst(
            CrashText.AfterLast(_ReadReportFile(reportFolder, LauncherLogFileName), "[Launch] ~ 基础参数 ~"),
            "开始 Minecraft 日志监控");

        var launchScript = _ReadReportFile(reportFolder, LaunchScriptFileName);

        var envInfo = new StringBuilder();

        _AppendLauncherInfo(envInfo);
        _AppendProfileInfo(envInfo, launcherLog);
        _AppendInstanceInfo(envInfo, launcherLog, launchScript);
        _AppendEnvironmentInfo(envInfo, launcherLog);

        CrashFileIo.WriteText(
            Path.Combine(reportFolder, EnvironmentFileName),
            envInfo.ToString(),
            Encoding.UTF8);
    }

    private static void _AppendLauncherInfo(StringBuilder builder)
    {
        builder.AppendLine(Lang.Text("Crash.Report.Environment.LauncherVersion", Basics.VersionName));
        builder.AppendLine(Lang.Text("Crash.Report.Environment.LauncherId", Identify.LauncherId));
        builder.AppendLine();
    }

    private static void _AppendProfileInfo(
        StringBuilder builder,
        string launcherLog)
    {
        builder.AppendLine(Lang.Text("Crash.Report.Environment.ProfileSection"));
        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.ProfileName",
                _ExtractLauncherValue(launcherLog, "玩家用户名："),
                _ExtractLauncherValue(launcherLog, "验证方式：")));
        builder.AppendLine();
    }

    private static void _AppendInstanceInfo(
        StringBuilder builder,
        string launcherLog,
        string launchScript)
    {
        builder.AppendLine(Lang.Text("Crash.Report.Environment.InstanceSection"));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.SelectedJava",
                _ExtractLauncherValue(launcherLog, "Java 信息：")));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.Log4j2NoLookups",
                !launchScript.Contains("-Dlog4j2.formatMsgNoLookups=false", StringComparison.OrdinalIgnoreCase)));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.MinecraftFolder",
                _ExtractLauncherValue(launcherLog, "MC 文件夹：")));

        builder.AppendLine();
    }

    private static void _AppendEnvironmentInfo(
        StringBuilder builder,
        string launcherLog)
    {
        builder.AppendLine(Lang.Text("Crash.Report.Environment.EnvironmentSection"));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.OperatingSystem",
                SystemInfo.OSInfo,
                !SystemInfo.Is32BitSystem,
                SystemInfo.IsArm64System));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.Cpu",
                HardwareInfo.CPUName));

        builder.AppendLine(
            Lang.Text(
                "Crash.Report.Environment.MemoryAllocation",
                _ExtractLauncherValue(launcherLog, "分配的内存："),
                Lang.Number(HardwareInfo.SystemMemorySize / 1024d, "N2"),
                Lang.Number(HardwareInfo.SystemMemorySize, "N0")));

        for (var i = 0; i < HardwareInfo.GPUs.Count; i++)
        {
            var gpu = HardwareInfo.GPUs[i];

            builder.AppendLine(
                Lang.Text(
                    "Crash.Report.Environment.Gpu",
                    i,
                    gpu.Name,
                    _FormatGpuMemory(gpu.Memory),
                    gpu.DriverVersion));
        }
    }

    private static string _ExtractLauncherValue(
        string launcherLog,
        string key)
    {
        return CrashText.Between(launcherLog, key, "[")
            .TrimEnd('[')
            .Trim();
    }

    private static string _FormatGpuMemory(long memory)
    {
        return memory >= 4095L
            ? ">= " + memory
            : memory.ToString();
    }

    private static string _ReadReportFile(
        string reportFolder,
        string fileName)
    {
        var filePath = Path.Combine(reportFolder, fileName);

        return File.Exists(filePath)
            ? CrashFileIo.ReadText(filePath)
            : "";
    }
}
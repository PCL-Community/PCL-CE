using System.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public sealed class MinecraftCrashController
{
    private readonly CrashAnalyzer _analyzer = new();

    public void AnalyzeImportedCrashReport(string filePath)
    {
        var request = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            Mode = CrashAnalysisMode.Manual,
            ImportedFilePath = filePath,
            TempDirectory = ModMain.RequestTaskTempFolder(),
            Now = DateTimeOffset.Now
        };
        _AnalyzeAndNavigate(request, null, []);
    }

    public void AnalyzeGameCrash(MinecraftCrashUiRequest request)
    {
        var launchScriptPath = Path.Combine(ModBase.exePath, "PCL", "LatestLaunch.bat");
        var launchScript = File.Exists(launchScriptPath)
            ? ModBase.ReadFile(launchScriptPath)
            : string.Empty;
        var reportFiles = new List<string>(request.ExtraReportFiles);
        if (File.Exists(launchScriptPath)) reportFiles.Add(launchScriptPath);

        var analysisRequest = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            Mode = CrashAnalysisMode.Automatic,
            InstancePath = request.VersionPath,
            MinecraftRootPath = _GetMinecraftRootPath(request.VersionPath),
            CapturedOutputLines = request.LatestOutputLines,
            LaunchScript = launchScript,
            TempDirectory = ModMain.RequestTaskTempFolder(),
            Now = DateTimeOffset.Now,
            RuntimeContext = _Create(request.Instance, launchScript)
        };
        _AnalyzeAndNavigate(analysisRequest, request.Instance, reportFiles);
    }

    private static CrashRuntimeContext _Create(McInstance? instance, string? launchScript)
    {
        var info = instance?.Info;
        return new CrashRuntimeContext
        {
            LauncherVersion = ModBase.versionBaseName,
            LauncherId = Identify.LauncherId,
            InstanceName = instance?.Name,
            InstancePath = instance?.PathInstance,
            MinecraftVersion = info?.VanillaName,
            LoaderName = _GetLoaderName(info),
            JavaInfo = _ReadLaunchLogValue("Java 信息："),
            JavaPath = _ReadLaunchLogValue("Java 路径："),
            AllocatedMemory = _ReadLaunchLogValue("分配的内存："),
            AccountName = _ReadLaunchLogValue("玩家用户名："),
            AuthType = _ReadLaunchLogValue("验证方式："),
            OperatingSystem = SystemInfo.OSInfo,
            Is32BitSystem = SystemInfo.Is32BitSystem,
            IsArm64System = SystemInfo.IsArm64System,
            CpuName = HardwareInfo.CPUName,
            SystemMemoryMb = HardwareInfo.SystemMemorySize,
            Gpus = HardwareInfo.GPUs.Select(static gpu => new CrashGpuInfo
            {
                Name = gpu.Name,
                MemoryMb = gpu.Memory,
                DriverVersion = gpu.DriverVersion
            }).ToList(),
            LaunchArguments = string.IsNullOrWhiteSpace(launchScript)
                ? []
                : launchScript.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(static item => item.StartsWith("-", StringComparison.Ordinal)).Take(40).ToList()
        };
    }

    private static string? _GetLoaderName(McInstanceInfo? info)
    {
        if (info is null) return null;
        if (info.HasFabric) return "Fabric " + info.Fabric;
        if (info.HasForge) return "Forge " + info.Forge;
        if (info.HasNeoForge) return "NeoForge " + info.NeoForge;
        if (info.HasQuilt) return "Quilt " + info.Quilt;
        if (info.HasLiteLoader) return "LiteLoader";
        if (info.HasCleanroom) return "Cleanroom " + info.Cleanroom;
        return null;
    }

    private static string? _ReadLaunchLogValue(string key)
    {
        try
        {
            var logFile = LogWrapper.CurrentLogger.CurrentLogFiles.LastOrDefault();
            if (string.IsNullOrWhiteSpace(logFile) || !File.Exists(logFile)) return null;
            var text = ModBase.ReadFile(logFile);
            var start = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += key.Length;
            var end = text.IndexOf('[', start);
            return (end < 0 ? text[start..] : text[start..end]).Trim();
        }
        catch
        {
            return null;
        }
    }

    private void _AnalyzeAndNavigate(
        CrashAnalysisRequest request,
        McInstance? instance,
        IReadOnlyList<string> extraReportFiles)
    {
        var result = _analyzer.Analyze(request);
        var markdown = CrashMarkdownBuilder.Build(result, result.Presentation, MinecraftCrashUi.LocalizeMarkdown);
        var session = new MinecraftCrashSession
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTimeOffset.Now,
            Instance = instance,
            Request = request,
            Result = result,
            Presentation = result.Presentation,
            Markdown = markdown,
            ExtraReportFiles = extraReportFiles
        };
        _NavigateTo(session);
    }

    private static void _NavigateTo(
        MinecraftCrashSession session,
        FormMain.PageSubType tab = FormMain.PageSubType.CrashOverview)
    {
        MinecraftCrashSessionStore.SetCurrent(session);
        ModBase.RunInUi(() =>
        {
            ModMain.frmMain?.PageChange(FormMain.PageType.CrashAnalysis, tab);
            if (ModMain.frmMain?.pageRight is IRefreshable refreshable)
                refreshable.Refresh();
        });
    }

    private static string? _GetMinecraftRootPath(string? instancePath)
    {
        if (string.IsNullOrWhiteSpace(instancePath)) return null;
        try
        {
            return new DirectoryInfo(instancePath).Parent?.Parent?.FullName;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MinecraftCrashUiRequest
{
    public McInstance? Instance { get; init; }
    public string? VersionPath { get; init; }
    public IReadOnlyList<string> LatestOutputLines { get; init; } = [];
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];
}
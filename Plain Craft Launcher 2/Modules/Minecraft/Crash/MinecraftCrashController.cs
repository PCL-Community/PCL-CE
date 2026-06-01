using System.IO;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

/// <summary>
///     <p>启动器层的崩溃分析协调器。</p>
///     <p>
///         该类负责把 WPF/启动器对象转换成 Core 可理解的 <see cref="CrashAnalysisRequest" />，
///         然后调用 Core 分析并交给弹窗服务展示。这里可以读取启动脚本、实例路径和临时目录，
///         但不应该写规则或拼接崩溃分析文案。
///     </p>
/// </summary>
public sealed class MinecraftCrashController
{
    private readonly CrashAnalyzer _analyzer = new();
    private readonly MinecraftCrashDialogService _dialogService = new();
    private readonly MinecraftCrashEnvironmentProvider _environmentProvider = new();

    /// <summary>
    ///     分析用户手动拖入或选择的错误报告。
    /// </summary>
    public void AnalyzeImportedCrashReport(string filePath)
    {
        var tempDirectory = ModMain.RequestTaskTempFolder();
        var request = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            Mode = CrashAnalysisMode.Manual,
            ImportedFilePath = filePath,
            TempDirectory = tempDirectory
        };

        var report = _analyzer.Analyze(request);
        MinecraftCrashDialogService.Show(report, null);
    }

    /// <summary>
    ///     分析游戏进程退出后的自动崩溃。
    /// </summary>
    public void AnalyzeGameCrash(MinecraftCrashUiRequest request)
    {
        var tempDirectory = ModMain.RequestTaskTempFolder();
        var launchScriptPath = Path.Combine(ModBase.exePath, "PCL", "LatestLaunch.bat");
        var launchScript = File.Exists(launchScriptPath) ? ModBase.ReadFile(launchScriptPath) : "";
        var minecraftRoot = _GetMinecraftRootPath(request.VersionPath);
        var reportFiles = new List<string>(request.ExtraReportFiles);
        if (File.Exists(launchScriptPath)) reportFiles.Add(launchScriptPath);

        var analysisRequest = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            Mode = CrashAnalysisMode.Automatic,
            VersionPath = request.VersionPath,
            MinecraftRootPath = minecraftRoot,
            TempDirectory = tempDirectory,
            LatestOutputLines = request.LatestOutputLines,
            LatestLaunchScript = launchScript,
            ExtraReportFiles = reportFiles,
            EnvironmentInfo = MinecraftCrashEnvironmentProvider.Create(request.Instance, launchScript)
        };

        var report = _analyzer.Analyze(analysisRequest);
        MinecraftCrashDialogService.Show(report, request.Instance);
    }

    private static string? _GetMinecraftRootPath(string? versionPath)
    {
        if (string.IsNullOrWhiteSpace(versionPath)) return null;
        try
        {
            var directory = new DirectoryInfo(versionPath);
            return directory.Parent?.Parent?.FullName;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
///     UI 层传给崩溃控制器的实时游戏崩溃上下文。
/// </summary>
public sealed record MinecraftCrashUiRequest
{
    public ModMinecraft.Instance? Instance { get; init; }
    public string? VersionPath { get; init; }
    public IReadOnlyList<string> LatestOutputLines { get; init; } = [];
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];
}
using System.IO;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public sealed class MinecraftCrashController
{
    private readonly CrashAnalyzer _analyzer = new();
    private readonly MinecraftCrashDialogService _dialogService = new();
    private readonly MinecraftCrashEnvironmentProvider _environmentProvider = new();

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

public sealed record MinecraftCrashUiRequest
{
    public ModMinecraft.Instance? Instance { get; init; }
    public string? VersionPath { get; init; }
    public IReadOnlyList<string> LatestOutputLines { get; init; } = [];
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];
}
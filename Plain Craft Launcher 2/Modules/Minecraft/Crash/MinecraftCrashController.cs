using System.IO;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public sealed class MinecraftCrashController
{
    private readonly CrashAnalyzer _analyzer = new();
    private readonly CrashMarkdownBuilder _markdownBuilder = new();

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
            RuntimeContext = MinecraftCrashEnvironmentProvider.Create(request.Instance, launchScript)
        };
        _AnalyzeAndNavigate(analysisRequest, request.Instance, reportFiles);
    }

    private void _AnalyzeAndNavigate(CrashAnalysisRequest request, ModMinecraft.Instance? instance,
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
        MinecraftCrashNavigation.NavigateTo(session);
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
    public ModMinecraft.Instance? Instance { get; init; }
    public string? VersionPath { get; init; }
    public IReadOnlyList<string> LatestOutputLines { get; init; } = [];
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];
}
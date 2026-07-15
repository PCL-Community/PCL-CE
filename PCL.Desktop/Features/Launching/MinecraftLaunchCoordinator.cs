// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PCL.Application.Downloads;
using PCL.Application.Instances;
using PCL.Application.Launching;
using PCL.Application.Minecraft.Java;
using PCL.Application.Minecraft.Launch;
using PCL.Application.Settings;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Shared;
using PCL.Desktop.Localization;
using PCL.Domain.Minecraft.Java;
using PCL.Domain.Minecraft.Launch;
using PCL.Platform.Java;
using PCL.Platform.Paths;

namespace PCL.Desktop.Features.Launching;

/// <summary>
/// Weighted launch stages aligned with the legacy WPF <c>ModLaunch</c> pipeline.
/// </summary>
internal static class MinecraftLaunchStages
{
    public const double GetJava = 4d;
    public const double Login = 15d;
    public const double CompleteFiles = 15d;
    public const double GetArguments = 2d;
    public const double ExtractNatives = 2d;
    public const double PreLaunch = 1d;
    public const double CustomCommand = 1d;
    public const double StartProcess = 2d;
    public const double WaitWindow = 1d;
    public const double End = 1d;

    public const double Total =
        GetJava + Login + CompleteFiles + GetArguments + ExtractNatives +
        PreLaunch + CustomCommand + StartProcess + WaitWindow + End;

    public static double ProgressAt(double completedWeight) =>
        Math.Clamp(completedWeight / Total, 0d, 1d);
}

internal sealed record MinecraftLaunchStageReport(
    string StageName,
    double Progress,
    bool IsLaunched = false,
    string? Method = null,
    string? DownloadSpeed = null);

internal sealed record MinecraftLaunchCoordinatorResult(
    Process Process,
    MinecraftProcessLaunchPlan Plan,
    string JavaExecutablePath,
    LoginProfileInfo Profile,
    Guid SessionId);

internal sealed class MinecraftLaunchCoordinator
{
    /// <summary>How long to watch for an early crash after Process.Start.</summary>
    private static readonly TimeSpan EarlyExitGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WindowPollInterval = TimeSpan.FromMilliseconds(150);

    private readonly MinecraftVanillaInstallService _installService;

    public MinecraftLaunchCoordinator(MinecraftVanillaInstallService installService)
    {
        _installService = installService ?? throw new ArgumentNullException(nameof(installService));
    }

    public async Task<MinecraftLaunchCoordinatorResult> RunAsync(
        MinecraftLaunchCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        double completed = 0d;
        string method = FormatLoginMethod(request.Profile);

        string javaExecutable = await RunStageWithHeartbeatAsync(
                request,
                StageName("Minecraft.Launch.Stage.GetJava", "获取 Java"),
                completed,
                MinecraftLaunchStages.GetJava,
                method,
                token => ResolveJavaExecutableAsync(request, token),
                cancellationToken)
            .ConfigureAwait(false);
        completed += MinecraftLaunchStages.GetJava;
        request.Log?.Invoke("选择的 Java：" + javaExecutable);

        LoginProfileInfo profile = await RunStageWithHeartbeatAsync(
                request,
                StageName("Minecraft.Launch.Stage.Login", "登录"),
                completed,
                MinecraftLaunchStages.Login,
                method,
                token => request.RefreshProfileAsync(request.Profile, token),
                cancellationToken)
            .ConfigureAwait(false);
        method = FormatLoginMethod(profile);
        completed += MinecraftLaunchStages.Login;
        Report(request, StageName("Minecraft.Launch.Stage.Login", "登录"), completed, method: method);

        Report(request, StageName("Minecraft.Launch.Stage.CompleteFiles", "补全文件"), completed, method: method);
        await CompleteFilesAsync(request, completed, method, cancellationToken).ConfigureAwait(false);
        completed += MinecraftLaunchStages.CompleteFiles;
        Report(request, StageName("Minecraft.Launch.Stage.CompleteFiles", "补全文件"), completed, method: method);

        MinecraftProcessLaunchPlan plan = await RunStageWithHeartbeatAsync(
                request,
                StageName("Minecraft.Launch.Stage.GetArguments", "获取启动参数"),
                completed,
                MinecraftLaunchStages.GetArguments + MinecraftLaunchStages.ExtractNatives,
                method,
                token => request.CreatePlanAsync(
                    request.Instance,
                    profile,
                    javaExecutable,
                    token,
                    request.WorldName,
                    request.Metadata,
                    request.ServerAddress),
                cancellationToken)
            .ConfigureAwait(false);
        completed += MinecraftLaunchStages.GetArguments;
        Report(request, StageName("Minecraft.Launch.Stage.ExtractNatives", "解压 Natives"), completed, method: method);
        completed += MinecraftLaunchStages.ExtractNatives;
        Report(request, StageName("Minecraft.Launch.Stage.ExtractNatives", "解压 Natives"), completed, method: method);

        Report(request, StageName("Minecraft.Launch.Stage.PreLaunch", "预启动处理"), completed, method: method);
        EnsureWorkingDirectory(plan.StartInfo.WorkingDirectory);
        completed += MinecraftLaunchStages.PreLaunch;
        Report(request, StageName("Minecraft.Launch.Stage.PreLaunch", "预启动处理"), completed, method: method);

        await RunStageWithHeartbeatAsync(
                request,
                StageName("Minecraft.Launch.Stage.CustomCommand", "执行自定义命令"),
                completed,
                MinecraftLaunchStages.CustomCommand,
                method,
                async token =>
                {
                    await RunCustomCommandIfNeededAsync(request, plan, token).ConfigureAwait(false);
                    return 0;
                },
                cancellationToken)
            .ConfigureAwait(false);
        completed += MinecraftLaunchStages.CustomCommand;
        Report(request, StageName("Minecraft.Launch.Stage.CustomCommand", "执行自定义命令"), completed, method: method);

        Report(request, StageName("Minecraft.Launch.Stage.StartProcess", "启动进程"), completed, method: method);
        // Normalize FileName before logging so the UI shows the real executable used.
        NormalizeJavaExecutableForLaunch(plan.StartInfo);
        request.Log?.Invoke(
            "启动 Java：" + plan.StartInfo.FileName +
            "\n工作目录：" + plan.StartInfo.WorkingDirectory +
            "\nNatives：" + plan.NativesDirectory +
            "\n参数预览：" + Truncate(plan.StartInfo.Arguments ?? string.Empty, 240));
        Process process = StartProcess(plan);
        GameSessionSnapshot session = GameSessionRegistry.Shared.Start(request.Instance.Name, process.Id);
        GameSessionRegistry.Shared.PublishOutput(
            session.SessionId,
            GameProcessOutputChannel.Launcher,
            "Minecraft process started.");
        _ = ObserveProcessExitAsync(process, session.SessionId);
        request.ApplyProcessPriority(process, request.Settings);
        completed += MinecraftLaunchStages.StartProcess;
        string processMethod = "PID " + process.Id.ToString(CultureInfo.InvariantCulture);
        Report(
            request,
            StageName("Minecraft.Launch.Stage.StartProcess", "启动进程"),
            completed,
            method: processMethod);

        // Mark launched so the title flips to "游戏已启动", then briefly watch for early crash.
        // Do not flood the UI with heartbeat posts during this wait (was freezing Avalonia).
        Report(
            request,
            StageName("Minecraft.Launch.Stage.WaitWindow", "等待游戏窗口"),
            completed,
            isLaunched: true,
            method: processMethod);
        await WaitForGameAppearanceAsync(process, cancellationToken, request.Log).ConfigureAwait(false);
        completed += MinecraftLaunchStages.WaitWindow;
        Report(
            request,
            StageName("Minecraft.Launch.Stage.WaitWindow", "等待游戏窗口"),
            completed,
            isLaunched: true,
            method: processMethod);

        Report(
            request,
            StageName("Minecraft.Launch.Stage.End", "结束处理"),
            completed,
            isLaunched: true,
            method: processMethod);
        completed += MinecraftLaunchStages.End;
        Report(
            request,
            StageName("Minecraft.Launch.Stage.End", "结束处理"),
            completed,
            isLaunched: true,
            method: processMethod);

        return new MinecraftLaunchCoordinatorResult(process, plan, javaExecutable, profile, session.SessionId);
    }

    private static async Task<T> RunStageWithHeartbeatAsync<T>(
        MinecraftLaunchCoordinatorRequest request,
        string stage,
        double completedBefore,
        double stageWeight,
        string method,
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken,
        bool isLaunched = false)
    {
        Report(request, stage, completedBefore, isLaunched, method);
        Task<T> workTask = work(cancellationToken);
        double softFraction = 0d;
        while (!workTask.IsCompleted)
        {
            softFraction = Math.Min(0.92d, softFraction + 0.05d);
            Report(
                request,
                stage,
                completedBefore + (stageWeight * softFraction),
                isLaunched,
                method);
            try
            {
                await Task.WhenAny(workTask, Task.Delay(120, cancellationToken)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (workTask.IsCompleted)
            {
                break;
            }
        }

        T result = await workTask.ConfigureAwait(false);
        Report(request, stage, completedBefore + stageWeight, isLaunched, method);
        return result;
    }

    public static string FormatLoginMethod(LoginProfileInfo profile) =>
        profile.Kind switch
        {
            LaunchLoginProfileKind.Microsoft => AvaloniaLocalizationManager.GetText(
                "Launch.Account.Type.Microsoft",
                "微软"),
            LaunchLoginProfileKind.ThirdParty => FormatThirdPartyMethod(profile),
            _ => AvaloniaLocalizationManager.GetText("Launch.Account.Type.Offline", "离线")
        };

    public static string PreferJavaExecutable(string javaExecutablePath, bool forceConsole)
    {
        if (string.IsNullOrWhiteSpace(javaExecutablePath) || !OperatingSystem.IsWindows())
            return javaExecutablePath;

        string directory = Path.GetDirectoryName(javaExecutablePath) ?? string.Empty;
        string fileName = Path.GetFileName(javaExecutablePath);
        if (forceConsole &&
            string.Equals(fileName, "javaw.exe", StringComparison.OrdinalIgnoreCase))
        {
            string consoleJava = Path.Combine(directory, "java.exe");
            if (File.Exists(consoleJava))
                return consoleJava;
        }

        if (!forceConsole &&
            string.Equals(fileName, "java.exe", StringComparison.OrdinalIgnoreCase))
        {
            string windowJava = Path.Combine(directory, "javaw.exe");
            if (File.Exists(windowJava))
                return windowJava;
        }

        return javaExecutablePath;
    }

    public static async Task<string> ResolveJavaExecutableAsync(
        MinecraftLaunchCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        InstanceMetadata metadata = request.Metadata;
        bool forceConsole = request.Settings.GetBooleanOption(
            "LaunchAdvanceNoJavaw",
            LauncherSettingDefaults.GetBoolean("LaunchAdvanceNoJavaw"));
        MinecraftLaunchProfile profile = BuildLaunchProfile(request.Instance);

        // Always prefer console java.exe for launch reliability (stderr + real exit codes).
        // javaw hides crashes and was producing "empty exit code" UX.
        const bool launchForceConsole = true;
        _ = forceConsole;

        // 1) Instance forced path
        if (metadata.JavaSelectionMode == 2 &&
            JavaRuntimeCatalog.TryResolveExistingJavaPath(metadata.SelectedJavaPath, out string instanceJava))
        {
            request.Log?.Invoke("使用实例指定的 Java：" + instanceJava);
            return PreferJavaExecutable(instanceJava, launchForceConsole);
        }

        // Load the same catalog Settings uses (custom roots + disabled flags).
        IReadOnlyList<JavaRuntimeCandidate> catalog =
            await JavaRuntimeCatalog.LoadAsync(request.Settings, cancellationToken).ConfigureAwait(false);

        JavaRequirementResolution requirement = JavaRuntimeRequirementResolver.Resolve(profile);
        if (!requirement.Success)
        {
            throw new InvalidOperationException(
                requirement.Detail ?? "无法解析该版本的 Java 要求。");
        }

        // 2) Global explicit selection from Settings (when not instance-auto)
        bool forceAuto = metadata.JavaSelectionMode == 1;
        bool hasGlobalSelection =
            request.Settings.TryGetTextOption(LauncherSettingKeys.LaunchSelectedJava, out string? globalJava) &&
            !string.IsNullOrWhiteSpace(globalJava);

        if (!forceAuto &&
            hasGlobalSelection &&
            JavaRuntimeCatalog.TryResolveExistingJavaPath(globalJava, out string resolvedGlobal) &&
            JavaRuntimeCatalog.IsJavaPathEnabled(request.Settings, resolvedGlobal))
        {
            request.Log?.Invoke("使用设置中选定的 Java：" + resolvedGlobal);
            return PreferJavaExecutable(resolvedGlobal, launchForceConsole);
        }

        // 3) Auto-select from Settings catalog by version range
        JavaRuntimeCandidate? best = JavaRuntimeCatalog.SelectBest(catalog, requirement.Range);
        if (best is not null)
        {
            string path = best.Installation.JavaExecutablePath;
            request.Log?.Invoke("自动选择 Java：" + path + " (" + best.Installation.MajorVersion + ")");
            return PreferJavaExecutable(path, launchForceConsole);
        }

        // 4) Auto-download Mojang runtime
        JavaSelectionResult fakeFailure = JavaSelectionResult.Failed(
            requirement,
            JavaSelectionFailureReason.NoCompatibleJava,
            suggestedDownloadComponent: JavaRuntimeAcquisitionPlanner.Plan(requirement, profile.HasForge).DownloadComponent);

        string? downloaded = await TryAutoDownloadJavaAsync(
                request,
                profile,
                fakeFailure,
                launchForceConsole,
                cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(downloaded))
            return downloaded;

        throw new InvalidOperationException(BuildNoCompatibleJavaMessage(fakeFailure));
    }

    private static async Task<string?> TryAutoDownloadJavaAsync(
        MinecraftLaunchCoordinatorRequest request,
        MinecraftLaunchProfile profile,
        JavaSelectionResult selection,
        bool forceConsole,
        CancellationToken cancellationToken)
    {
        JavaRuntimeAcquisitionDecision acquisition =
            JavaRuntimeAcquisitionPlanner.Plan(selection.Requirement, profile.HasForge);
        if (!acquisition.CanAutoDownload ||
            string.IsNullOrWhiteSpace(acquisition.DownloadComponent))
        {
            return null;
        }

        string component = acquisition.DownloadComponent;
        string versionLabel = acquisition.JavaVersionCode ?? component;
        if (request.ConfirmJavaDownloadAsync is not null)
        {
            bool confirmed = await request.ConfirmJavaDownloadAsync(versionLabel, cancellationToken)
                .ConfigureAwait(false);
            if (!confirmed)
                throw new InvalidOperationException("已取消自动下载 Java " + versionLabel + "。");
        }

        request.Log?.Invoke("未找到兼容 Java，开始自动下载 Java " + versionLabel + "…");
        DefaultPlatformPathProvider paths = new();
        string runtimeRoot = JavaRuntimeInstaller.GetDefaultRuntimeRoot(paths);
        using HttpJavaRuntimeMetadataProvider metadata = new();
        JavaRuntimeInstaller installer = new(metadata);
        Progress<JavaRuntimeInstallProgress> progress = new(update =>
        {
            request.Report(new MinecraftLaunchStageReport(
                StageName("Minecraft.Launch.Stage.GetJava", "获取 Java") + " · " + update.Stage,
                Math.Clamp(update.Progress * (MinecraftLaunchStages.GetJava / MinecraftLaunchStages.Total), 0d, 0.09d),
                Method: update.Detail ?? versionLabel));
        });

        string javaPath = await installer.InstallAsync(component, runtimeRoot, progress, cancellationToken)
            .ConfigureAwait(false);
        request.Log?.Invoke("Java 已安装：" + javaPath);

        // Re-scan catalog so the new runtime is preferred next time.
        IReadOnlyList<JavaRuntimeCandidate> catalog =
            await JavaRuntimeCatalog.LoadAsync(request.Settings, cancellationToken).ConfigureAwait(false);
        JavaRequirementResolution requirement = JavaRuntimeRequirementResolver.Resolve(profile);
        if (requirement.Success)
        {
            JavaRuntimeCandidate? best = JavaRuntimeCatalog.SelectBest(catalog, requirement.Range);
            if (best is not null)
            {
                string path = forceConsole
                    ? best.Installation.JavaExecutablePath
                    : best.Installation.WindowedJavaExecutablePath
                      ?? best.Installation.JavaExecutablePath;
                return PreferJavaExecutable(path, forceConsole);
            }
        }

        return PreferJavaExecutable(javaPath, forceConsole);
    }

    public static MinecraftLaunchProfile BuildLaunchProfile(LaunchInstanceInfo instance)
    {
        MinecraftVersionJsonInfo info = MinecraftVersionJsonInspector.Read(instance);
        Version? vanillaVersion = TryParseMinecraftVanillaVersion(info.MinecraftVersionId);
        string? neoForgeVersion = MinecraftLoaderLibraryDetector.DetectVersion(
            info.Libraries,
            "net.neoforged:neoforge",
            "net.neoforge:forge");
        // Do not use bare "forge" needle — it matches NeoForge library names.
        string? forgeVersion = neoForgeVersion is null
            ? MinecraftLoaderLibraryDetector.DetectVersion(info.Libraries, "net.minecraftforge:forge")
            : null;
        string? cleanroomVersion = MinecraftLoaderLibraryDetector.DetectVersion(
            info.Libraries,
            "com.cleanroommc:cleanroom");

        return new MinecraftLaunchProfile
        {
            InstanceId = instance.Name,
            VanillaVersion = vanillaVersion,
            HasReliableVanillaVersion = vanillaVersion is not null,
            ReleaseTime = TryReadReleaseTime(instance),
            ManifestJavaMajorVersion = TryReadManifestJavaMajor(instance),
            ManifestJavaComponent = TryReadManifestJavaComponent(instance),
            HasOptiFine = HasLoaderNeedle(instance, "optifine"),
            // Treat NeoForge as Forge-family for Java constraints, without dual labeling elsewhere.
            HasForge = forgeVersion is not null ||
                       neoForgeVersion is not null ||
                       HasLoaderNeedle(instance, "net.minecraftforge:forge") ||
                       HasLoaderNeedle(instance, "net.neoforged:neoforge"),
            ForgeVersion = forgeVersion ?? neoForgeVersion,
            HasCleanroom = cleanroomVersion is not null || HasLoaderNeedle(instance, "cleanroom"),
            CleanroomVersion = cleanroomVersion,
            HasFabric = HasLoaderNeedle(instance, "fabric-loader", "quilt-loader"),
            HasLiteLoader = HasLoaderNeedle(instance, "liteloader"),
            HasLabyMod = HasLoaderNeedle(instance, "labymod")
        };
    }

    public static async Task WaitForGameAppearanceAsync(
        Process process,
        CancellationToken cancellationToken,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + EarlyExitGracePeriod;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                string code = TryGetExitCode(process);
                throw new InvalidOperationException(
                    "游戏进程在启动后立即退出，退出码：" + code +
                    "。\n请检查版本目录下的 LatestLaunch-PCLN.bat，或查看游戏日志（logs/latest.log）。");
            }

            await Task.Delay(WindowPollInterval, cancellationToken).ConfigureAwait(false);
        }

        // Still alive after grace → success. Do not require MainWindowHandle (java/javaw often lag).
        if (process.HasExited)
        {
            string code = TryGetExitCode(process);
            throw new InvalidOperationException(
                "游戏进程在启动后立即退出，退出码：" + code +
                "。\n请检查版本目录下的 LatestLaunch-PCLN.bat，或查看游戏日志（logs/latest.log）。");
        }

        log?.Invoke("游戏进程仍在运行（PID " + process.Id.ToString(CultureInfo.InvariantCulture) + "）。");
    }

    private static async Task ObserveProcessExitAsync(Process process, Guid sessionId)
    {
        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            GameSessionRegistry.Shared.Complete(sessionId, process.ExitCode);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            GameSessionRegistry.Shared.Complete(sessionId, -1);
        }
    }

    private static void NormalizeJavaExecutableForLaunch(ProcessStartInfo startInfo)
    {
        // Log-time normalize only: StartProcess decides final java/javaw.
        _ = startInfo;
    }

    private static string TryGetExitCode(Process process)
    {
        try
        {
            return process.HasExited
                ? process.ExitCode.ToString(CultureInfo.InvariantCulture)
                : "未知（进程仍在运行）";
        }
        catch (Exception)
        {
            return "未知";
        }
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text;
        return text[..max] + "…";
    }

    private async Task CompleteFilesAsync(
        MinecraftLaunchCoordinatorRequest request,
        double completedBeforeStage,
        string method,
        CancellationToken cancellationToken)
    {
        // Fast path: client jar present → skip full RepairAsync (was freezing launches).
        string clientJar = Path.Combine(request.Instance.InstanceDirectory, request.Instance.Name + ".jar");
        string versionJson = request.Instance.VersionJsonPath;
        bool looksInstalled = File.Exists(clientJar) && File.Exists(versionJson);
        if (looksInstalled)
        {
            request.Log?.Invoke("版本文件已存在，跳过完整补全（快速路径）。");
            request.Report(new MinecraftLaunchStageReport(
                StageName("Minecraft.Launch.Stage.CompleteFiles", "补全文件"),
                MinecraftLaunchStages.ProgressAt(completedBeforeStage + MinecraftLaunchStages.CompleteFiles),
                Method: method));
            return;
        }

        Progress<MinecraftInstallProgress> progress = new(update =>
        {
            double stageFraction = Math.Clamp(update.Progress, 0d, 1d);
            double weightProgress = completedBeforeStage + (MinecraftLaunchStages.CompleteFiles * stageFraction);
            string? speed = update.SpeedBytesPerSecond > 0
                ? FormatSpeed(update.SpeedBytesPerSecond)
                : null;
            string stage = StageName("Minecraft.Launch.Stage.CompleteFiles", "补全文件");
            if (!string.IsNullOrWhiteSpace(update.Detail))
                stage = stage + " · " + update.Detail;

            request.Report(new MinecraftLaunchStageReport(
                stage,
                MinecraftLaunchStages.ProgressAt(weightProgress),
                Method: method,
                DownloadSpeed: speed));
        });

        await _installService.RepairAsync(
                new MinecraftRepairRequest
                {
                    VersionId = request.Instance.Name,
                    VersionJsonPath = request.Instance.VersionJsonPath,
                    MinecraftRootDirectory = request.MinecraftRootDirectory,
                    InstanceDirectory = request.Instance.InstanceDirectory,
                    PreferOfficialSource = request.PreferOfficialSource
                },
                progress,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task RunCustomCommandIfNeededAsync(
        MinecraftLaunchCoordinatorRequest request,
        MinecraftProcessLaunchPlan plan,
        CancellationToken cancellationToken)
    {
        string preLaunchCommand = FirstNonEmpty(
            request.Metadata.PreLaunchCommand,
            request.Settings.GetTextOption(
                "LaunchAdvanceRun",
                LauncherSettingDefaults.GetText("LaunchAdvanceRun"))) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(preLaunchCommand))
            return;

        bool waitForPreLaunchCommand = !string.IsNullOrWhiteSpace(request.Metadata.PreLaunchCommand)
            ? request.Metadata.WaitForPreLaunchCommand
            : request.Settings.GetBooleanOption(
                "LaunchAdvanceRunWait",
                LauncherSettingDefaults.GetBoolean("LaunchAdvanceRunWait"));

        await request.RunPreLaunchCommandAsync(
                preLaunchCommand,
                waitForPreLaunchCommand,
                plan.StartInfo.WorkingDirectory,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Process StartProcess(MinecraftProcessLaunchPlan plan)
    {
        ProcessStartInfo startInfo = plan.StartInfo;

        // Prefer java.exe (matches WPF LatestLaunch.bat). Do NOT permanently redirect stdio —
        // Minecraft logs heavily; an undrained redirected stderr pipe freezes/kills the process.
        if (OperatingSystem.IsWindows() &&
            string.Equals(Path.GetFileName(startInfo.FileName), "javaw.exe", StringComparison.OrdinalIgnoreCase))
        {
            string? dir = Path.GetDirectoryName(startInfo.FileName);
            string console = Path.Combine(dir ?? string.Empty, "java.exe");
            if (File.Exists(console))
                startInfo.FileName = console;
        }

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardError = false;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardInput = false;
        // Always hide console — flashing black CMD is poor UX. Errors go to game logs / LatestLaunch bat.
        startInfo.CreateNoWindow = true;

        // Prefer javaw on Windows for GUI games (no console attach). Keep java.exe only if javaw missing.
        if (OperatingSystem.IsWindows() &&
            string.Equals(Path.GetFileName(startInfo.FileName), "java.exe", StringComparison.OrdinalIgnoreCase))
        {
            string? dir = Path.GetDirectoryName(startInfo.FileName);
            string windowed = Path.Combine(dir ?? string.Empty, "javaw.exe");
            if (File.Exists(windowed))
                startInfo.FileName = windowed;
        }

        if (string.IsNullOrWhiteSpace(startInfo.FileName) || !File.Exists(startInfo.FileName))
        {
            throw new FileNotFoundException(
                "找不到 Java 可执行文件：" + (startInfo.FileName ?? "(null)"),
                startInfo.FileName);
        }

        TryWriteLatestLaunchBatch(startInfo);

        Process? process = Process.Start(startInfo);
        if (process is null)
            throw new InvalidOperationException("Java 进程未能启动。文件：" + startInfo.FileName);
        return process;
    }

    private static void TryWriteLatestLaunchBatch(ProcessStartInfo startInfo)
    {
        try
        {
            string workDir = string.IsNullOrWhiteSpace(startInfo.WorkingDirectory)
                ? Environment.CurrentDirectory
                : startInfo.WorkingDirectory;
            Directory.CreateDirectory(workDir);
            string batPath = Path.Combine(workDir, "LatestLaunch-PCLN.bat");
            string fileName = startInfo.FileName ?? "java";
            string args = startInfo.Arguments ?? string.Empty;
            string content =
                "chcp 65001>nul\r\n" +
                "@echo off\r\n" +
                "cd /D \"" + workDir.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"\r\n" +
                "\"" + fileName.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" " + args + "\r\n" +
                "echo.\r\necho Exit code: %ERRORLEVEL%\r\npause\r\n";
            File.WriteAllText(batPath, content, Encoding.UTF8);
        }
        catch
        {
            // Best-effort debug aid only.
        }
    }

    private static void EnsureWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
            return;

        Directory.CreateDirectory(workingDirectory);
    }

    private static void Report(
        MinecraftLaunchCoordinatorRequest request,
        string stage,
        double completedWeight,
        bool isLaunched = false,
        string? method = null)
    {
        request.Report(new MinecraftLaunchStageReport(
            stage,
            MinecraftLaunchStages.ProgressAt(completedWeight),
            isLaunched,
            method));
    }

    private static string StageName(string key, string fallback) =>
        AvaloniaLocalizationManager.GetText(key, fallback);

    private static string FormatThirdPartyMethod(LoginProfileInfo profile)
    {
        string baseName = AvaloniaLocalizationManager.GetText("Launch.Account.Type.ThirdParty", "第三方");
        string serverLabel = !string.IsNullOrWhiteSpace(profile.AuthServer)
            ? profile.AuthServer
            : profile.Info;
        return string.IsNullOrWhiteSpace(serverLabel) ||
               string.Equals(serverLabel, baseName, StringComparison.OrdinalIgnoreCase)
            ? baseName
            : baseName + " / " + serverLabel;
    }

    private static string BuildNoCompatibleJavaMessage(JavaSelectionResult selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.SuggestedDownloadComponent))
        {
            return "未找到兼容的 Java。建议安装 " + selection.SuggestedDownloadComponent +
                   " 后重试，或在设置中手动选择 Java。";
        }

        if (selection.Requirement.Success)
        {
            return "未找到兼容的 Java（需要 " +
                   FormatJavaVersionBound(selection.Requirement.Range.Minimum) +
                   "–" +
                   FormatJavaVersionBound(selection.Requirement.Range.Maximum) +
                   "）。请在设置中安装或选择合适的 Java。";
        }

        return selection.Detail ?? "未找到兼容的 Java。";
    }

    private static string FormatJavaVersionBound(Version version)
    {
        int major = version.Major == 1 ? version.Minor : version.Major;
        return major.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return bytesPerSecond.ToString(CultureInfo.InvariantCulture) + " B/s";
        if (bytesPerSecond < 1024 * 1024)
            return (bytesPerSecond / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB/s";
        return (bytesPerSecond / (1024d * 1024d)).ToString("0.00", CultureInfo.InvariantCulture) + " MB/s";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Maps Minecraft ids like "1.20.5" / "1.8.9" to the domain form used by
    /// <see cref="JavaRuntimeRequirementResolver"/> (e.g. Version(20, 0, 5)).
    /// </summary>
    internal static Version? TryParseMinecraftVanillaVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string cleaned = text.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
            .Trim();
        if (cleaned.Length == 0)
            return null;

        // Snapshot / non-semver ids: leave unresolved so release-time + manifest rules apply.
        if (!char.IsDigit(cleaned[0]))
            return null;

        // Classic Minecraft ids are "1.X" or "1.X.Y" where X is the series major (8, 12, 20…).
        if (cleaned.StartsWith("1.", StringComparison.Ordinal) &&
            cleaned.Length > 2 &&
            char.IsDigit(cleaned[2]))
        {
            string rest = cleaned[2..];
            string[] parts = rest.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 1 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major))
            {
                int minor = 0;
                int patch = 0;
                if (parts.Length == 2 &&
                    int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int second))
                {
                    // "1.20.5" → (20, 0, 5); "1.8.9" → (8, 0, 9)
                    patch = second;
                }
                else if (parts.Length >= 3 &&
                         int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor) &&
                         int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch))
                {
                    // Defensive for unexpected "1.20.0.1" style.
                }

                return new Version(major, minor, patch, 0);
            }
        }

        return Version.TryParse(cleaned, out Version? version) ? version : null;
    }

    private static bool HasLoaderNeedle(LaunchInstanceInfo instance, params string[] needles)
    {
        MinecraftVersionJsonInfo info = MinecraftVersionJsonInspector.Read(instance);
        return info.Libraries.Any(library =>
            needles.Any(needle => library.Contains(needle, StringComparison.OrdinalIgnoreCase)));
    }

    private static DateTimeOffset? TryReadReleaseTime(LaunchInstanceInfo instance)
    {
        try
        {
            if (!File.Exists(instance.VersionJsonPath))
                return null;

            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("releaseTime", out JsonElement releaseTime) &&
                releaseTime.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(
                    releaseTime.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out DateTimeOffset value))
            {
                return value;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static int? TryReadManifestJavaMajor(LaunchInstanceInfo instance)
    {
        try
        {
            if (!File.Exists(instance.VersionJsonPath))
                return null;

            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("javaVersion", out JsonElement javaVersion) &&
                javaVersion.ValueKind == JsonValueKind.Object &&
                javaVersion.TryGetProperty("majorVersion", out JsonElement major) &&
                major.TryGetInt32(out int value))
            {
                return value;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static string? TryReadManifestJavaComponent(LaunchInstanceInfo instance)
    {
        try
        {
            if (!File.Exists(instance.VersionJsonPath))
                return null;

            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("javaVersion", out JsonElement javaVersion) &&
                javaVersion.ValueKind == JsonValueKind.Object &&
                javaVersion.TryGetProperty("component", out JsonElement component) &&
                component.ValueKind == JsonValueKind.String)
            {
                return component.GetString();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }
}

internal sealed class MinecraftLaunchCoordinatorRequest
{
    public required LaunchInstanceInfo Instance { get; init; }
    public required LoginProfileInfo Profile { get; init; }
    public required InstanceMetadata Metadata { get; init; }
    public required LauncherSettings Settings { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public bool PreferOfficialSource { get; init; } = true;
    public string? WorldName { get; init; }
    public string? ServerAddress { get; init; }

    public required Action<MinecraftLaunchStageReport> Report { get; init; }
    public Action<string>? Log { get; init; }

    public required Func<LoginProfileInfo, CancellationToken, Task<LoginProfileInfo>> RefreshProfileAsync { get; init; }

    public required Func<
        LaunchInstanceInfo,
        LoginProfileInfo,
        string,
        CancellationToken,
        string?,
        InstanceMetadata?,
        string?,
        Task<MinecraftProcessLaunchPlan>> CreatePlanAsync { get; init; }

    public required Func<string, bool, string, CancellationToken, Task> RunPreLaunchCommandAsync { get; init; }

    public required Action<Process, LauncherSettings> ApplyProcessPriority { get; init; }

    /// <summary>
    /// Optional UI confirmation before downloading a Mojang Java runtime.
    /// Return true to download, false to cancel launch.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? ConfirmJavaDownloadAsync { get; init; }
}
